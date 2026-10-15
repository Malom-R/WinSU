namespace WinSU.Core.Library;

/// <summary>Une console ajoutée par l'utilisateur (D-10).</summary>
public sealed record LibraryConsole(string ConsoleId, DateTimeOffset AddedAt);

/// <summary>
/// Un jeu, rangé sous sa console (D-7 : un raccourci par jeu) : ce qui se lance depuis WinSU, un raccourci (.lnk, .url) ou
/// un programme (.exe, .bat) ajouté par l'utilisateur (D-4, D-6, D-176). Le nom vient du nom du fichier, renommable
/// ensuite. Le fichier est référencé là où il est (D-9). Depuis l'étape 35 (D-175), les anciennes applications sont des jeux
/// de la console « Windows ».
/// </summary>
public sealed record LibraryGame(
    Guid Id,
    string ConsoleId,
    string Name,
    string SourcePath,
    DateTimeOffset AddedAt,
    DateTimeOffset? LastPlayedAt = null);

/// <summary>
/// Bibliothèque de l'utilisateur, enregistrée dans <c>library.json</c>. L'ordre de <see cref="Consoles"/>
/// est l'ordre d'affichage choisi par l'utilisateur.
/// </summary>
public interface ILibraryService
{
    IReadOnlyList<LibraryConsole> Consoles { get; }

    IReadOnlyList<LibraryGame> Games { get; }

    /// <summary>Déclenché après chaque modification enregistrée (sur le thread de l'appelant).</summary>
    event EventHandler? Changed;

    Task LoadAsync(CancellationToken cancellationToken = default);

    bool ContainsConsole(string consoleId);

    /// <summary>Ajoute la console à la fin. Renvoie false si elle y était déjà.</summary>
    Task<bool> AddConsoleAsync(string consoleId, CancellationToken cancellationToken = default);

    /// <summary>Retire la console et ses jeux de la bibliothèque (aucun fichier n'est supprimé).</summary>
    Task<bool> RemoveConsoleAsync(string consoleId, CancellationToken cancellationToken = default);

    /// <summary>Déplace la console de <paramref name="offset"/> places (−1 : vers la gauche). Renvoie false si rien ne bouge.</summary>
    Task<bool> MoveConsoleAsync(string consoleId, int offset, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ajoute des jeux à une console (qui est ajoutée si besoin). Un fichier déjà présent dans cette console est ignoré.
    /// Renvoie les jeux réellement ajoutés.
    /// </summary>
    Task<IReadOnlyList<LibraryGame>> AddGamesAsync(string consoleId, IEnumerable<string> sourcePaths, CancellationToken cancellationToken = default);

    /// <summary>Retire un jeu (aucun fichier n'est supprimé).</summary>
    Task<bool> RemoveEntryAsync(Guid entryId, CancellationToken cancellationToken = default);

    Task<bool> RenameEntryAsync(Guid entryId, string name, CancellationToken cancellationToken = default);

    Task<bool> MarkPlayedAsync(Guid entryId, DateTimeOffset playedAt, CancellationToken cancellationToken = default);
}

public static class LibraryServiceExtensions
{
    public static IEnumerable<LibraryGame> GamesOf(this ILibraryService library, string consoleId) =>
        library.Games.Where(g => string.Equals(g.ConsoleId, consoleId, StringComparison.OrdinalIgnoreCase));

    public static LibraryGame? FindGame(this ILibraryService library, Guid gameId) =>
        library.Games.FirstOrDefault(g => g.Id == gameId);
}
