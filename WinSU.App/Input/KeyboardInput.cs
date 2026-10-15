using System.Collections.Frozen;
using Avalonia.Input;
using WinSU.Core.Input;
using WinSU.Core.Settings;

namespace WinSU.App.Input;

/// <summary>
/// Clavier (M11, D-27) : table « touche → actions » tirée des réglages (touches par défaut, ou choisies dans l'écran
/// « Boutons »), et capture d'une touche pour cet écran. F11 (plein écran) reste fixe : la fenêtre la traite avant.
/// Remplace les touches fixes de M2 (<c>KeyboardMap</c>), qui sont devenues les touches par défaut.
/// </summary>
public sealed class KeyboardInput
{
    private readonly HashSet<Key> _held = [];
    private readonly HashSet<PhysicalKey> _heldPhysical = [];
    private readonly Lock _captureLock = new();
    private FrozenDictionary<Key, InputAction[]> _map;
    private Capture? _capture;

    public KeyboardInput(ISettingsStore settings)
    {
        _map = Build(settings.Current.Input);
        settings.Changed += (_, e) =>
        {
            if (e.Previous.Input != e.Current.Input)
                _map = Build(e.Current.Input);
        };
    }

    /// <summary>Une capture attend sa touche.</summary>
    public bool IsCapturing => _capture is not null;

    /// <summary>
    /// Une touche, par sa place physique, vient d'être enfoncée (vrai) ou relâchée (faux) : le clavier dessiné de l'écran
    /// « Boutons » l'allume (étape 32, D-130). Les répétitions d'une touche maintenue ne sont pas signalées.
    /// </summary>
    public event Action<PhysicalKey, bool>? PhysicalKeyChanged;

    /// <summary>Appelé par la fenêtre à chaque touche, enfoncée ou relâchée, par sa place physique.</summary>
    public void OnPhysicalKey(PhysicalKey key, bool down)
    {
        if (key == PhysicalKey.None || (down ? !_heldPhysical.Add(key) : !_heldPhysical.Remove(key)))
            return;

        PhysicalKeyChanged?.Invoke(key, down);
    }

    /// <summary>Actions de la touche (aucune si elle n'est liée à rien).</summary>
    public IReadOnlyList<InputAction> Map(Key key) => _map.TryGetValue(key, out var actions) ? actions : [];

    /// <summary>Touche enregistrée dans les réglages (nom de l'énumération <see cref="Key"/>), ou null si le nom est inconnu.</summary>
    public static Key? Parse(string name) =>
        Enum.TryParse<Key>(name, ignoreCase: true, out var key) && key != Key.None ? key : null;

    /// <summary>
    /// Nom enregistré d'une touche, toujours le même : l'énumération <see cref="Key"/> a plusieurs noms pour certaines
    /// touches (Enter et Return, PageUp et Prior…) et <c>ToString</c> n'en donne qu'un, au hasard de la déclaration.
    /// </summary>
    public static string NameOf(Key key) => key switch
    {
        Key.Enter => "Enter",
        Key.PageUp => "PageUp",
        Key.PageDown => "PageDown",
        Key.CapsLock => "CapsLock",
        _ => key.ToString(),
    };

    /// <summary>
    /// Table de touches aux noms canoniques (<see cref="NameOf"/>), pour que deux noms d'une même touche soient
    /// reconnus comme une seule entrée ; un nom inconnu est gardé tel quel.
    /// </summary>
    public static IReadOnlyDictionary<InputAction, IReadOnlyList<string>> Normalize(IReadOnlyDictionary<InputAction, IReadOnlyList<string>> keys) =>
        keys.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)[.. pair.Value.Select(name => Parse(name) is { } key ? NameOf(key) : name).Distinct()]);

    /// <summary>
    /// Attend la prochaine touche, sans modificateur, pour l'écran « Boutons ». Échap annule (renvoie null). Les touches
    /// déjà enfoncées au moment de l'appel (celle qui a lancé la capture) sont ignorées jusqu'à leur relâchement.
    /// </summary>
    public Task<Key?> CaptureAsync(CancellationToken cancellationToken = default)
    {
        var capture = new Capture([.. _held]);
        lock (_captureLock)
        {
            _capture?.Completion.TrySetCanceled(CancellationToken.None);
            _capture = capture;
        }

        // L'annulation peut venir d'un autre fil (délai dépassé).
        cancellationToken.Register(() =>
        {
            lock (_captureLock)
            {
                if (_capture == capture)
                    _capture = null;
            }

            capture.Completion.TrySetCanceled(cancellationToken);
        });
        return capture.Completion.Task;
    }

    /// <summary>
    /// Appelé par la fenêtre à chaque touche enfoncée, avant la navigation. Renvoie true si la touche est prise par une
    /// capture (elle ne doit alors rien faire d'autre).
    /// </summary>
    public bool OnKeyDown(Key key, KeyModifiers modifiers)
    {
        var repeat = !_held.Add(key);
        Capture? capture;
        lock (_captureLock)
        {
            capture = _capture;
            if (capture is null)
                return false;

            // Modificateurs seuls, combinaisons, touche maintenue depuis le début : on attend la suivante.
            if (IsModifier(key) || modifiers != KeyModifiers.None || repeat || capture.Held.Contains(key))
                return true;

            _capture = null;
        }

        capture.Completion.TrySetResult(key == Key.Escape ? null : key);
        return true;
    }

    /// <summary>Appelé par la fenêtre à chaque touche relâchée.</summary>
    public void OnKeyUp(Key key)
    {
        _held.Remove(key);
        lock (_captureLock)
            _capture?.Held.Remove(key);
    }

    /// <summary>La fenêtre a perdu le premier plan : ses touches relâchées ailleurs ne lui parviendront pas.</summary>
    public void ReleaseAll()
    {
        _held.Clear();
        foreach (var key in _heldPhysical.ToList())
            OnPhysicalKey(key, down: false);
        lock (_captureLock)
            _capture?.Held.Clear();
    }

    private static bool IsModifier(Key key) =>
        key is Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin or Key.System or Key.CapsLock or Key.NumLock;

    private static FrozenDictionary<Key, InputAction[]> Build(InputSettings input) =>
        input.GetKeys()
            .SelectMany(pair => pair.Value.Select(name => (Key: Parse(name), Action: pair.Key)))
            .Where(entry => entry.Key is not null && entry.Key != Key.F11)
            .GroupBy(entry => entry.Key!.Value)
            .ToFrozenDictionary(group => group.Key, group => group.Select(entry => entry.Action).Distinct().ToArray());

    private sealed class Capture(HashSet<Key> held)
    {
        public HashSet<Key> Held { get; } = held;
        public TaskCompletionSource<Key?> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
