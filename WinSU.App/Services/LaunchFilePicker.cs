using WinSU.App.Localization;
using WinSU.Infrastructure.Win32;

namespace WinSU.App.Services;

public interface ILaunchFilePicker
{
    /// <summary>
    /// Choix de raccourcis (.lnk, et .url des jeux Steam : étape 35, D-176) ou de programmes (.exe, .bat, .cmd), plusieurs à
    /// la fois. Vide si annulé.
    /// </summary>
    Task<IReadOnlyList<string>> PickAsync(string title);
}

/// <summary>Boîte « Ouvrir » de Windows qui renvoie les raccourcis eux-mêmes, sans les déréférencer (D-7).</summary>
internal sealed class Win32LaunchFilePicker(IWindowControl window) : ILaunchFilePicker
{
    public Task<IReadOnlyList<string>> PickAsync(string title) =>
        Task.FromResult(FileOpenDialog.PickFiles(
            window.Handle,
            title,
            [
                (Tr.Get("win_games_pick_filter"), "*.lnk;*.url;*.exe;*.bat;*.cmd"),
                (Tr.Get("win_games_pick_filter_all"), "*.*"),
            ]));
}
