namespace WinSU.Core.Playtime;

public enum SessionEndReason
{
    /// <summary>Le jeu s'est fermé (tous ses processus).</summary>
    Exited,

    /// <summary>Jeu non suivi (ou relais vers un autre lanceur) : la session s'arrête quand l'utilisateur revient dans WinSU.</summary>
    UserReturned,
}

public sealed record PlaySession(Guid EntryId, DateTimeOffset StartedAt, DateTimeOffset EndedAt, SessionEndReason EndReason)
{
    public TimeSpan Duration => EndedAt - StartedAt;
}

/// <summary>Cumul du temps de jeu d'une entrée (jeu ou application).</summary>
public sealed record PlaytimeSummary(Guid EntryId, TimeSpan Total, int Sessions, DateTimeOffset LastPlayedAt);

/// <summary>Historique des sessions, dans <c>winsu.db</c> (SQLite).</summary>
public interface IPlaytimeService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task RecordAsync(PlaySession session, CancellationToken cancellationToken = default);

    /// <summary>Cumuls de toutes les entrées ayant au moins une session.</summary>
    Task<IReadOnlyDictionary<Guid, PlaytimeSummary>> GetSummariesAsync(CancellationToken cancellationToken = default);

    /// <summary>Les entrées jouées le plus récemment, de la plus récente à la plus ancienne.</summary>
    Task<IReadOnlyList<PlaytimeSummary>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
}
