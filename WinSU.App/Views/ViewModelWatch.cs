using Avalonia.Controls;

namespace WinSU.App.Views;

/// <summary>
/// Une vue suit son ViewModel tant qu'elle est à l'écran (étape 36) : <c>attach</c> quand elle le reçoit ou revient à l'écran,
/// <c>detach</c> quand elle le lâche ou quitte l'écran. Les ViewModels vivent plus longtemps que leurs vues (changement de
/// style à chaud, retour d'un écran ouvert par-dessus) : une vue retirée ne reçoit plus rien.
/// </summary>
internal static class ViewModelWatch
{
    public static void Follow<T>(Control view, Action<T> attach, Action<T> detach)
        where T : class => Follow(view, context => context as T, attach, detach);

    /// <param name="pick">Ce que la vue suit, tiré de son ViewModel (le tableau déroulant d'un écran, par exemple).</param>
    public static void Follow<T>(Control view, Func<object?, T?> pick, Action<T> attach, Action<T> detach)
        where T : class
    {
        T? current = null;

        void Watch(T? next)
        {
            if (ReferenceEquals(current, next))
                return;

            if (current is not null)
                detach(current);
            current = next;
            if (current is not null)
                attach(current);
        }

        view.DataContextChanged += (_, _) => Watch(pick(view.DataContext));
        view.AttachedToVisualTree += (_, _) => Watch(pick(view.DataContext));
        view.DetachedFromVisualTree += (_, _) => Watch(null);
    }
}
