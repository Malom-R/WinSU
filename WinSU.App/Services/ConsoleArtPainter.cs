using System.Collections.Concurrent;
using SkiaSharp;
using WinSU.App.Controls;
using WinSU.Core.Appearance;
using WinSU.Core.Catalog;

namespace WinSU.App.Services;

/// <summary>Ce que <see cref="ConsoleArtPainter"/> dessine pour une console.</summary>
public enum ConsoleArt
{
    /// <summary>
    /// Tuile de la console : dégradé de sa couleur, son icône blanche au milieu ; coins arrondis au rayon de la face (la
    /// tuile est montrée réduite dans la face, ses coins ne sont pas rognés par elle).
    /// </summary>
    Tile,

    /// <summary>Fond des jeux sans image (montré atténué sous leur nom) : le dégradé et l'icône en filigrane, rien au milieu.</summary>
    Background,

    /// <summary>
    /// Cadre posé sur les jaquettes (réglage « Cadre ») : liseré de la couleur de la console au rayon de la face des tuiles,
    /// pastille avec l'icône de sa manette dans le coin haut gauche (centrée sur sa partie visible) ; le milieu est transparent.
    /// </summary>
    Frame,
}

/// <summary>
/// Tuiles et cadres des consoles, dessinés par WinSU (étape 34, D-149, D-150 : plus d'images de l'APK d'iiSU) à partir de la
/// couleur et de l'icône du catalogue (icônes du thème « Automatic » de RetroArch, CC BY 4.0 : <c>Assets/Icons/CREDITS.txt</c>).
/// Un seul code pour tous les styles ; les vues reçoivent des images comme avant (<see cref="IImageLoader.LoadConsoleArtAsync"/>).
/// </summary>
public static class ConsoleArtPainter
{
    /// <summary>Rayon de la face des tuiles, en part de son côté (18 sur 152) : le liseré du cadre le suit.</summary>
    public static double FaceRadiusRatio => TileMetrics.FaceRadius / (TileMetrics.GameSize - (2 * TileMetrics.FaceInset));

    /// <summary>Épaisseur du liseré du cadre, en part du côté (≈ 4,6 sur une face de 152).</summary>
    public const double BandRatio = 0.03;

    /// <summary>Côté de la pastille du cadre, en part du côté.</summary>
    public const double BadgeRatio = 0.24;

    /// <summary>Taille de l'icône de la tuile, en part du côté (l'icône de RetroArch remplit presque son carré).</summary>
    public const double IconRatio = 0.58;

    /// <summary>Côté de l'icône de la manette dans la pastille, en part du côté de la pastille (partie visible).</summary>
    public const double PadRatio = 0.66;

    /// <summary>
    /// Icônes décodées, une fois par fichier, avec leur partie visible (<see cref="OpaqueArea"/> : les marges transparentes
    /// des icônes ne sont pas égales) ; partagées par toutes les tuiles, lecture seule, sûres entre fils.
    /// </summary>
    private static readonly ConcurrentDictionary<string, Icon?> Icons = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Une icône décodée et le rectangle de ses pixels visibles.</summary>
    private sealed record Icon(SKImage Image, SKRect Visible);

    /// <summary>Dessine <paramref name="art"/> pour <paramref name="console"/>, carré de <paramref name="size"/> pixels.</summary>
    public static SKBitmap Paint(ConsoleDefinition console, ConsoleArt art, int size, bool dark)
    {
        var bitmap = new SKBitmap(new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        var icon = IconOf(console.IconPath);
        switch (art)
        {
            case ConsoleArt.Tile:
                using (var corners = new SKRoundRect(new SKRect(0, 0, size, size), (float)(size * FaceRadiusRatio)))
                    canvas.ClipRoundRect(corners, antialias: true);
                PaintBackdrop(canvas, console.Color, size, dark, icon, watermark: 0.08);
                PaintIcon(canvas, icon, size / 2f, size / 2f, (float)(size * IconRatio), shadow: true);
                break;
            case ConsoleArt.Background:
                PaintBackdrop(canvas, console.Color, size, dark, icon, watermark: 0.14);
                break;
            case ConsoleArt.Frame:
                PaintFrame(canvas, console.Color, size, IconOf(console.PadIconPath) ?? icon);
                break;
        }

        canvas.Flush();
        return bitmap;
    }

    /// <summary>Le dégradé de la couleur (du haut à gauche, plus clair, au bas à droite), un reflet, l'icône en filigrane.</summary>
    private static void PaintBackdrop(SKCanvas canvas, ThemeColor color, int size, bool dark, Icon? icon, double watermark)
    {
        var stops = dark
            ? new[] { color.Lighten(0.08), color.Darken(0.12), color.Darken(0.38) }
            : new[] { color.Lighten(0.22), color, color.Darken(0.18) };
        using (var paint = new SKPaint { IsAntialias = true })
        {
            paint.Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(size, size),
                [.. stops.Select(ToSk)], [0f, 0.5f, 1f], SKShaderTileMode.Clamp);
            canvas.DrawRect(0, 0, size, size, paint);

            // Reflet doux en haut à gauche, comme le verre des tuiles
            paint.Shader = SKShader.CreateRadialGradient(new SKPoint(size * 0.15f, size * 0.1f), size * 0.9f,
                [new SKColor(255, 255, 255, (byte)(dark ? 30 : 52)), SKColors.Transparent], [0f, 1f], SKShaderTileMode.Clamp);
            canvas.DrawRect(0, 0, size, size, paint);
        }

        if (icon is null)
            return;

        // Filigrane : l'icône agrandie, inclinée, dans le coin bas droit
        canvas.Save();
        canvas.RotateDegrees(-14, size * 0.78f, size * 0.8f);
        PaintIcon(canvas, icon, size * 0.78f, size * 0.8f, size * 0.95f, shadow: false, alpha: (byte)(255 * watermark));
        canvas.Restore();
    }

