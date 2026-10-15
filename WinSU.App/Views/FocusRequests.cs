using Avalonia.Controls;
using Avalonia.Threading;
using WinSU.App.Input;
using WinSU.App.ViewModels;

namespace WinSU.App.Views;

/// <summary>
/// « Donner le focus à l'élément demandé », écrit une fois (étape 36, C-2) : la vue suit les demandes de son ViewModel
/// (<see cref="IFocusRequester"/> : liste reconstruite après une action, retour d'un écran, section refermée…) ; une fois la
/// mise en page faite, l'élément qui le représente à l'écran prend le focus (<see cref="FocusNavigator.Focus"/>). Rien si
/// la vue a quitté l'écran ou si l'élément n'y est pas.
/// </summary>
public static class FocusRequests
{
    /// <summary>L'élément demandé est un élément de <paramref name="list"/>.</summary>
    public static void Follow(Control view, ListBox list) => Follow(view, item => list.ContainerFromItem(item));

    /// <param name="target">L'élément à l'écran qui prend le focus pour l'élément demandé (null : aucun).</param>
    public static void Follow(Control view, Func<object, Control?> target) =>
        Follow(view, context => context as IFocusRequester, target);

    /// <param name="requester">Qui demande, tiré du ViewModel de la vue (son tableau déroulant, par exemple).</param>
    /// <param name="target">L'élément à l'écran qui prend le focus pour l'élément demandé (null : aucun).</param>
    public static void Follow(Control view, Func<object?, IFocusRequester?> requester, Func<object, Control?> target)
    {
        void OnRequested(object? sender, object item) => Dispatcher.UIThread.Post(() =>
        {
            if (TopLevel.GetTopLevel(view) is not null && target(item) is { } control)
                FocusNavigator.Focus(control);
        }, DispatcherPriority.Loaded);

        ViewModelWatch.Follow(view, requester, r => r.FocusRequested += OnRequested, r => r.FocusRequested -= OnRequested);
    }
}
