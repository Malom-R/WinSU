using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace WinSU.App.Controls;

/// <summary>Un chemin d'un SVG : données au format des chemins SVG, règle de remplissage.</summary>
internal sealed record SvgPath(string Data, bool EvenOdd);

/// <summary>Contenu utile d'un SVG simple : cadre (viewBox) et chemins, dans l'ordre du document.</summary>
internal sealed record SvgPaths(double MinX, double MinY, double Width, double Height, IReadOnlyList<SvgPath> Paths);

/// <summary>
/// Lecteur minimal de SVG pour le logo « WinSU » (D-172) : garde le cadre et les éléments <c>path</c>, ignore les couleurs
/// (le logo est recoloré par l'application). Suffisant pour un mot-symbole plein ; pas un moteur SVG.
/// </summary>
internal static class SvgPathReader
{
    /// <summary>Les chemins du document, ou null s'il n'est pas un SVG lisible avec au moins un chemin.</summary>
    public static SvgPaths? Read(string svg)
    {
        XElement? root;
        try
        {
            root = XDocument.Parse(svg).Root;
        }
        catch (XmlException)
        {
            return null;
        }

        if (root?.Name.LocalName != "svg" || ReadFrame(root) is not { } frame)
            return null;

        var paths = root.Descendants()
            .Where(element => element.Name.LocalName == "path")
            .Select(element => (Data: (string?)element.Attribute("d"), Rule: (string?)element.Attribute("fill-rule")))
            .Where(path => !string.IsNullOrWhiteSpace(path.Data))
            .Select(path => new SvgPath(path.Data!.Trim(), string.Equals(path.Rule, "evenodd", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return paths.Count == 0 ? null : new SvgPaths(frame.X, frame.Y, frame.Width, frame.Height, paths);
    }

    /// <summary>Le cadre : viewBox, sinon width et height (à partir de 0, 0).</summary>
    private static (double X, double Y, double Width, double Height)? ReadFrame(XElement root)
    {
        var viewBox = ((string?)root.Attribute("viewBox"))?
            .Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(ReadNumber)
            .ToArray();
        if (viewBox is { Length: 4 } box && box.All(value => value is not null) && box[2] > 0 && box[3] > 0)
            return (box[0]!.Value, box[1]!.Value, box[2]!.Value, box[3]!.Value);

        var width = ReadNumber((string?)root.Attribute("width"));
        var height = ReadNumber((string?)root.Attribute("height"));
        return width > 0 && height > 0 ? (0, 0, width.Value, height.Value) : null;
    }

    private static double? ReadNumber(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
}
