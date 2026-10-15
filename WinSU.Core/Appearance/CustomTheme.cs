namespace WinSU.Core.Appearance;

/// <summary>Thème de base : ses ombres, liserés et couleurs neutres servent de point de départ.</summary>
public enum ThemeBase
{
    Light,
    Dark,
}

public enum ThemeMode
{
    /// <summary>Une seule couleur : le reste en est tiré (D-39).</summary>
    Mono,

    /// <summary>Chaque couleur de <see cref="ThemeColors"/> est choisie.</summary>
    Multi,
}

/// <summary>Couleurs d'un thème multicouleur, en hexadécimal (« #RRGGBB »). Les textes sont toujours calculés (D-38).</summary>
public sealed record ThemeColors
{
    // Par défaut : le thème Sombre de WinSU (palette « Prisme », étape 34, D-168).
    public string Background { get; set; } = "#100F17";

    /// <summary>Verre des panneaux, des boutons et des fenêtres superposées.</summary>
    public string Glass { get; set; } = "#16151D";

    public string Accent { get; set; } = "#B39CFF";

    /// <summary>Dégradé du thème (onglet choisi, textes en dégradé), du début à la fin.</summary>
    public string Gradient1 { get; set; } = "#6EC3F5";
    public string Gradient2 { get; set; } = "#9D8CF5";
    public string Gradient3 { get; set; } = "#D88CEB";

    /// <summary>Gouttes de la lampe à lave (M10), prises à tour de rôle.</summary>
    public string Lava1 { get; set; } = "#6EC3F5";
    public string Lava2 { get; set; } = "#9D8CF5";
    public string Lava3 { get; set; } = "#D88CEB";
    public string Lava4 { get; set; } = "#8E7BEF";
}

/// <summary>
/// Thème enregistré (M14, E10, E11) : un nom, une base claire ou sombre, et soit une couleur (monocouleur), soit les
/// couleurs une à une (multicouleur). Traité comme immuable (<c>with</c>), comme les réglages.
/// </summary>
public sealed record CustomTheme
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public ThemeBase Base { get; set; } = ThemeBase.Dark;
    public ThemeMode Mode { get; set; } = ThemeMode.Mono;

    /// <summary>Couleur d'un thème monocouleur.</summary>
    public string Color { get; set; } = ThemePalette.DefaultColor;

    /// <summary>Couleurs d'un thème multicouleur.</summary>
    public ThemeColors Colors { get; set; } = new();
}

/// <summary>Thèmes enregistrés, dans <c>themes.json</c>.</summary>
public interface IThemeStore
{
    /// <summary>Dans l'ordre de création.</summary>
    IReadOnlyList<CustomTheme> Themes { get; }

    /// <summary>Après un enregistrement ou une suppression (sur le thread qui l'a demandé).</summary>
    event EventHandler? Changed;

    CustomTheme? Find(string? id);

    /// <summary>Lit le fichier ; illisible, il est mis de côté et la liste est vide.</summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Ajoute le thème, ou remplace celui qui a le même identifiant.</summary>
    Task SaveAsync(CustomTheme theme, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
