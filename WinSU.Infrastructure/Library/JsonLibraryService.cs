using WinSU.Core.Library;
using WinSU.Core.Storage;
using WinSU.Infrastructure.Storage;
using Microsoft.Extensions.Logging;

namespace WinSU.Infrastructure.Library;

/// <summary><see cref="ILibraryService"/> adossé à <c>library.json</c>, avec les mêmes garanties que les réglages.</summary>
public sealed class JsonLibraryService : ILibraryService
{
    private readonly string _filePath;
    private readonly string _mediaDirectory;
    private readonly ILogger<JsonLibraryService> _logger;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Snapshot _snapshot = Snapshot.Empty;

    public JsonLibraryService(IDataPaths paths, ILogger<JsonLibraryService> logger, TimeProvider? time = null)
    {
        _filePath = paths.LibraryFile;
        _mediaDirectory = paths.MediaDirectory;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    public IReadOnlyList<LibraryConsole> Consoles => Volatile.Read(ref _snapshot).Consoles;

    public IReadOnlyList<LibraryGame> Games => Volatile.Read(ref _snapshot).Games;

    public event EventHandler? Changed;

    public bool ContainsConsole(string consoleId) => Consoles.Any(c => SameId(c.ConsoleId, consoleId));

    /// <summary>Charge (ou recharge, après une restauration) la bibliothèque, puis prévient les écrans.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await LoadCoreAsync(cancellationToken).ConfigureAwait(false);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath))
            {
                Volatile.Write(ref _snapshot, Snapshot.Empty);
                return;
            }

            var document = await AtomicJsonFile.ReadOrQuarantineAsync(_filePath, WinSUJsonContext.Default.LibraryDocument, _time,
                (ex, quarantined) => _logger.LogWarning(ex, "Bibliothèque illisible, fichier mis de côté sous {Quarantined} ; bibliothèque vide", quarantined),
                cancellationToken).ConfigureAwait(false);
            if (document is null)
            {
                Volatile.Write(ref _snapshot, Snapshot.Empty);
                return;
            }

            if (document.Apps is { Count: > 0 })
                await ConvertLegacyAppsAsync(document, cancellationToken).ConfigureAwait(false);

            var snapshot = FromDocument(document);
            Volatile.Write(ref _snapshot, snapshot);
            _logger.LogInformation("Bibliothèque chargée : {Consoles} console(s), {Games} jeu(x)", snapshot.Consoles.Count, snapshot.Games.Count);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<bool> AddConsoleAsync(string consoleId, CancellationToken cancellationToken = default) =>
        MutateAsync(state => state.AddConsole(consoleId, _time.GetUtcNow()), cancellationToken);

    public Task<bool> RemoveConsoleAsync(string consoleId, CancellationToken cancellationToken = default) =>
        MutateAsync(state =>
        {
            if (state.Consoles.RemoveAll(c => SameId(c.ConsoleId, consoleId)) == 0)
                return false;

            state.Games.RemoveAll(g => SameId(g.ConsoleId, consoleId));
            return true;
        }, cancellationToken);

    public Task<bool> MoveConsoleAsync(string consoleId, int offset, CancellationToken cancellationToken = default) =>
        MutateAsync(state =>
        {
            var index = state.Consoles.FindIndex(c => SameId(c.ConsoleId, consoleId));
            if (index < 0)
                return false;

            var target = Math.Clamp(index + offset, 0, state.Consoles.Count - 1);
            if (target == index)
                return false;

            var console = state.Consoles[index];
            state.Consoles.RemoveAt(index);
            state.Consoles.Insert(target, console);
            return true;
        }, cancellationToken);

    public async Task<IReadOnlyList<LibraryGame>> AddGamesAsync(string consoleId, IEnumerable<string> sourcePaths, CancellationToken cancellationToken = default)
    {
        var added = new List<LibraryGame>();
        await MutateAsync(state =>
        {
            var now = _time.GetUtcNow();
            state.AddConsole(consoleId, now);
            foreach (var raw in sourcePaths)
            {
                var path = Path.GetFullPath(raw);
                var alreadyThere = state.Games.Any(g => SameId(g.ConsoleId, consoleId)
                    && string.Equals(g.SourcePath, path, StringComparison.OrdinalIgnoreCase));
                if (alreadyThere)
                    continue;

                var game = new LibraryGame(Guid.NewGuid(), consoleId, NameFromFile(path), path, now);
                state.Games.Add(game);
                added.Add(game);
            }

            return added.Count > 0;
        }, cancellationToken).ConfigureAwait(false);

        return added;
    }

    public Task<bool> RemoveEntryAsync(Guid entryId, CancellationToken cancellationToken = default) =>
        MutateAsync(state => state.Games.RemoveAll(g => g.Id == entryId) > 0, cancellationToken);

    public Task<bool> RenameEntryAsync(Guid entryId, string name, CancellationToken cancellationToken = default) =>
        MutateAsync(state => state.Replace(entryId, e => string.IsNullOrWhiteSpace(name) || e.Name == name.Trim() ? e : e with { Name = name.Trim() }), cancellationToken);

    public Task<bool> MarkPlayedAsync(Guid entryId, DateTimeOffset playedAt, CancellationToken cancellationToken = default) =>
        MutateAsync(state => state.Replace(entryId, e => e with { LastPlayedAt = playedAt }), cancellationToken);

    /// <summary>« Super Mario Sunshine.lnk » → « Super Mario Sunshine » (D-7).</summary>
    internal static string NameFromFile(string path) => Path.GetFileNameWithoutExtension(path).Trim();

    private static bool SameId(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Étape 35 (D-175) : les applications d'un ancien fichier deviennent des jeux de la console « windows », leurs images
    /// suivent ; le fichier est réécrit sans elles. Un échec d'écriture est seulement noté (la conversion sera refaite).
    /// </summary>
    private async Task ConvertLegacyAppsAsync(LibraryDocument document, CancellationToken cancellationToken)
    {
        var converted = LegacyApps.Convert(document);
        try
        {
            var images = LegacyApps.MoveMedia(_mediaDirectory);
            await AtomicJsonFile.WriteAsync(_filePath, document, WinSUJsonContext.Default.LibraryDocument, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("{Count} application(s) devenue(s) des jeux de la console Windows ({Images} image(s) déplacée(s))", converted, images);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Applications converties en jeux de la console Windows, mais non enregistrées");
        }
    }

    /// <summary>Applique une modification sur une copie ; si elle change quelque chose, l'enregistre puis prévient.</summary>
    private async Task<bool> MutateAsync(Func<MutableState, bool> mutate, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = new MutableState(Volatile.Read(ref _snapshot));
            if (!mutate(state))
                return false;

            var snapshot = state.ToSnapshot();
            await AtomicJsonFile.WriteAsync(_filePath, ToDocument(snapshot), WinSUJsonContext.Default.LibraryDocument, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _snapshot, snapshot);
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private static Snapshot FromDocument(LibraryDocument document)
    {
        var consoles = document.Consoles
            .Where(c => !string.IsNullOrWhiteSpace(c.ConsoleId))
            .DistinctBy(c => c.ConsoleId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var games = consoles
            .SelectMany(c => c.Games
                .Where(g => !string.IsNullOrWhiteSpace(g.SourcePath))
                .Select(g => new LibraryGame(
                    g.Id == Guid.Empty ? Guid.NewGuid() : g.Id,
                    c.ConsoleId,
                    string.IsNullOrWhiteSpace(g.Name) ? NameFromFile(g.SourcePath) : g.Name,
                    g.SourcePath,
                    g.AddedAt,
                    g.LastPlayedAt)))
            .DistinctBy(g => g.Id)
            .ToList();

        return new Snapshot([.. consoles.Select(c => new LibraryConsole(c.ConsoleId, c.AddedAt))], games);
    }

    private static LibraryDocument ToDocument(Snapshot snapshot) => new()
    {
        Consoles =
        [
            .. snapshot.Consoles.Select(c => new LibraryConsoleEntry
            {
                ConsoleId = c.ConsoleId,
                AddedAt = c.AddedAt,
                Games = [.. snapshot.Games.Where(g => SameId(g.ConsoleId, c.ConsoleId)).Select(ToItem)],
            }),
        ],
    };

    private static LibraryItemEntry ToItem(LibraryGame entry) => new()
    {
        Id = entry.Id,
        Name = entry.Name,
        SourcePath = entry.SourcePath,
        AddedAt = entry.AddedAt,
        LastPlayedAt = entry.LastPlayedAt,
    };

    private sealed record Snapshot(IReadOnlyList<LibraryConsole> Consoles, IReadOnlyList<LibraryGame> Games)
    {
        public static readonly Snapshot Empty = new([], []);
    }

    private sealed class MutableState(Snapshot snapshot)
    {
        public List<LibraryConsole> Consoles { get; } = [.. snapshot.Consoles];
        public List<LibraryGame> Games { get; } = [.. snapshot.Games];

        public bool AddConsole(string consoleId, DateTimeOffset now)
        {
            if (Consoles.Any(c => SameId(c.ConsoleId, consoleId)))
                return false;

            Consoles.Add(new LibraryConsole(consoleId, now));
            return true;
        }

        /// <summary>Remplace un jeu ; renvoie false s'il est introuvable ou inchangé.</summary>
        public bool Replace(Guid entryId, Func<LibraryGame, LibraryGame> change)
        {
            var index = Games.FindIndex(e => e.Id == entryId);
            if (index < 0)
                return false;

            var updated = change(Games[index]);
            if (updated == Games[index])
                return false;

            Games[index] = updated;
            return true;
        }

        public Snapshot ToSnapshot() => new([.. Consoles], [.. Games]);
    }
}
