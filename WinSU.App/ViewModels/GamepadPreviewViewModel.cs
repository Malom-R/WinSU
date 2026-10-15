using CommunityToolkit.Mvvm.ComponentModel;
using WinSU.App.Input;
using WinSU.App.Localization;
using WinSU.App.Services;
using WinSU.Core.Input;
using WinSU.Core.Settings;

namespace WinSU.App.ViewModels;

/// <summary>Un bouton de l'aperçu de la manette : glyphe dans le style des boutons, allumé quand il est enfoncé.</summary>
public sealed partial class PadButtonViewModel(GamepadButton button) : ObservableObject
{
    public GamepadButton Button { get; } = button;

    [ObservableProperty]
    public partial string? Glyph { get; set; }

    [ObservableProperty]
    public partial GlyphTint Tint { get; set; }

    [ObservableProperty]
    public partial bool IsLit { get; set; }

    public void Show(BindingText glyph)
    {
        Glyph = glyph.Glyph;
        Tint = glyph.Tint;
    }
}

/// <summary>Un stick : position (−1 à 1 sur chaque axe, vers le haut = négatif), poussé au-delà du seuil, cliqué.</summary>
public sealed partial class PadStickViewModel(GamepadButton click, GamepadAxis x, GamepadAxis y) : ObservableObject
{
    [ObservableProperty]
    public partial double X { get; set; }

    [ObservableProperty]
    public partial double Y { get; set; }

    /// <summary>Poussé assez loin pour compter comme une direction (seuil des réglages).</summary>
    [ObservableProperty]
    public partial bool IsTilted { get; set; }

    [ObservableProperty]
    public partial bool IsPressed { get; set; }

    [ObservableProperty]
    public partial string? Glyph { get; set; }

    public void Apply(GamepadState? state, double threshold)
    {
        X = state?.Axis(x) ?? 0;
        Y = state?.Axis(y) ?? 0;
        IsTilted = Math.Max(Math.Abs(X), Math.Abs(Y)) >= threshold;
        IsPressed = state?.IsPressed(click) == true;
    }
}

/// <summary>Une gâchette : pression de 0 à 1, allumée au-delà du seuil.</summary>
public sealed partial class PadTriggerViewModel(GamepadAxis axis) : ObservableObject
{
    public GamepadAxis Axis { get; } = axis;

    [ObservableProperty]
    public partial double Value { get; set; }

    [ObservableProperty]
    public partial bool IsLit { get; set; }

    [ObservableProperty]
    public partial string? Glyph { get; set; }

    public void Apply(GamepadState? state, double threshold)
    {
        Value = Math.Clamp(state?.Axis(Axis) ?? 0, 0, 1);
        IsLit = Value >= threshold;
    }
}

/// <summary>
/// Aperçu de la manette (« Input Overlay », étape 22, D-45 à D-47) : l'état de la manette reconnue en cours
/// (<see cref="IInputService.Gamepad"/>) bouton par bouton, la position des sticks, la pression des gâchettes et la
/// dernière action déclenchée (« Ⓐ → Valider »). Glyphes dans le style des boutons des réglages. Indépendant du style
/// d'interface (D-2) : chaque vue dessine la manette à sa façon. Ne suit la manette qu'entre <see cref="Start"/> et
/// <see cref="Stop"/> ; les changements arrivés entre deux passages sur le thread d'interface sont regroupés (un stick
/// en mouvement en envoie des dizaines par seconde).
/// </summary>
public sealed partial class GamepadPreviewViewModel : ObservableObject
{
    private readonly IInputService _input;
    private readonly ISettingsStore _settings;
    private readonly PadButtonViewModel[] _buttons;
    private GlyphStyle _glyphs = GlyphStyle.Default;
    private int _refreshQueued;
    private readonly bool _withIndicators;
    private bool _running;

    /// <param name="withIndicators">
    /// Les témoins des actions sont affichés dessous (assistant) : sans manette, l'état le rappelle. Faux pour l'écran
    /// « Boutons » (étape 32), qui n'en a pas.
    /// </param>
    public GamepadPreviewViewModel(IInputService input, ISettingsStore settings, bool withIndicators = true)
    {
        _input = input;
        _settings = settings;
        _withIndicators = withIndicators;
        _buttons =
        [
            South, East, West, North, Back, Guide, Start, LeftShoulder, RightShoulder,
            DPadUp, DPadDown, DPadLeft, DPadRight,
        ];
        Status = StatusText(null);
    }

    public PadButtonViewModel South { get; } = new(GamepadButton.South);
    public PadButtonViewModel East { get; } = new(GamepadButton.East);
    public PadButtonViewModel West { get; } = new(GamepadButton.West);
    public PadButtonViewModel North { get; } = new(GamepadButton.North);
    public PadButtonViewModel Back { get; } = new(GamepadButton.Back);
    public PadButtonViewModel Guide { get; } = new(GamepadButton.Guide);
    public PadButtonViewModel Start { get; } = new(GamepadButton.Start);
    public PadButtonViewModel LeftShoulder { get; } = new(GamepadButton.LeftShoulder);
    public PadButtonViewModel RightShoulder { get; } = new(GamepadButton.RightShoulder);
    public PadButtonViewModel DPadUp { get; } = new(GamepadButton.DPadUp);
    public PadButtonViewModel DPadDown { get; } = new(GamepadButton.DPadDown);
    public PadButtonViewModel DPadLeft { get; } = new(GamepadButton.DPadLeft);
    public PadButtonViewModel DPadRight { get; } = new(GamepadButton.DPadRight);

