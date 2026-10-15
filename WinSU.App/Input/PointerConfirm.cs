using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WinSU.Core.Input;

namespace WinSU.App.Input;

/// <summary>
/// Souris (29.15, D-98, comme l'écran tactile de la Wii U) : un 1er clic choisit un élément de liste (tuile, carte, élément
/// d'un menu), un 2e clic sur ce même élément vaut Confirmer : lancer un jeu, appliquer une carte… Le clic passe par le
/// même chemin que la touche (<see cref="InputRouter"/>) : écran ou fenêtre superposée d'abord, son, appui. Un élément qui
/// avait déjà le focus (choisi aux flèches ou à la manette) se confirme donc dès le 1er clic. Les boutons, eux, agissent
/// au 1er clic, comme avant.
/// </summary>
public static class PointerConfirm
{
    private static InputRouter? _router;

    /// <summary>Élément qui avait déjà le focus quand le bouton gauche a été enfoncé dessus.</summary>
    private static ListBoxItem? _armed;

    public static void Register(InputRouter router)
    {
        if (_router is not null)
            return;

        _router = router;
        // En descente : avant que la liste ne choisisse l'élément et ne lui donne le focus.
        InputElement.PointerPressedEvent.AddClassHandler<ListBoxItem>(OnPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        InputElement.PointerReleasedEvent.AddClassHandler<ListBoxItem>(OnReleased, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>Le clic confirme-t-il ? Bouton gauche relâché sur l'élément qui avait déjà le focus quand il a été enfoncé.</summary>
    public static bool Confirms(bool hadFocusWhenPressed, MouseButton released, bool releasedOverItem) =>
        hadFocusWhenPressed && released == MouseButton.Left && releasedOverItem;

    private static void OnPressed(ListBoxItem item, PointerPressedEventArgs e) =>
        _armed = e.GetCurrentPoint(item).Properties.IsLeftButtonPressed && item.IsFocused ? item : null;

    private static void OnReleased(ListBoxItem item, PointerReleasedEventArgs e)
    {
        var armed = _armed == item;
        _armed = null;
        if (_router is null || !Confirms(armed, e.InitialPressMouseButton, new Rect(item.Bounds.Size).Contains(e.GetPosition(item))))
            return;

        _router.Route(new InputActionEvent(InputAction.Confirm, IsRepeat: false, _router.LastDevice));
    }
}
