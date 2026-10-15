using WinSU.App.Localization;
using WinSU.Infrastructure.Win32;

namespace WinSU.App.Services;

public interface IMediaPicker
{
    /// <summary>Une image (un raccourci vers une image renvoie l'image), ou null si annulé.</summary>
    Task<string?> PickImageAsync(string title, IReadOnlyList<string> extensions);
}

/// <summary>Boîte « Ouvrir » de Windows, pour une image (« Choisir une image »).</summary>
internal sealed class Win32MediaPicker(IWindowControl window) : IMediaPicker
{
    public Task<string?> PickImageAsync(string title, IReadOnlyList<string> extensions) =>
        Task.FromResult(FileOpenDialog.PickFiles(
            window.Handle,
            title,
            [
                (Tr.Get("win_image_pick_filter"), string.Join(';', extensions.Select(e => "*" + e))),
                (Tr.Get("win_games_pick_filter_all"), "*.*"),
            ],
            multiselect: false,
            keepShortcuts: false).FirstOrDefault());
}
