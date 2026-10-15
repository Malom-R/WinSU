using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WinSU.App.Controls;

/// <summary>
/// Logo « WinSU » (étape 34, D-172 : mot-symbole au trait rond, dessiné par WinSU), embarqué dans l'exe
/// (<c>Brand/winsu.svg</c>, produit par <c>tools/dev/winsu_brand.cs</c>) et peint avec <see cref="Fill"/> (la couleur
/// du texte), à la hauteur demandée.
/// </summary>
public sealed class BrandLogo : Control
{
    /// <summary>Nom de la ressource du mot-symbole dans l'exe (voir <c>WinSU.App.csproj</c>).</summary>
    public const string ResourceName = "WinSU.Brand.winsu.svg";

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<BrandLogo, IBrush?>(nameof(Fill));

    private static readonly Lazy<LoadedLogo?> Logo = new(Load);

    static BrandLogo() => AffectsRender<BrandLogo>(FillProperty);

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Logo.Value is not { } logo)
            return default;

        var height = double.IsFinite(availableSize.Height) ? availableSize.Height : logo.Frame.Height;
        return new Size(height * logo.Frame.Width / logo.Frame.Height, height);
    }

    public override void Render(DrawingContext context)
    {
        if (Logo.Value is not { } logo || Fill is not { } fill || Bounds.Height <= 0)
            return;

        var scale = Bounds.Height / logo.Frame.Height;
        var transform = Matrix.CreateTranslation(-logo.Frame.MinX, -logo.Frame.MinY) * Matrix.CreateScale(scale, scale);
        using (context.PushTransform(transform))
        {
            foreach (var geometry in logo.Geometries)
                context.DrawGeometry(fill, null, geometry);
        }
    }

    /// <summary>Le document SVG du mot-symbole, tel qu'embarqué dans l'exe (null si la ressource manque).</summary>
    internal static string? ReadDocument()
    {
        using var stream = typeof(BrandLogo).Assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
            return null;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static LoadedLogo? Load()
    {
        try
        {
            if (ReadDocument() is not { } svg || SvgPathReader.Read(svg) is not { } frame)
            {
                Debug.WriteLine($"Logo absent ou illisible (ressource {ResourceName})");
                return null;
            }

            // « F0 » : règle pair-impair ; « F1 » : non nulle (langage des chemins d'Avalonia, comme WPF).
            var geometries = frame.Paths.Select(path => Geometry.Parse((path.EvenOdd ? "F0 " : "F1 ") + path.Data)).ToArray();
            return new LoadedLogo(frame, geometries);
        }
        catch (FormatException ex)
        {
            Debug.WriteLine($"Logo illisible (ressource {ResourceName}) : {ex.Message}");
            return null;
        }
    }

    private sealed record LoadedLogo(SvgPaths Frame, Geometry[] Geometries);
}
