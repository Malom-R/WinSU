using Avalonia;
using Avalonia.Styling;
using WinSU.App.ViewModels;

namespace WinSU.App.Services;

/// <summary>
/// Dessine les tuiles d'une série de consoles (variante claire ou sombre selon le thème affiché ; étape 34 : dessinées par
/// WinSU, <see cref="ConsoleArtPainter"/>).
/// </summary>
public static class ConsoleTileImages
{
    private const int MaxConcurrentDecodes = 4;

    public static bool IsDarkTheme => Application.Current?.ActualThemeVariant == ThemeVariant.Dark;

    public static async Task LoadAsync(IEnumerable<ConsoleTileViewModel> tiles, IImageLoader images, int decodeWidth, CancellationToken cancellationToken)
    {
        var dark = IsDarkTheme;
        using var gate = new SemaphoreSlim(MaxConcurrentDecodes);

        async Task LoadTileAsync(ConsoleTileViewModel tile)
        {
            if (tile.Image is not null)
                return;

            await gate.WaitAsync(cancellationToken);
            try
            {
                var image = await images.LoadConsoleArtAsync(tile.Console, ConsoleArt.Tile, decodeWidth, dark, cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                    image?.Dispose();
                else
                    tile.Image = image; // continuation sur le thread d'interface (contexte capturé)
            }
            finally
            {
                gate.Release();
            }
        }

        try
        {
            await Task.WhenAll(tiles.ToList().Select(LoadTileAsync));
        }
        catch (OperationCanceledException)
        {
            // Écran quitté pendant le chargement.
        }
    }
}
