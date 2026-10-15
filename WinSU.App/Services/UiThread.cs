using Avalonia;
using Avalonia.Threading;

namespace WinSU.App.Services;

internal static class UiThread
{
    /// <summary>
    /// Exécute tout de suite si l'on est déjà sur le thread d'interface, sinon l'y envoie.
    /// Sans application Avalonia (tests), aucune boucle ne traiterait l'envoi : on exécute directement.
    /// </summary>
    public static void Run(Action action)
    {
        if (Application.Current is null || Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }
}
