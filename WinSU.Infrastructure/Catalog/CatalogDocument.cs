namespace WinSU.Infrastructure.Catalog;

/// <summary>
/// Contenu de <c>Assets/Catalog/consoles.json</c>, le catalogue des consoles de WinSU (étape 34, D-148 : écrit par nous, à
/// partir de données publiques), lu par <see cref="JsonConsoleCatalog"/>. Propriétés en <c>set</c> pour la même raison que
/// <see cref="Core.Settings.AppSettings"/> (valeurs par défaut conservées par la sérialisation générée).
/// </summary>
public sealed class CatalogDocument
{
    /// <summary>3 : couleur et icône (étape 34, bloc 3) ; 2 : catalogue de WinSU ; 1 : celui tiré de l'APK d'iiSU.</summary>
    public const int CurrentSchemaVersion = 3;

    /// <summary>Chemin du fichier, relatif au dossier Assets.</summary>
    public const string RelativePath = "Catalog/consoles.json";

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public List<CatalogConsole> Consoles { get; set; } = [];
}

public sealed class CatalogConsole
{
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Autres identifiants de la même console (doublons régionaux fusionnés, ex. <c>genesis</c> pour <c>megadrive</c>) : une
    /// bibliothèque qui en utilise un garde sa console.
    /// </summary>
    public List<string> Aliases { get; set; } = [];

    public string Name { get; set; } = string.Empty;
    public List<string> AlternativeNames { get; set; } = [];
    public string? Manufacturer { get; set; }
    public int? ReleaseYear { get; set; }

    /// <summary>Couleur de la console, « #RRGGBB » (tuile, cadre, pastille ; dessinés par WinSU).</summary>
    public string? Color { get; set; }

    /// <summary>Icône, relative au dossier Assets (ex. <c>Icons/gc.png</c>) ; null s'il n'y en a pas.</summary>
    public string? Icon { get; set; }

    /// <summary>Icône de la manette, pour la pastille des cadres (ex. <c>Icons/Pads/gc.png</c>) ; null : l'icône de la console.</summary>
    public string? Pad { get; set; }
}
