using System.Diagnostics;
using System.Text;
using WinSU.Core.Input;
using WinSU.Core.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SDL;
using static SDL.SDL3;

namespace WinSU.Infrastructure.Input;

/// <summary>
/// Manettes via SDL3, sur un thread dédié (SDL doit être initialisé et interrogé sur le même thread).
/// Liaisons en vigueur (<see cref="InputSettings.GetControllerBindings"/>, plusieurs par action depuis M11) : une manette
/// reconnue (« gamepad ») est lue par position (événements « gamepad » : boutons et axes) et par ses entrées brutes
/// (liaisons enregistrées avant M11) ; une autre, par ses entrées brutes, ou par la table générique si aucune liaison
/// brute n'existe. La capture renvoie une position pour une manette reconnue, une entrée brute sinon.
/// L'état de chaque manette reconnue (boutons, sticks, gâchettes) est gardé pour l'aperçu de la manette (étape 22) :
/// <see cref="Gamepad"/> est celui de la dernière qui a servi.
/// </summary>
public sealed unsafe class SdlInputService : IInputService, IHostedService, IDisposable
{
    private const int WaitTimeoutMs = 8;

    private readonly ISettingsStore _settings;
    private readonly ILogger<SdlInputService> _logger;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lock _captureLock = new();

    // État propre au thread SDL.
    private readonly Dictionary<SDL_JoystickID, OpenController> _open = [];
    private readonly HashSet<string> _activeSources = new(StringComparer.Ordinal);
    private readonly ActionRepeater _repeater = new();
    private readonly List<(InputAction Action, bool IsRepeat)> _pending = [];
    private OpenController? _activePad;
    private bool _padDirty;

    private volatile Settings _current;
    private volatile GamepadState? _gamepad;
    private volatile bool _resetRequested = true;
    private volatile bool _stopping;
    private volatile bool _available;
    private volatile ControllerInfo[] _controllers = [];
    private CaptureRequest? _capture;
    private Thread? _thread;

    public SdlInputService(ISettingsStore settings, ILogger<SdlInputService> logger)
    {
        _settings = settings;
        _logger = logger;
        _current = new Settings(settings.Current.Input);
        settings.Changed += OnSettingsChanged;
    }

    public bool IsAvailable => _available;

    public IReadOnlyList<ControllerInfo> Controllers => _controllers;

    public event EventHandler<InputActionEvent>? ActionTriggered;

    public event EventHandler? ControllersChanged;

    public GamepadState? Gamepad => _gamepad;

    public event EventHandler? GamepadChanged;

