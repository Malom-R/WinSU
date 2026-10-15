namespace WinSU.Core.Input;

public enum InputDevice
{
    Keyboard,
    Controller,
}

/// <summary>Une action déclenchée. <see cref="IsRepeat"/> : répétition automatique d'une direction maintenue.</summary>
public readonly record struct InputActionEvent(InputAction Action, bool IsRepeat, InputDevice Device);

/// <param name="Mapping">Correspondance de SDL d'une manette reconnue (<see cref="GamepadMapping"/>), sinon null.</param>
public sealed record ControllerInfo(int InstanceId, string Name, string Guid, bool IsGamepad, string? Mapping = null)
{
    /// <summary>La correspondance de la première manette reconnue branchée (affichage des liaisons brutes, DF-4).</summary>
    public static string? MappingOf(IEnumerable<ControllerInfo> controllers) => controllers.FirstOrDefault(c => c.IsGamepad)?.Mapping;
}

/// <summary>Entrée capturée (assistant de mapping, écran « Boutons »).</summary>
public sealed record CapturedInput(ControllerInfo Controller, InputBinding Binding);

/// <summary>
/// Manettes (SDL) traduites en <see cref="InputAction"/> selon les liaisons des réglages
/// (<see cref="Settings.InputSettings.GetControllerBindings"/> : disposition standard ou liaisons enregistrées).
/// Le clavier est géré par l'interface.
/// </summary>
/// <remarks>Les événements sont déclenchés sur le thread des entrées : l'interface doit les renvoyer sur son thread.</remarks>
public interface IInputService
{
    /// <summary>False si SDL n'a pas pu démarrer : seul le clavier fonctionne alors.</summary>
    bool IsAvailable { get; }

    IReadOnlyList<ControllerInfo> Controllers { get; }

    event EventHandler<InputActionEvent>? ActionTriggered;

    event EventHandler? ControllersChanged;

    /// <summary>
    /// État de la manette reconnue utilisée en dernier (à défaut, la première branchée) ; null s'il n'y en a aucune.
    /// Sert à l'aperçu de la manette de l'assistant.
    /// </summary>
    GamepadState? Gamepad { get; }

    /// <summary><see cref="Gamepad"/> a changé : au plus une fois par tour de la boucle des entrées.</summary>
    event EventHandler? GamepadChanged;

    /// <summary>
    /// Attend la prochaine entrée d'une manette : bouton ou axe par position pour une manette reconnue, entrée brute
    /// (bouton, axe ou croix) sinon. Les entrées déjà enfoncées au moment de l'appel sont ignorées jusqu'à leur
    /// relâchement. Aucune action n'est émise pendant une capture.
    /// </summary>
    Task<CapturedInput> CaptureAsync(CancellationToken cancellationToken = default);
}
