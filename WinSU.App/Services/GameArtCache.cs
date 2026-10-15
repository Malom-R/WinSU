using Avalonia.Media.Imaging;
using WinSU.Core.Catalog;

namespace WinSU.App.Services;

/// <summary>
/// Fond et cadre des tuiles de jeux (étape 34 : dessinés par WinSU, <see cref="ConsoleArtPainter"/>), une seule fois par
/// console et partagés par toutes ses tuiles. Pas de cadre si <paramref name="showBorders"/> le dit (réglage lu à chaque
/// chargement). À vider (<see cref="Clear"/>) en quittant l'écran.
/// </summary>
public sealed class GameArtCache(IImageLoader images, Func<bool> showBorders)
{
    /// <summary>Le fond est atténué : une résolution réduite suffit.</summary>
    private const int BackgroundWidth = 240;

    /// <summary>Le cadre, lui, est net par-dessus les jaquettes : 200 px d'interface à 200 %.</summary>
    private const int BorderWidth = 400;

    private readonly Dictionary<string, Task<(Bitmap? Background, Bitmap? Border)>> _byConsole = new(StringComparer.OrdinalIgnoreCase);

    public Task<(Bitmap? Background, Bitmap? Border)> GetAsync(ConsoleDefinition console)
    {
        if (!_byConsole.TryGetValue(console.Id, out var art))
            _byConsole[console.Id] = art = LoadAsync(console);
        return art;
    }

    public void Clear()
    {
        foreach (var art in _byConsole.Values.Where(t => t.IsCompletedSuccessfully).Select(t => t.Result))
        {
            art.Background?.Dispose();
            art.Border?.Dispose();
        }

        _byConsole.Clear();
    }

    private async Task<(Bitmap?, Bitmap?)> LoadAsync(ConsoleDefinition console)
    {
        var background = images.LoadConsoleArtAsync(console, ConsoleArt.Background, BackgroundWidth, ConsoleTileImages.IsDarkTheme);
        var border = showBorders() ? images.LoadConsoleArtAsync(console, ConsoleArt.Frame, BorderWidth, dark: false) : Task.FromResult<Bitmap?>(null);
        return (await background, await border);
    }
}