    /// <summary>Terminée quand SDL a démarré (ou échoué). Utile aux tests.</summary>
    internal Task Started => _started.Task;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_thread is not null)
            return Task.CompletedTask;

        _thread = new Thread(Run) { IsBackground = true, Name = "WinSU SDL input" };
        _thread.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _stopping = true;
        _thread?.Join(TimeSpan.FromSeconds(2));
        _thread = null;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _settings.Changed -= OnSettingsChanged;
        StopAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    public Task<CapturedInput> CaptureAsync(CancellationToken cancellationToken = default)
    {
        var request = new CaptureRequest();
        lock (_captureLock)
        {
            _capture?.Completion.TrySetCanceled(CancellationToken.None);
            _capture = request;
        }

        _resetRequested = true; // aucune action maintenue ne doit se répéter pendant la capture
        cancellationToken.Register(() =>
        {
            lock (_captureLock)
            {
                if (_capture == request)
                    _capture = null;
            }

            request.Completion.TrySetCanceled(cancellationToken);
        });

        return request.Completion.Task;
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (e.Previous.Input == e.Current.Input)
            return;

        _current = new Settings(e.Current.Input);
        _resetRequested = true;
    }

    private void Run()
    {
        try
        {
            SDL_SetHint(SDL_HINT_JOYSTICK_ALLOW_BACKGROUND_EVENTS, "1"u8);
            if (!SDL_Init(SDL_InitFlags.SDL_INIT_JOYSTICK | SDL_InitFlags.SDL_INIT_GAMEPAD))
            {
                _logger.LogError("SDL n'a pas démarré ({Error}) : seul le clavier fonctionnera", SDL_GetError());
                return;
            }

            _available = true;
            var version = SDL_GetVersion();
            _logger.LogInformation("SDL {Major}.{Minor}.{Patch} démarré", version / 1_000_000, version / 1000 % 1000, version % 1000);
            _started.TrySetResult();

            SDL_Event e;
            while (!_stopping)
            {
                ApplyPendingReset();
                if (SDL_WaitEventTimeout(&e, WaitTimeoutMs))
                {
                    Handle(&e);
                    while (SDL_PollEvent(&e))
                        Handle(&e);
                }

                _repeater.Tick(_clock.ElapsedMilliseconds, _pending);
                PublishGamepad(); // avant les actions : l'interface qui reçoit une action lit un état à jour
                Flush();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "La boucle des entrées manette s'est arrêtée");
        }
        finally
        {
            foreach (var controller in _open.Values)
                controller.Close();
            _open.Clear();
            _activePad = null;
            _gamepad = null;
            if (_available)
                SDL_Quit();
            _available = false;
            _started.TrySetResult();
        }
    }

    private void ApplyPendingReset()
    {
        if (!_resetRequested)
            return;

        _resetRequested = false;
        var settings = _current;
        _repeater.ReleaseAll();
        _repeater.RepeatDelayMs = settings.Input.RepeatDelayMs;
        _repeater.RepeatIntervalMs = settings.Input.RepeatIntervalMs;
    }

    private void Handle(SDL_Event* e)
    {
        switch ((SDL_EventType)e->type)
        {
            case SDL_EventType.SDL_EVENT_JOYSTICK_ADDED:
                OnAdded(e->jdevice.which);
                break;
            case SDL_EventType.SDL_EVENT_JOYSTICK_REMOVED:
                OnRemoved(e->jdevice.which);
                break;
            case SDL_EventType.SDL_EVENT_JOYSTICK_BUTTON_DOWN:
            case SDL_EventType.SDL_EVENT_JOYSTICK_BUTTON_UP:
                if (_open.TryGetValue(e->jbutton.which, out var buttonOwner))
                    OnRaw(buttonOwner, InputBinding.RawButton(e->jbutton.button), e->jbutton.down);
                break;
            case SDL_EventType.SDL_EVENT_JOYSTICK_AXIS_MOTION:
                if (_open.TryGetValue(e->jaxis.which, out var axisOwner))
                    OnJoystickAxis(axisOwner, e->jaxis.axis, e->jaxis.value);
                break;
            case SDL_EventType.SDL_EVENT_JOYSTICK_HAT_MOTION:
                if (_open.TryGetValue(e->jhat.which, out var hatOwner))
                    OnJoystickHat(hatOwner, e->jhat.hat, e->jhat.value);
                break;
            case SDL_EventType.SDL_EVENT_GAMEPAD_BUTTON_DOWN:
            case SDL_EventType.SDL_EVENT_GAMEPAD_BUTTON_UP:
                if (_open.TryGetValue(e->gbutton.which, out var gamepadOwner))
                    OnGamepadButton(gamepadOwner, (SDL_GamepadButton)e->gbutton.button, e->gbutton.down);
                break;
            case SDL_EventType.SDL_EVENT_GAMEPAD_AXIS_MOTION:
                if (_open.TryGetValue(e->gaxis.which, out var stickOwner))
                    OnGamepadAxis(stickOwner, (SDL_GamepadAxis)e->gaxis.axis, e->gaxis.value);
                break;
        }
    }

    private void OnAdded(SDL_JoystickID id)
    {
        if (_open.ContainsKey(id))
            return;

        var joystick = SDL_OpenJoystick(id);
        if (joystick == null)
        {
            _logger.LogWarning("Manette {Id} détectée mais impossible à ouvrir : {Error}", (uint)id, SDL_GetError());
            return;
        }

        var gamepad = SDL_IsGamepad(id) ? SDL_OpenGamepad(id) : null;
        var name = (gamepad != null ? SDL_GetGamepadName(gamepad) : null) ?? SDL_GetJoystickName(joystick) ?? "Manette";
        var type = gamepad != null ? SDL_GetGamepadType(gamepad) : SDL_GamepadType.SDL_GAMEPAD_TYPE_UNKNOWN;
        var info = new ControllerInfo((int)(uint)id, name, GuidOf(joystick), gamepad != null, gamepad != null ? SDL_GetGamepadMapping(gamepad) : null);

        var axes = new AxisState[Math.Max(0, SDL_GetNumJoystickAxes(joystick))];
        for (var i = 0; i < axes.Length; i++)
        {
            short rest;
            axes[i].Rest = SDL_GetJoystickAxisInitialState(joystick, i, &rest) ? rest : (short)0;
        }

        var controller = new OpenController(joystick, gamepad, info, axes, new byte[Math.Max(0, SDL_GetNumJoystickHats(joystick))]);
        _open[id] = controller;
        if (info.IsGamepad && _activePad is null)
        {
            _activePad = controller;
            _padDirty = true;
        }

        PublishControllers();
        _logger.LogInformation(
            "Manette connectée : {Name} (gamepad : {IsGamepad}, type {Type}, {Axes} axes)", name, info.IsGamepad, type, axes.Length);
    }


    private void OnRemoved(SDL_JoystickID id)
    {
        if (!_open.Remove(id, out var controller))
            return;

        var prefix = SourcePrefix(controller);
        _repeater.ReleaseSources(prefix);
        _activeSources.RemoveWhere(source => source.StartsWith(prefix, StringComparison.Ordinal));
        if (_activePad == controller)
        {
            _activePad = _open.Values.FirstOrDefault(c => c.Info.IsGamepad);
            _padDirty = true;
        }

        controller.Close();
        PublishControllers();
        _logger.LogInformation("Manette déconnectée : {Name}", controller.Info.Name);
    }

    private void OnJoystickAxis(OpenController controller, int axis, short value)
    {
        if (axis >= controller.Axes.Length)
            return;

        var (positiveChanged, negativeChanged) = controller.Axes[axis].Update(value, _current.Input.AxisThreshold);
        if (positiveChanged)
            OnRaw(controller, AxisBinding(axis, InputDirection.Positive), controller.Axes[axis].Positive);
        if (negativeChanged)
            OnRaw(controller, AxisBinding(axis, InputDirection.Negative), controller.Axes[axis].Negative);
    }

    private void OnJoystickHat(OpenController controller, int hat, byte value)
    {
        if (hat >= controller.Hats.Length)
            return;

        var previous = controller.Hats[hat];
        controller.Hats[hat] = value;
        foreach (var (bit, direction) in HatDirections)
        {
            var was = (previous & bit) != 0;
            var now = (value & bit) != 0;
            if (was != now)
                OnRaw(controller, InputBinding.RawHat(hat, direction), now);
        }
    }

    /// <summary>
    /// Entrée « joystick » brute : capture d'une manette non reconnue, liaisons brutes (toutes les manettes), table
    /// générique (manette non reconnue, aucune liaison brute).
    /// </summary>
    private void OnRaw(OpenController controller, InputBinding binding, bool active)
    {
        var source = SourceKey(controller, binding);
        if (active ? !_activeSources.Add(source) : !_activeSources.Remove(source))
            return; // pas de changement réel

        // Une manette reconnue est capturée par position (événements « gamepad ») : ses entrées brutes se taisent.
        if (controller.Info.IsGamepad ? IsCapturing() : TryCapture(controller, source, binding, active))
            return;

        var map = _current.Bindings;
        Dispatch(!controller.Info.IsGamepad && !map.HasRaw ? BindingMap.GenericJoystick : map, source, binding, active);
    }

    /// <summary>Bouton d'une manette reconnue, par position.</summary>
    private void OnGamepadButton(OpenController controller, SDL_GamepadButton button, bool down)
    {
        var index = (int)button;
        if (index >= 0 && index < controller.PadButtons.Length)
            controller.PadButtons[index] = down;
        OnPadChanged(controller, used: down);

        OnPosition(controller, $"{SourcePrefix(controller)}g{index}", InputBinding.Pad((GamepadButton)index), down);
    }

    /// <summary>Axe d'une manette reconnue (sticks et gâchettes), dans chaque sens, par rapport à sa position de repos.</summary>
    private void OnGamepadAxis(OpenController controller, SDL_GamepadAxis axis, short value)
    {
        var index = (int)axis;
        if (index < 0 || index >= controller.PadAxes.Length)
            return;

        controller.PadValues[index] = value;
        var (positiveChanged, negativeChanged) = controller.PadAxes[index].Update(value, _current.Input.AxisThreshold);
        var state = controller.PadAxes[index];
        OnPadChanged(controller, used: (positiveChanged && state.Positive) || (negativeChanged && state.Negative));

        if (positiveChanged)
            OnPosition(controller, $"{SourcePrefix(controller)}ga{index}+", InputBinding.Pad((GamepadAxis)index, InputDirection.Positive), state.Positive);
        if (negativeChanged)
            OnPosition(controller, $"{SourcePrefix(controller)}ga{index}-", InputBinding.Pad((GamepadAxis)index, InputDirection.Negative), state.Negative);
    }

    private void OnPosition(OpenController controller, string source, InputBinding binding, bool active)
    {
        if (active ? !_activeSources.Add(source) : !_activeSources.Remove(source))
            return;

        if (!TryCapture(controller, source, binding, active))
            Dispatch(_current.Bindings, source, binding, active);
    }

    private void Dispatch(BindingMap map, string source, InputBinding binding, bool active)
    {
        foreach (var action in map.Lookup(binding))
            _repeater.Update(source, action, active, _clock.ElapsedMilliseconds, _pending);
    }

    private bool IsCapturing()
    {
        lock (_captureLock)
            return _capture is not null;
    }

    private bool TryCapture(OpenController controller, string source, InputBinding binding, bool active)
    {
        CaptureRequest? request;
        lock (_captureLock)
        {
            request = _capture;
            if (request is null)
                return false;

            // Les entrées déjà enfoncées au début de la capture sont ignorées jusqu'à leur relâchement.
            if (request.Held is null)
            {
                request.Held = new HashSet<string>(_activeSources, StringComparer.Ordinal);
                if (active)
                    request.Held.Remove(source);
            }

            if (!active)
            {
                request.Held.Remove(source);
                return true;
            }

            if (request.Held.Contains(source))
                return true;

            _capture = null;
        }

        request.Completion.TrySetResult(new CapturedInput(controller.Info, binding));
        return true;
    }

    private void Flush()
    {
        if (_pending.Count == 0)
            return;

        foreach (var (action, isRepeat) in _pending)
            ActionTriggered?.Invoke(this, new InputActionEvent(action, isRepeat, InputDevice.Controller));
        _pending.Clear();
    }

    /// <summary>
    /// L'état d'une manette reconnue a changé. <paramref name="used"/> : un bouton enfoncé ou un axe poussé au-delà du
    /// seuil ; elle devient la manette de l'aperçu (un stick qui tremble au repos ne la lui prend pas).
    /// </summary>
    private void OnPadChanged(OpenController controller, bool used)
    {
        if (used)
            _activePad = controller;
        if (controller == _activePad)
            _padDirty = true;
    }

    private void PublishGamepad()
    {
        if (!_padDirty)
            return;

        _padDirty = false;
        _gamepad = _activePad?.Snapshot();
        GamepadChanged?.Invoke(this, EventArgs.Empty);
    }

    private void PublishControllers()
    {
        _controllers = [.. _open.Values.Select(c => c.Info)];
        ControllersChanged?.Invoke(this, EventArgs.Empty);
    }

    private static readonly (byte Bit, InputDirection Direction)[] HatDirections =
    [
        ((byte)SDL_HAT_UP, InputDirection.Up),
        ((byte)SDL_HAT_DOWN, InputDirection.Down),
        ((byte)SDL_HAT_LEFT, InputDirection.Left),
        ((byte)SDL_HAT_RIGHT, InputDirection.Right),
    ];

    private static InputBinding AxisBinding(int axis, InputDirection direction) => InputBinding.RawAxis(axis, direction);

    private static string SourcePrefix(OpenController controller) => $"{controller.Info.InstanceId}:";

    private static string SourceKey(OpenController controller, InputBinding binding) =>
        $"{SourcePrefix(controller)}{binding.Kind}{binding.Index}{binding.Direction}";

    private static string GuidOf(SDL_Joystick* joystick)
    {
        var buffer = stackalloc byte[33];
        SDL_GUIDToString(SDL_GetJoystickGUID(joystick), buffer, 33);
        return Encoding.ASCII.GetString(buffer, 32).TrimEnd('\0');
    }

    private sealed record Settings(InputSettings Input)
    {
        public BindingMap Bindings { get; } = BindingMap.From(Input.GetControllerBindings());
    }

    private sealed class CaptureRequest
    {
        public TaskCompletionSource<CapturedInput> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public HashSet<string>? Held { get; set; }
    }

    private sealed class OpenController(SDL_Joystick* joystick, SDL_Gamepad* gamepad, ControllerInfo info, AxisState[] axes, byte[] hats)
    {
        public ControllerInfo Info { get; } = info;
        public AxisState[] Axes { get; } = axes;
        public byte[] Hats { get; } = hats;
        /// <summary>Axes « gamepad » (sticks et gâchettes, numérotation de SDL) : au repos à 0.</summary>
        public AxisState[] PadAxes { get; } = new AxisState[(int)SDL_GamepadAxis.SDL_GAMEPAD_AXIS_COUNT];
        /// <summary>État brut pour l'aperçu de la manette : boutons et valeurs des axes « gamepad ».</summary>
        public bool[] PadButtons { get; } = new bool[(int)SDL_GamepadButton.SDL_GAMEPAD_BUTTON_COUNT];
        public short[] PadValues { get; } = new short[(int)SDL_GamepadAxis.SDL_GAMEPAD_AXIS_COUNT];

        public GamepadState Snapshot() => new(Info, [.. PadButtons], [.. PadValues.Select(GamepadState.Normalize)]);

        public void Close()
        {
            if (gamepad != null)
                SDL_CloseGamepad(gamepad);
            SDL_CloseJoystick(joystick);
        }
    }
}
