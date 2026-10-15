using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using WinSU.Core.Catalog;
using WinSU.Infrastructure.Win32;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace WinSU.App.Services;

public interface IImageLoader
{
    /// <summary>
    /// Décode une image hors du thread d'interface, directement à la largeur voulue (une tuile 512 px
    /// affichée en 176 px n'occupe ainsi que ~0,1 Mo). Renvoie null si le chemin est null ou l'image illisible.
    /// </summary>
    Task<Bitmap?> LoadAsync(string? path, int decodeWidth, CancellationToken cancellationToken = default);

    /// <summary>
    /// Image destinée à remplir un carré de <paramref name="size"/> pixels (jaquette, capture) : décodée pour que son
    /// petit côté fasse <paramref name="size"/> (sans agrandir une petite image). Null si le fichier est illisible.
    /// </summary>
    Task<Bitmap?> LoadToFillAsync(string path, int size, CancellationToken cancellationToken = default);

    /// <summary>
    /// Grande image large (bannière héro) : décodée à <paramref name="maxWidth"/> pixels de large au plus, sans agrandir
    /// une image plus petite. Null si le fichier est illisible.
    /// </summary>
    Task<Bitmap?> LoadToWidthAsync(string path, int maxWidth, CancellationToken cancellationToken = default);

    /// <summary>
    /// Image à fond transparent (logo du jeu, étape 33) : ses marges transparentes retirées (<see cref="OpaqueArea"/>), puis
    /// réduite à <paramref name="maxWidth"/> pixels de large au plus, sans agrandir. Null si le fichier est illisible.
    /// </summary>
    Task<Bitmap?> LoadTrimmedToWidthAsync(string path, int maxWidth, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tuile, fond des jeux ou cadre d'une console (étape 34 : dessinés par WinSU, <see cref="ConsoleArtPainter"/>), carré de
    /// <paramref name="size"/> pixels, variante sombre si <paramref name="dark"/>. Null si le dessin échoue.
    /// </summary>
    Task<Bitmap?> LoadConsoleArtAsync(ConsoleDefinition console, ConsoleArt art, int size, bool dark, CancellationToken cancellationToken = default);

    /// <summary>Le fichier est une image dans un format reconnu (en-tête lu, sans décoder l'image).</summary>
    Task<bool> IsImageAsync(string path);
}

internal sealed class ImageLoader(ILogger<ImageLoader> logger) : IImageLoader
{
    public Task<Bitmap?> LoadAsync(string? path, int decodeWidth, CancellationToken cancellationToken = default)
    {
        if (path is null)
            return Task.FromResult<Bitmap?>(null);

        return Task.Run(() =>
        {
            try
            {
                using var stream = File.OpenRead(path);
                return (Bitmap?)Bitmap.DecodeToWidth(stream, decodeWidth, BitmapInterpolationMode.HighQuality);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Image illisible : {Path}", path);
                return null;
            }
        }, cancellationToken);
    }

    public Task<Bitmap?> LoadToFillAsync(string path, int size, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            try
            {
                // Taille lue dans l'en-tête, sans décoder l'image.
                int width, height;
                using (var codec = SKCodec.Create(path))
                {
                    if (codec is null)
                        throw new InvalidDataException("Format d'image non reconnu.");
                    (width, height) = (codec.Info.Width, codec.Info.Height);
                }

                using var stream = File.OpenRead(path);
                return width >= height
                    ? (Bitmap?)Bitmap.DecodeToHeight(stream, Math.Min(size, height), BitmapInterpolationMode.HighQuality)
                    : Bitmap.DecodeToWidth(stream, Math.Min(size, width), BitmapInterpolationMode.HighQuality);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Image illisible : {Path}", path);
                return null;
            }
        }, cancellationToken);

    public Task<Bitmap?> LoadToWidthAsync(string path, int maxWidth, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            try
            {
                int width;
                using (var codec = SKCodec.Create(path))
                {
                    if (codec is null)
                        throw new InvalidDataException("Format d'image non reconnu.");
                    width = codec.Info.Width;
                }

                using var stream = File.OpenRead(path);
                return (Bitmap?)Bitmap.DecodeToWidth(stream, Math.Min(maxWidth, width), BitmapInterpolationMode.HighQuality);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Image illisible : {Path}", path);
                return null;
            }
        }, cancellationToken);

    public Task<Bitmap?> LoadTrimmedToWidthAsync(string path, int maxWidth, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            try
            {
                using var decoded = SKBitmap.Decode(path) ?? throw new InvalidDataException("Format d'image non reconnu.");
                using var converted = decoded.ColorType == SKColorType.Bgra8888 ? null : decoded.Copy(SKColorType.Bgra8888);
                var source = converted ?? decoded;
                var (x, y, width, height) = OpaqueArea.Find(source.GetPixelSpan(), source.Width, source.Height, source.RowBytes)
                    ?? (0, 0, source.Width, source.Height);

                using var visible = new SKBitmap();
                if (!source.ExtractSubset(visible, new SKRectI(x, y, x + width, y + height)))
                    throw new InvalidDataException("Partie visible illisible.");

                var targetWidth = Math.Min(maxWidth, width);
                var targetHeight = Math.Max(1, (int)Math.Round(height * (double)targetWidth / width));
                using var scaled = targetWidth == width
                    ? null
                    : visible.Resize(new SKImageInfo(targetWidth, targetHeight, SKColorType.Bgra8888, source.AlphaType), new SKSamplingOptions(SKCubicResampler.Mitchell));
                return (Bitmap?)ToBitmap(scaled ?? visible);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Image illisible : {Path}", path);
                return null;
            }
        }, cancellationToken);

    public Task<Bitmap?> LoadConsoleArtAsync(ConsoleDefinition console, ConsoleArt art, int size, bool dark, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            try
            {
                using var painted = ConsoleArtPainter.Paint(console, art, size, dark);
                return (Bitmap?)ToBitmap(painted);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Dessin de la console {Id} impossible ({Art})", console.Id, art);
                return null;
            }
        }, cancellationToken);

    /// <summary>Bitmap d'Avalonia d'un bitmap de Skia BGRA : le constructeur copie les pixels, celui de Skia peut être libéré aussitôt.</summary>
    private static Bitmap ToBitmap(SKBitmap source) =>
        new(PixelFormat.Bgra8888, ToAlphaFormat(source.AlphaType), source.GetPixels(),
            new PixelSize(source.Width, source.Height), new Vector(96, 96), source.RowBytes);

    private static AlphaFormat ToAlphaFormat(SKAlphaType alpha) => alpha switch
    {
        SKAlphaType.Unpremul => AlphaFormat.Unpremul,
        SKAlphaType.Opaque => AlphaFormat.Opaque,
        _ => AlphaFormat.Premul,
    };

    public Task<bool> IsImageAsync(string path) =>
        Task.Run(() =>
        {
            try
            {
                using var codec = SKCodec.Create(path);
                return codec is not null;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Image illisible : {Path}", path);
                return false;
            }
        });
}
