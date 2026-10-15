using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace WinSU.App.Input;

/// <summary>
/// Garde le focus dans une liste dont les éléments sont recréés (fin de partie, jeu retiré…) : la tuile qui avait
/// le focus disparaît, et sans cela le premier appui après le retour dans WinSU ne ferait rien.
/// Le focus revient sur l'élément sélectionné, seulement s'il était dans la liste et qu'il n'est allé nulle part ailleurs.
/// </summary>
public static class ListFocus
{
    /// <summary>L'élément choisi de la liste à l'écran (le premier s'il n'y en a pas) ; null s'il n'y est pas encore.</summary>
    public static Control? SelectedContainer(ListBox list) => list.ContainerFromIndex(Math.Max(list.SelectedIndex, 0));

    /// <summary>Le focus à l'élément choisi de la liste (à l'ouverture d'un écran) ; false s'il n'est pas encore à l'écran.</summary>
    public static bool FocusSelected(ListBox list) => SelectedContainer(list) is { } container && FocusNavigator.Focus(container);

    public static void KeepOnReset(ListBox list)
    {
        var hasFocus = false;
        var lastIndex = -1;
        var restorePending = false;

        list.GotFocus += (_, e) =>
        {
            if (e.Source is Visual source && source.FindAncestorOfType<ListBoxItem>(includeSelf: true) is { } item
                && list.IndexFromContainer(item) is >= 0 and var index)
            {
                hasFocus = true;
                lastIndex = index;
            }
        };

        // Seul un départ du focus vers un autre élément compte (fenêtre superposée, autre liste). Fenêtre désactivée
        // (jeu lancé, Alt+Tab) : le focus reviendra ici. Un élément retiré de l'écran, lui, ne prévient plus la liste.
        list.LostFocus += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if (TopLevel.GetTopLevel(list) is not WindowBase { IsActive: true } window)
                return;

            if (window.FocusManager?.GetFocusedElement() is Visual focused && !list.IsVisualAncestorOf(focused))
                hasFocus = false;
        });

        list.Items.CollectionChanged += (_, _) =>
        {
            if (!hasFocus || restorePending)
                return;

            restorePending = true;
            Dispatcher.UIThread.Post(() =>
            {
                restorePending = false;
                Restore(list, lastIndex);
            }, DispatcherPriority.Loaded);
        };
    }

    private static void Restore(ListBox list, int lastIndex)
    {
        if (TopLevel.GetTopLevel(list) is not { } topLevel)
            return; // écran quitté

        var focused = topLevel.FocusManager?.GetFocusedElement() as Visual;
        var lost = focused is null || TopLevel.GetTopLevel(focused) is null || focused == list || focused.IsVisualAncestorOf(list);
        if (!lost)
            return; // fenêtre superposée, autre élément : on n'y touche pas

        var index = list.SelectedIndex >= 0 ? list.SelectedIndex : Math.Min(lastIndex, list.ItemCount - 1);
        if (index >= 0 && list.ContainerFromIndex(index) is { } container)
            FocusNavigator.Focus(container);
    }
}
