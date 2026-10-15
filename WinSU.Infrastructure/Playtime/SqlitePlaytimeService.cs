using System.Globalization;
using WinSU.Core.Playtime;
using WinSU.Core.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace WinSU.Infrastructure.Playtime;

/// <summary>Sessions de jeu dans <c>winsu.db</c> ; les cumuls sont calculés par requête (voir notes 3.5).</summary>
public sealed class SqlitePlaytimeService(IDataPaths paths, ILogger<SqlitePlaytimeService> logger) : IPlaytimeService
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = paths.DatabaseFile,
        Pooling = false, // pas de fichier gardé ouvert entre deux requêtes (sauvegardes, tests)
    }.ToString();

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS play_sessions (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                entry_id    TEXT    NOT NULL,
                started_at  TEXT    NOT NULL,
                ended_at    TEXT    NOT NULL,
                duration_ms INTEGER NOT NULL,
                end_reason  TEXT    NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_play_sessions_entry ON play_sessions(entry_id);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RecordAsync(PlaySession session, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO play_sessions (entry_id, started_at, ended_at, duration_ms, end_reason)
            VALUES ($entry, $started, $ended, $duration, $reason);
            """;
        command.Parameters.AddWithValue("$entry", session.EntryId.ToString());
        command.Parameters.AddWithValue("$started", session.StartedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$ended", session.EndedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$duration", (long)Math.Max(0, session.Duration.TotalMilliseconds));
        command.Parameters.AddWithValue("$reason", session.EndReason.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);

        logger.LogInformation("Session enregistrée : {Entry}, {Duration}, fin : {Reason}", session.EntryId, session.Duration, session.EndReason);
    }

    public async Task<IReadOnlyDictionary<Guid, PlaytimeSummary>> GetSummariesAsync(CancellationToken cancellationToken = default) =>
        (await QuerySummariesAsync(limit: null, cancellationToken)).ToDictionary(s => s.EntryId);

    public async Task<IReadOnlyList<PlaytimeSummary>> GetRecentAsync(int count, CancellationToken cancellationToken = default) =>
        await QuerySummariesAsync(count, cancellationToken);

    private async Task<List<PlaytimeSummary>> QuerySummariesAsync(int? limit, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        // Dates stockées en UTC au format ISO 8601 (« O ») : leur tri alphabétique est chronologique.
        command.CommandText = """
            SELECT entry_id, SUM(duration_ms), COUNT(*), MAX(started_at)
            FROM play_sessions
            GROUP BY entry_id
            ORDER BY MAX(started_at) DESC
            """ + (limit is null ? string.Empty : " LIMIT $limit");
        if (limit is not null)
            command.Parameters.AddWithValue("$limit", limit.Value);

        var summaries = new List<PlaytimeSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!Guid.TryParse(reader.GetString(0), out var entryId))
                continue;

            summaries.Add(new PlaytimeSummary(
                entryId,
                TimeSpan.FromMilliseconds(reader.GetInt64(1)),
                reader.GetInt32(2),
                DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture)));
        }

        return summaries;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(paths.DatabaseFile)!);
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
