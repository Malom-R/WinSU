using WinSU.App.Navigation;
using WinSU.App.Overlays;
using WinSU.App.Services;
using WinSU.App.ViewModels.Wizard;
using WinSU.Core.Audio;
using WinSU.Core.Input;

namespace WinSU.App.Input;

/// <summary>
/// Point d'entrée unique des actions (clavier et manette) sur le thread d'interface :
/// la fenêtre superposée d'abord, puis l'écran courant (<see cref="IInputHandler"/>), puis le comportement par défaut.
/// Joue aussi les sons de déplacement et de validation (les ouvertures et retours d'écran sont sonorisés par la navigation).
/// Start ouvre le menu d'alimentation (D-18), partout sauf dans l'assistant. Pendant un jeu, rien ne passe (D-182).
/// </summary>
public sealed class InputRouter(
    INavigationService navigation,
    IOverlayService overlays,
    FocusNavigator focus,
    IAudioService audio,
    PowerMenu power,
    ActiveUse use,
    LaunchCoordinator launcher)
{
    /// <summary>
    /// Un jeu lancé depuis WinSU est suivi : aucune entrée n'est acceptée (D-182 : manette et clavier ici, toutes les touches
    /// et la souris dans la fenêtre), jusqu'à la fin du jeu et le retour de WinSU au premier plan.
    /// </summary>
    public bool IsHeld => launcher.IsRunning;

    public InputDevice LastDevice { get; private set; } = InputDevice.Keyboard;

    /// <summary>False tant qu'aucune touche ni aucun bouton n'a été utilisé.</summary>
    public bool HasReceivedInput { get; private set; }

    /// <summary>Déclenché quand on passe du clavier à la manette ou inversement (la barre d'aide change d'icônes).</summary>
    public event EventHandler? LastDeviceChanged;

    public bool Route(InputActionEvent e)
    {
        if (IsHeld)
            return true; // avalée : ni l'écran ni le comportement par défaut ne la voient

        use.NotifyInput(); // réveil après une mise en veille : ce qui suit ActiveUse reprend
        if (e.Device != LastDevice || !HasReceivedInput)
        {
            LastDevice = e.Device;
            HasReceivedInput = true;
            LastDeviceChanged?.Invoke(this, EventArgs.Empty);
        }

        IInputHandler? handler = overlays.Current is { } overlay ? overlay : navigation.Current as IInputHandler;
        if (handler is not null && handler.HandleAction(e))
            return true;

        switch (e.Action)
        {
            case InputAction.Up or InputAction.Down or InputAction.Left or InputAction.Right:
                var moved = focus.Move(ToDirection(e.Action));
                if (moved)
                    audio.Play(UiSound.Navigate);
                return moved;

            case InputAction.Confirm when !e.IsRepeat:
                var (page, overlayBefore) = (navigation.Current, overlays.Current);
                var activated = focus.ActivateFocused();
                // Si l'activation a ouvert un écran ou une fenêtre, leur son a déjà été joué.
                if (activated && navigation.Current == page && overlays.Current == overlayBefore)
                    audio.Play(UiSound.Open);
                return activated;

            case InputAction.Back when !e.IsRepeat:
                return navigation.Current is { IgnoresBack: true } || navigation.GoBack();

            // Grille en pages (style Wii U, M16) : LB/RB tournent la page, quand l'écran ne s'en sert pas lui-même.
            case InputAction.PagePrevious or InputAction.PageNext when !e.IsRepeat:
                var turned = focus.TurnPage(e.Action == InputAction.PageNext ? 1 : -1);
                if (turned)
                    audio.Play(UiSound.Navigate);
                return turned;

            case InputAction.Start when !e.IsRepeat && overlays.Current is null && navigation.Current is not FirstRunWizardViewModel:
                _ = power.ShowAsync();
                return true;

            default:
                return false;
        }
    }

    private static NavigationDirection ToDirection(InputAction action) => action switch
    {
        InputAction.Up => NavigationDirection.Up,
        InputAction.Down => NavigationDirection.Down,
        InputAction.Left => NavigationDirection.Left,
        _ => NavigationDirection.Right,
    };
}
