using WinSU.Core.Appearance;

namespace WinSU.Core.Catalog;

/// <summary>
/// Une console du catalogue de WinSU (étape 34, D-148, D-156 : Sega, Nintendo, Xbox, PlayStation, Windows). Sa tuile et son
/// cadre sont dessinés par WinSU (D-149, D-150) à partir de sa couleur et de son icône.
/// </summary>
public sealed record ConsoleDefinition
{
    /// <summary>
    /// Identifiant court, ex. <c>gc</c>, <c>psx</c>, <c>n3ds</c> ; celui que la bibliothèque emploie (un alias, si c'est par
    /// lui que la console a été trouvée : <see cref="IConsoleCatalog.Find"/>).
    /// </summary>
    public required string Id { get; init; }

    /// <summary>Autres identifiants de la même console (doublons régionaux fusionnés, ex. <c>genesis</c> pour <c>megadrive</c>).</summary>
    public IReadOnlyList<string> Aliases { get; init; } = [];

    public required string Name { get; init; }

    public string? Manufacturer { get; init; }

    public int? ReleaseYear { get; init; }

    /// <summary>Autres noms de la console (ex. « Super Famicom » pour la Super Nintendo).</summary>
    public IReadOnlyList<string> AlternativeNames { get; init; } = [];

    /// <summary>Couleur de la console : fond de sa tuile, cadre et pastille de ses jeux.</summary>
    public ThemeColor Color { get; init; } = DefaultColor;

    /// <summary>Icône de la console (traits blancs, chemin absolu), ou null si le fichier manque dans les assets.</summary>
    public string? IconPath { get; init; }

    /// <summary>
    /// Icône de sa manette (silhouette blanche, chemin absolu) : la pastille des cadres de ses jeux (étape 34, demande de
    /// l'utilisateur : des icônes différentes de celles des tuiles) ; null si le fichier manque.
    /// </summary>
    public string? PadIconPath { get; init; }

    /// <summary>Couleur d'une console dont le catalogue n'en donne pas (ou une illisible) : un gris bleuté neutre.</summary>
    public static readonly ThemeColor DefaultColor = new(0x6E, 0x73, 0x80);

    /// <summary>L'identifiant <paramref name="id"/> désigne cette console (le sien ou un de ses alias).</summary>
    public bool Answers(string id) =>
        string.Equals(Id, id, StringComparison.OrdinalIgnoreCase) || Aliases.Contains(id, StringComparer.OrdinalIgnoreCase);
}