    public PadStickViewModel LeftStick { get; } = new(GamepadButton.LeftStick, GamepadAxis.LeftX, GamepadAxis.LeftY);
    public PadStickViewModel RightStick { get; } = new(GamepadButton.RightStick, GamepadAxis.RightX, GamepadAxis.RightY);

    public PadTriggerViewModel LeftTrigger { get; } = new(GamepadAxis.LeftTrigger);
    public PadTriggerViewModel RightTrigger { get; } = new(GamepadAxis.RightTrigger);

    /// <summary>Une manette reconnue est suivie ; sinon la silhouette est grisée et seuls les témoins s'allument.</summary>
    [ObservableProperty]
    public partial bool HasGamepad { get; set; }

    /// <summary>Nom de la manette suivie, ou pourquoi il n'y en a pas.</summary>
    [ObservableProperty]
    public partial string Status { get; set; }

    /// <summary>Dernière action déclenchée (« Valider »).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAction))]
    public partial string? ActionLabel { get; set; }

    /// <summary>Glyphe de l'entrée qui l'a déclenchée (bouton, stick, gâchette), s'il est connu.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActionSource))]
    public partial string? ActionGlyph { get; set; }

    /// <summary>Texte de l'entrée qui l'a déclenchée (« ↑ » d'un stick…).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActionSource))]
    public partial string? ActionSource { get; set; }

    [ObservableProperty]
    public partial GlyphTint ActionTint { get; set; }

    public bool HasAction => ActionLabel is not null;

    public bool HasActionSource => ActionGlyph is not null || ActionSource is not null;

    /// <summary>Commence à suivre la manette : glyphes du style en vigueur, état du moment, dernière action effacée.</summary>
    public void Begin()
    {
        _glyphs = GlyphStyle.From(_settings.Current.Appearance);
        foreach (var button in _buttons)
            button.Show(ActionLabels.Show(InputBinding.Pad(button.Button), _glyphs));
        LeftStick.Glyph = ControllerGlyphs.LeftStick;
        RightStick.Glyph = ControllerGlyphs.RightStick;
        LeftTrigger.Glyph = ActionLabels.Show(InputBinding.Pad(GamepadAxis.LeftTrigger, InputDirection.Positive), _glyphs).Glyph;
        RightTrigger.Glyph = ActionLabels.Show(InputBinding.Pad(GamepadAxis.RightTrigger, InputDirection.Positive), _glyphs).Glyph;
        ShowNoAction();

        if (!_running)
        {
            _running = true;
            _input.GamepadChanged += OnInputChanged;
            _input.ControllersChanged += OnInputChanged;
        }

        Refresh();
    }

    /// <summary>Arrête de suivre la manette (étape quittée).</summary>
    public void End()
    {
        if (!_running)
            return;

        _running = false;
        _input.GamepadChanged -= OnInputChanged;
        _input.ControllersChanged -= OnInputChanged;
    }

    /// <summary>Une action vient d'être déclenchée : son nom, et le glyphe de l'entrée de la manette qui la tient.</summary>
    public void ShowAction(InputActionEvent e)
    {
        ActionLabel = ActionLabels.Of(e.Action);
        BindingText? source = null;
        if (e.Device == InputDevice.Controller
            && _input.Gamepad is { } state
            && _settings.Current.Input.GetControllerBindings().TryGetValue(e.Action, out var bindings))
        {
            var threshold = _settings.Current.Input.AxisThreshold;
            var held = bindings.FirstOrDefault(b => state.Holds(b, threshold));
            if (held is not null)
                source = ActionLabels.Show(held, _glyphs);
        }

        ActionGlyph = source?.Glyph;
        ActionSource = source?.Text;
        ActionTint = source?.Tint ?? GlyphTint.None;
    }

    private void ShowNoAction()
    {
        ActionLabel = null;
        ActionGlyph = null;
        ActionSource = null;
        ActionTint = GlyphTint.None;
    }

    // Thread des entrées : un seul passage en attente sur le thread d'interface à la fois.
    private void OnInputChanged(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref _refreshQueued, 1) == 0)
            UiThread.Run(Refresh);
    }

    private void Refresh()
    {
        Volatile.Write(ref _refreshQueued, 0);
        if (!_running)
            return;

        var state = _input.Gamepad;
        var threshold = _settings.Current.Input.AxisThreshold;
        HasGamepad = state is not null;
        Status = StatusText(state);
        foreach (var button in _buttons)
            button.IsLit = state?.IsPressed(button.Button) == true;
        LeftStick.Apply(state, threshold);
        RightStick.Apply(state, threshold);
        LeftTrigger.Apply(state, threshold);
        RightTrigger.Apply(state, threshold);
    }

    private string StatusText(GamepadState? state) => state is not null
        ? state.Controller.Name
        : (_input.Controllers.Count == 0, _withIndicators) switch
        {
            (true, true) => Tr.Get("win_pad_none"),
            (true, false) => Tr.Get("win_pad_none_short"),
            (false, true) => Tr.Get("win_pad_unrecognised"),
            (false, false) => Tr.Get("win_pad_unrecognised_short"),
        };
}
