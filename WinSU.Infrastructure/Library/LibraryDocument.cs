namespace WinSU.Infrastructure.Library;

/// <summary>Contenu de <c>library.json</c> (voir notes 3.5).</summary>
public sealed class LibraryDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>Dans l'ordre d'affichage.</summary>
    public List<LibraryConsoleEntry> Consoles { get; set; } = [];

    /// <summary>
    /// Anciennes applications (onglet Apps, retiré à l'étape 35) : lues pour devenir des jeux de la console « Windows »
    /// (<see cref="LegacyApps"/>), jamais écrites (null).
    /// </summary>
    public List<LibraryItemEntry>? Apps { get; set; }
}

public sealed class LibraryConsoleEntry
{
    public string ConsoleId { get; set; } = string.Empty;
    public DateTimeOffset AddedAt { get; set; }
    public List<LibraryItemEntry> Games { get; set; } = [];
}

/// <summary>Un jeu (sous sa console).</summary>
public sealed class LibraryItemEntry
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Raccourci (.lnk, .url) ou programme (.exe, .bat), à l'endroit où l'utilisateur l'a laissé (D-9).</summary>
    public string SourcePath { get; set; } = string.Empty;

    public DateTimeOffset AddedAt { get; set; }
    public DateTimeOffset? LastPlayedAt { get; set; }
}
