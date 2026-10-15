namespace WinSU.App.Services;

/// <summary>Actions sur l'application elle-même, accessibles depuis les ViewModels.</summary>
public interface IShell
{
    void Quit();

    /// <summary>Ouvre une page web dans le navigateur de l'utilisateur (https seulement).</summary>
    void OpenInBrowser(Uri address);
}
