using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using WinSU.App.Controls;
using WinSU.Core.Input;
using NavigationDirection = WinSU.Core.Input.NavigationDirection;

namespace WinSU.App.Input;

/// <summary>
/// Navigation au focus pilotée par la géométrie (<see cref="SpatialNavigation"/>) : Haut/Bas/Gauche/Droite mènent
/// à l'élément focusable le plus proche dans l'écran courant, y compris les tuiles d'une grille (ListBoxItem),
/// que la navigation XY d'Avalonia ne voit pas. Quand une fenêtre superposée est ouverte, seule elle est parcourue.
/// </summary>
public sealed class FocusNavigator
{
    private TopLevel? _topLevel;
    private Control? _pageHost;
    private Control? _overlayHost;

    /// <summary>Fixe la fenêtre, la zone de l'écran courant et celle des fenêtres superposées.</summary>
    public void Attach(TopLevel topLevel, Control pageHost, Control overlayHost)
    {
        _topLevel = topLevel;
        _pageHost = pageHost;
        _overlayHost = overlayHost;
    }

    /// <summary>Zone active : la fenêtre superposée si elle est visible, sinon l'écran courant.</summary>
    private Control? Scope => _overlayHost is { IsEffectivelyVisible: true } ? _overlayHost : _pageHost;

    public bool Move(NavigationDirection direction)
    {
        if (_topLevel is null || Scope is not { } scope)
            return false;

        var candidates = CollectCandidates(scope);
        if (candidates.Count == 0)
            return false;

        if (_topLevel.FocusManager?.GetFocusedElement() is not Control focused || !candidates.Contains(focused))
            return FocusFirst(candidates, scope);

        // Curseur (volume) : Gauche et Droite changent sa valeur ; Haut et Bas en sortent.
        if (focused is Slider slider && direction is NavigationDirection.Left or NavigationDirection.Right)
            return Step(slider, direction == NavigationDirection.Right ? 1 : -1);

        // File qui glisse (étape 30) : ses deux directions mènent au voisin, même en diagonale ; au bout, on y reste.
        if (focused.GetVisualAncestors().OfType<ITrackArea>().FirstOrDefault() is { } track && track.Handles(direction))
            return track.Neighbour(focused, direction) is { } neighbour && Focus(neighbour);

        // Grille en pages (style Wii U, M16) : Gauche et Droite y restent, d'élément en élément, et tournent la page au bord.
        if (PagedAreaOf(focused) is { } area && direction is NavigationDirection.Left or NavigationDirection.Right)
            return area.Neighbour(focused, direction) is { } neighbour && Focus(neighbour);

        // Zone parcourue en lignes (Réglages) : on reste dans la zone, ligne après ligne.
        var rows = focused.GetVisualAncestors().OfType<Control>().FirstOrDefault(RowNavigation.GetIsEnabled);
        var others = candidates.Where(c => c != focused && (rows is null || rows.IsVisualAncestorOf(c))).ToList();
        var boxes = others.Select(c => BoxOf(c, scope)).ToList();
        var index = rows is null
            ? SpatialNavigation.FindBest(BoxOf(focused, scope), boxes, direction)
            : SpatialNavigation.FindInRows(BoxOf(focused, scope), boxes, direction);
        if (index >= 0)
            return Focus(EntryPoint(others[index], focused));

        // Au bord gauche ou droit d'une zone en lignes, on en sort vers l'élément voisin (la liste des sections des
        // Réglages du style Switch, M15) ; en haut et en bas, on y reste.
        if (rows is null || direction is not (NavigationDirection.Left or NavigationDirection.Right))
            return false;

        var outside = candidates.Where(c => c != focused && !rows.IsVisualAncestorOf(c)).ToList();
        var next = SpatialNavigation.FindBest(BoxOf(focused, scope), [.. outside.Select(c => BoxOf(c, scope))], direction);
        return next >= 0 && Focus(EntryPoint(outside[next], focused));
    }

    /// <summary>
    /// LB/RB sur une grille en pages (style Wii U, M16) : la page voisine, le focus à la même place. La grille est celle
    /// qui a le focus, sinon la première de l'écran. False s'il n'y a ni grille en pages ni page de ce côté.
    /// </summary>
    public bool TurnPage(int offset)
    {
        if (_topLevel is null || Scope is not { } scope)
            return false;

        var focused = _topLevel.FocusManager?.GetFocusedElement() as Control;
        var area = focused is null ? null : PagedAreaOf(focused);
        var from = area is null ? null : focused;
        area ??= scope.GetVisualDescendants().OfType<IPagedArea>().FirstOrDefault(a => a is Visual { IsEffectivelyVisible: true });
        return area?.TurnPage(from, offset) is { } target && Focus(target);
    }

    /// <summary>
    /// Le focus est sur un élément à l'intérieur de <paramref name="area"/> (pas sur elle-même) : la vue d'un onglet ou d'une
    /// page l'y a déjà placé (étape 36 : une seule façon de le demander).
    /// </summary>
    public static bool HasFocusWithin(Visual area) =>
        TopLevel.GetTopLevel(area)?.FocusManager?.GetFocusedElement() is Visual focused && area.IsVisualAncestorOf(focused);

    /// <summary>Donne le focus au premier élément (ordre de lecture) d'une zone : nouvel onglet, fenêtre superposée…</summary>
    public static bool FocusFirstIn(Control scope)
    {
        var candidates = CollectCandidates(scope);
        return candidates.Count > 0 && FocusFirst(candidates, scope);
    }

