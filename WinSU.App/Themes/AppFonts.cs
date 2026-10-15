using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using WinSU.Core.Settings;

namespace WinSU.App.Themes;

/// <summary>Une police proposée dans les Réglages : son nom, ses auteurs (crédités sur sa carte) et où la trouver.</summary>
public sealed record AppFontChoice(AppFont Value, string Name, string Designers, string Uri);

/// <summary>
/// Polices de l'interface : pour chaque choix des Réglages, la police du texte et celle des titres (ressources
/// <c>WinSU.BodyFont</c> et <c>WinSU.TitleFont</c> de Themes/Theme.axaml, prises en DynamicResource), appliquées à chaud par
/// <see cref="Services.ThemeService"/>. Plus aucune police de l'application iiSU d'origine (30.10) : DM Sans par défaut,
/// et depuis l'étape 32 (D-129) des polices libres (SIL OFL 1.1, Google Fonts), dans <c>Fonts/</c> avec leur licence. Les
/// anciens choix (Cal Sans, Arctainium, Console Sans, Gamemaster, Gamenum) restent lisibles dans settings.json et donnent
/// DM Sans. Une lettre absente d'une police (flèches) vient de DM Sans (Program.cs).
/// </summary>
public static class AppFonts
{
    public const string DmSansUri = "avares://WinSU/Fonts/DMSans#DM Sans";

    /// <summary>Glyphes de boutons de manette (U+E0A0…) : notre police, générée par tools/dev/glyph_font.py (30.10).</summary>
    public const string GlyphsUri = "avares://WinSU/Fonts/WinSUGlyphs#WinSU Glyphs";

    /// <summary>
    /// Les polices proposées, dans l'ordre des cartes : DM Sans, puis les très lisibles et les « jeu / techno » (choix de
    /// l'utilisateur). Auteurs : ceux de Google Fonts (fichier METADATA.pb de chaque police).
    /// </summary>
    public static IReadOnlyList<AppFontChoice> Choices { get; } =
    [
        new(AppFont.DmSans, "DM Sans", "Colophon Foundry, Jonny Pinhorn, Indian Type Foundry", DmSansUri),
        new(AppFont.Inter, "Inter", "Rasmus Andersson", "avares://WinSU/Fonts/Inter#Inter"),
        new(AppFont.AtkinsonHyperlegibleNext, "Atkinson Hyperlegible Next",
            "Braille Institute, Applied Design Works, Elliott Scott, Megan Eiswerth, Letters From Sweden",
            "avares://WinSU/Fonts/AtkinsonHyperlegibleNext#Atkinson Hyperlegible Next"),
        new(AppFont.Lexend, "Lexend", "Bonnie Shaver-Troup, Thomas Jockin, Santiago Orozco, Héctor Gómez, Superunion",
            "avares://WinSU/Fonts/Lexend#Lexend"),
        new(AppFont.Exo2, "Exo 2", "Natanael Gama", "avares://WinSU/Fonts/Exo2#Exo 2"),
        new(AppFont.ChakraPetch, "Chakra Petch", "Cadson Demak", "avares://WinSU/Fonts/ChakraPetch#Chakra Petch"),
        new(AppFont.Oxanium, "Oxanium", "Severin Meyer", "avares://WinSU/Fonts/Oxanium#Oxanium"),
    ];

    /// <summary>Texte courant : la police choisie (DM Sans pour un choix d'avant l'étape 30).</summary>
    public static FontFamily Body(AppFont font) => new(Choices.FirstOrDefault(c => c.Value == font)?.Uri ?? DmSansUri);

    /// <summary>Titres, onglets, boutons, étiquettes : la même police que le texte (D-71).</summary>
    public static FontFamily Title(AppFont font) => Body(font);

    /// <summary>Carte du choix de la police : son nom s'écrit dans la police qu'elle propose.</summary>
    public static readonly IValueConverter Preview = new FuncValueConverter<AppFont, FontFamily>(Body);

    public static void Apply(IResourceDictionary resources, AppFont font)
    {
        resources["WinSU.BodyFont"] = Body(font);
        resources["WinSU.TitleFont"] = Title(font);
        resources["ContentControlThemeFontFamily"] = Body(font); // commandes Fluent (champs de saisie…)
    }
}