    /// <summary>
    /// Le liseré au rayon de la face et la pastille du coin haut gauche, de la couleur de la console ; la manette au centre de
    /// la pastille (sa partie visible centrée, demande de l'utilisateur).
    /// </summary>
    private static void PaintFrame(SKCanvas canvas, ThemeColor color, int size, Icon? pad)
    {
        var radius = (float)(size * FaceRadiusRatio);
        var band = (float)(size * BandRatio);
        using var paint = new SKPaint { IsAntialias = true };
        paint.Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(size, size),
            [ToSk(color.Lighten(0.25)), ToSk(color), ToSk(color.Darken(0.22))], [0f, 0.5f, 1f], SKShaderTileMode.Clamp);

        using (var path = new SKPath { FillType = SKPathFillType.EvenOdd })
        {
            path.AddRoundRect(new SKRoundRect(new SKRect(0, 0, size, size), radius));
            path.AddRoundRect(new SKRoundRect(new SKRect(band, band, size - band, size - band), Math.Max(0, radius - band)));
            canvas.DrawPath(path, paint);
        }

        // Pastille : son coin extérieur suit celui de la face, les autres sont plus serrés
        var side = (float)(size * BadgeRatio);
        var inner = size * 0.07f;
        using var badge = new SKRoundRect();
        badge.SetRectRadii(new SKRect(0, 0, side, side),
            [new SKPoint(radius, radius), new SKPoint(inner, inner), new SKPoint(inner, inner), new SKPoint(inner, inner)]);
        canvas.DrawRoundRect(badge, paint);
        if (pad is null)
            return;

        using var white = new SKPaint { IsAntialias = true, Color = SKColors.White };
        canvas.DrawImage(pad.Image, pad.Visible, FitCentered(pad.Visible, side / 2, side / 2, (float)(side * PadRatio)),
            new SKSamplingOptions(SKCubicResampler.Mitchell), white);
    }

    /// <summary>
    /// Place d'une image de taille <paramref name="source"/> réduite pour tenir dans un carré de <paramref name="side"/>
    /// centré sur (<paramref name="x"/>, <paramref name="y"/>), ses proportions gardées.
    /// </summary>
    public static SKRect FitCentered(SKRect source, float x, float y, float side)
    {
        var scale = side / Math.Max(source.Width, source.Height);
        var (width, height) = (source.Width * scale, source.Height * scale);
        return new SKRect(x - (width / 2), y - (height / 2), x + (width / 2), y + (height / 2));
    }

    /// <summary>L'icône (blanche), centrée sur (<paramref name="x"/>, <paramref name="y"/>), dans un carré de <paramref name="side"/>.</summary>
    private static void PaintIcon(SKCanvas canvas, Icon? icon, float x, float y, float side, bool shadow, byte alpha = 255)
    {
        if (icon is null)
            return;

        using var paint = new SKPaint { IsAntialias = true, Color = new SKColor(255, 255, 255, alpha) };
        if (shadow)
            paint.ImageFilter = SKImageFilter.CreateDropShadow(0, side * 0.025f, side * 0.03f, side * 0.03f, new SKColor(0, 0, 0, 72));
        var half = side / 2;
        canvas.DrawImage(icon.Image, new SKRect(x - half, y - half, x + half, y + half), new SKSamplingOptions(SKCubicResampler.Mitchell), paint);
    }

    private static Icon? IconOf(string? path) => path is null ? null : Icons.GetOrAdd(path, LoadIcon);

    private static Icon? LoadIcon(string path)
    {
        try
        {
            using var decoded = SKBitmap.Decode(path);
            if (decoded is null)
                return null;

            using var bgra = decoded.ColorType == SKColorType.Bgra8888 ? null : decoded.Copy(SKColorType.Bgra8888);
            var bitmap = bgra ?? decoded;
            var visible = OpaqueArea.Find(bitmap.GetPixelSpan(), bitmap.Width, bitmap.Height, bitmap.RowBytes) is { } area
                ? SKRect.Create(area.X, area.Y, area.Width, area.Height)
                : SKRect.Create(bitmap.Width, bitmap.Height);
            return new Icon(SKImage.FromBitmap(bitmap), visible);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static SKColor ToSk(ThemeColor color) => new(color.R, color.G, color.B, color.A);
}