    /// <summary>« Confirmer » sur l'élément qui a le focus ; une tuile, une carte ou un bouton joue l'appui (étape 29).</summary>
    public bool ActivateFocused()
    {
        var focused = _topLevel?.FocusManager?.GetFocusedElement();
        if (focused is Control control)
            PressFeedback.Play(control);

        switch (focused)
        {
            case ToggleButton toggle:
                toggle.IsChecked = !(toggle.IsChecked ?? false);
                return true;
            case Button { Command: { } command } button when command.CanExecute(button.CommandParameter):
                command.Execute(button.CommandParameter);
                return true;
            case Button button:
                button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                return true;
            case ListBoxItem item:
                Select(item);
                if (item.DataContext is IActivatable activatable)
                    activatable.Activate();
                return true;
            default:
                return false;
        }
    }

    /// <summary>Un cran de <see cref="Slider.SmallChange"/> ; false en butée (rien ne bouge, pas de son).</summary>
    private static bool Step(Slider slider, int sign)
    {
        var value = Math.Clamp(slider.Value + (sign * slider.SmallChange), slider.Minimum, slider.Maximum);
        if (value.Equals(slider.Value))
            return false;

        slider.Value = value;
        return true;
    }

    public static bool Focus(Control control)
    {
        if (!control.Focus(NavigationMethod.Directional))
            return false;

        // La boîte d'une tuile contient la tuile grossie et son anneau (TileMetrics) ; la marge de défilement garde aussi
        // sa lueur visible au bord de la zone qui défile. Même marge pour les boutons, soulevés au focus avec un anneau
        // et une lueur (M14 : le haut d'un bouton en tête d'une zone qui défile était coupé). Une zone qui passe sous la
        // barre d'aide (étape 32) ou sous l'en-tête de l'écran (étape 33) défile assez pour que l'élément s'arrête à côté :
        // il n'y est jamais atténué.
        var room = new Rect(control.Bounds.Size).Inflate(TileMetrics.ScrollRoom);
        var (above, below) = Covers.Room(control);
        control.BringIntoView(new Rect(room.X, room.Y - above, room.Width, room.Height + above + below));
        // La sélection suit le focus, sauf dans une liste où l'on choisit par Confirmer (cartes des réglages, D-78)
        if (control is ListBoxItem item && !ChoosesOnConfirm(item))
            Select(item);

        return true;
    }

    /// <summary>L'élément est dans une liste où l'on choisit par Confirmer (<see cref="ConfirmToSelect"/>).</summary>
    private static bool ChoosesOnConfirm(ListBoxItem item) =>
        ItemsControl.ItemsControlFromItemContainer(item) is { } list && ConfirmToSelect.GetIsEnabled(list);

    private static bool FocusFirst(List<Control> candidates, Control scope) =>
        Focus(EntryPoint(ReadingOrder(candidates, scope).First(), from: null));

    /// <summary>Les éléments d'une zone qui peuvent prendre le focus, dans l'ordre de lecture (haut en bas, puis gauche à droite).</summary>
    public static IReadOnlyList<Control> InReadingOrder(Control scope) => [.. ReadingOrder(CollectCandidates(scope), scope)];

    private static IOrderedEnumerable<Control> ReadingOrder(IEnumerable<Control> candidates, Control scope) =>
        candidates.OrderBy(c => Math.Round(BoxOf(c, scope).Y)).ThenBy(c => BoxOf(c, scope).X);

    /// <summary>
    /// En entrant dans une liste depuis l'extérieur, on arrive sur son élément sélectionné plutôt que sur le plus proche :
    /// on retrouve la dernière tuile visitée d'une grille, et l'option en vigueur d'une rangée de cartes (D-78).
    /// </summary>
    private static Control EntryPoint(Control candidate, Control? from)
    {
        if (candidate is not ListBoxItem item || ItemsControl.ItemsControlFromItemContainer(item) is not SelectingItemsControl list)
            return candidate;

        var comesFromSameList = from is ListBoxItem origin && ItemsControl.ItemsControlFromItemContainer(origin) == list;
        if (comesFromSameList || list.SelectedIndex < 0)
            return candidate;

        return list.ContainerFromIndex(list.SelectedIndex) is { IsEffectivelyVisible: true } selected ? selected : candidate;
    }

    private static void Select(ListBoxItem item)
    {
        if (ItemsControl.ItemsControlFromItemContainer(item) is SelectingItemsControl list)
            list.SelectedItem = list.ItemFromContainer(item);
    }

    private static List<Control> CollectCandidates(Control root) =>
        root.GetVisualDescendants()
            .OfType<Control>()
            .Where(c => c.Focusable
                && c.IsEffectivelyVisible
                && c.IsEffectivelyEnabled
                && c.Bounds is { Width: > 0, Height: > 0 }
                && c is not (ItemsControl or ScrollViewer or TopLevel) // conteneurs : on vise leurs éléments
                && PagedAreaOf(c)?.Shows(c) != false) // grille en pages : la page affichée seulement
            .ToList();

    private static IPagedArea? PagedAreaOf(Control control) => control.GetVisualAncestors().OfType<IPagedArea>().FirstOrDefault();

    private static Box BoxOf(Control control, Visual relativeTo)
    {
        var origin = control.TranslatePoint(default, relativeTo) ?? default;
        return new Box(origin.X, origin.Y, control.Bounds.Width, control.Bounds.Height);
    }
}
