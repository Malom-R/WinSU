using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using WinSU.App.Controls;
using WinSU.Core.Appearance;

namespace WinSU.App.Themes;

/// <summary>
/// Jetons d'un thème enregistré (M14), sous les clés de <c>Themes/Theme.axaml</c> : placé après lui dans les ressources
/// de l'application, ce dictionnaire l'emporte ; les jetons absents (ombres, liserés, motif du fond) restent ceux de la base.
/// Les formes des dégradés (centre du reflet du verre, angles) sont celles de <c>Theme.axaml</c>.
/// </summary>
public static class ThemeResources
{
    private static readonly string[] SolidTokens =
    [
        "Background", "Surface", "SurfaceAlt", "GlassOpaque", "Dialog", "Text", "TextMuted", "TextTertiary",
        "Accent", "AccentHover", "AccentStrong", "AccentFaded", "AccentTint", "OnAccent", "SwitchOn", "Scrim",
        "FocusRing", "Signature", "SignatureAlt",
    ];

    /// <summary>Arrêts du fond du verre des tuiles : voile, bas du reflet, bande sombre, remontée, bas (Themes/Theme.axaml).</summary>
    private static readonly double[] GlassFillOffsets = [0, 0.58, 0.66, 0.84, 1];

    /// <summary>Arrêts du verre de la tuile choisie : haut, milieu, bas (Themes/Theme.axaml, WinSU.TileSelectedFillBrush).</summary>
    private static readonly double[] SelectedFillOffsets = [0, 0.6, 1];

    /// <summary>Coins des équerres de la tuile choisie, dans l'ordre des arrêts « Cursor » (WinSU.Cursor*Brush).</summary>
    private static readonly string[] CursorCorners = ["TopLeft", "TopRight", "BottomRight", "BottomLeft"];

    public static ResourceDictionary Build(ThemePalette palette)
    {
        var resources = new ResourceDictionary();
        foreach (var token in SolidTokens)
            resources[$"WinSU.{token}Brush"] = new SolidColorBrush(ToColor(palette[token]));

        var glass = palette.Stops["Glass"];
        resources["WinSU.GlassBrush"] = glass.Count == 1
            ? new SolidColorBrush(ToColor(glass[0]))
            : new RadialGradientBrush
            {
                Center = RelativePoint.Parse("25%,25%"),
                GradientOrigin = RelativePoint.Parse("25%,25%"),
                RadiusX = RelativeScalar.Parse("110%"),
                RadiusY = RelativeScalar.Parse("110%"),
                GradientStops = Stops(glass, [0, 0.48, 1]),
            };
        resources["WinSU.ControlBrush"] = Vertical(palette.Stops["Control"]);
        resources["WinSU.ControlHoverBrush"] = Vertical(palette.Stops["ControlHover"]);

        var gradient = palette.Stops["Gradient"];
        resources["WinSU.AccentGradientBrush"] = Linear(gradient, "0%,0%", "100%,100%");
        resources["WinSU.AccentGradient45Brush"] = Linear(gradient, "0%,100%", "100%,0%");
        resources["WinSU.RingGradientBrush"] = Linear(palette.Stops["RingGradient"], "0%,100%", "100%,0%");
        resources["WinSU.TileGlassFillBrush"] = new LinearGradientBrush
        {
            StartPoint = RelativePoint.Parse("50%,0%"),
            EndPoint = RelativePoint.Parse("50%,100%"),
            GradientStops = Stops(palette.Stops["GlassFill"], GlassFillOffsets),
        };
        resources["WinSU.TileSelectedFillBrush"] = new LinearGradientBrush
        {
            StartPoint = RelativePoint.Parse("50%,0%"),
            EndPoint = RelativePoint.Parse("50%,100%"),
            GradientStops = Stops(palette.Stops["SelectedFill"], SelectedFillOffsets),
        };
        var cursor = palette.Stops["Cursor"];
        for (var corner = 0; corner < CursorCorners.Length; corner++)
            resources[$"WinSU.Cursor{CursorCorners[corner]}Brush"] = new SolidColorBrush(ToColor(cursor[corner]));
        resources["WinSU.LavaPaletteBrush"] = Linear(palette.Stops["Lava"], "0%,0%", "100%,0%");

        resources["WinSU.AccentGlowShadow"] = new BoxShadows(new BoxShadow { Blur = 12, Color = ToColor(palette["AccentGlow"]) });
        resources["WinSU.FocusGlow"] = new BoxShadows(new BoxShadow
        {
            Blur = TileMetrics.GlowRadius,
            Spread = TileMetrics.GlowSpread,
            Color = ToColor(palette["FocusGlow"]),
        });
        resources["WinSU.FocusOutline"] = new BoxShadows(
            new BoxShadow { Spread = TileMetrics.OutlineSpread, Color = ToColor(palette["FocusRing"]) },
            [new BoxShadow { Blur = TileMetrics.GlowRadius, Spread = TileMetrics.GlowSpread + TileMetrics.OutlineSpread, Color = ToColor(palette["FocusGlow"]) }]);
        return resources;
    }

    public static Color ToColor(ThemeColor color) => Color.FromArgb(color.A, color.R, color.G, color.B);

    public static ThemeColor FromColor(Color color) => new(color.R, color.G, color.B, color.A);

    /// <summary>Haut et bas (commandes de la base claire), ou une seule couleur (base sombre).</summary>
    private static IBrush Vertical(IReadOnlyList<ThemeColor> stops) =>
        stops.Count == 1 ? new SolidColorBrush(ToColor(stops[0])) : Linear(stops, "50%,0%", "50%,100%");

    private static LinearGradientBrush Linear(IReadOnlyList<ThemeColor> stops, string start, string end) => new()
    {
        StartPoint = RelativePoint.Parse(start),
        EndPoint = RelativePoint.Parse(end),
        GradientStops = Stops(stops, [.. Enumerable.Range(0, stops.Count).Select(i => stops.Count == 1 ? 0 : (double)i / (stops.Count - 1))]),
    };

    private static GradientStops Stops(IReadOnlyList<ThemeColor> colors, IReadOnlyList<double> offsets)
    {
        var stops = new GradientStops();
        for (var i = 0; i < colors.Count; i++)
            stops.Add(new GradientStop(ToColor(colors[i]), offsets[i]));
        return stops;
    }
}
