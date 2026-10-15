using WinSU.Core.Appearance;
using WinSU.Core.Storage;
using Microsoft.Extensions.Logging;

namespace WinSU.Infrastructure.Storage;

/// <summary>Contenu de <c>themes.json</c> (M14).</summary>
public sealed record ThemesDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public List<CustomTheme> Themes { get; set; } = [];
}

/// <summary>
/// <see cref="IThemeStore"/> adossé à <c>themes.json</c> : écritures atomiques, fichier illisible mis de côté, comme les
/// réglages et la bibliothèque.
/// </summary>
public sealed class JsonThemeStore(IDataPaths paths, ILogger<JsonThemeStore> logger, TimeProvider? time = null) : IThemeStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<CustomTheme> _themes = [];

    public IReadOnlyList<CustomTheme> Themes => Volatile.Read(ref _themes);

    public event EventHandler? Changed;

    public CustomTheme? Find(string? id) => id is null ? null : Themes.FirstOrDefault(t => t.Id == id);

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(paths.ThemesFile))
                return;

            var document = await AtomicJsonFile.ReadOrQuarantineAsync(paths.ThemesFile, WinSUJsonContext.Default.ThemesDocument, time ?? TimeProvider.System,
                (ex, quarantined) => logger.LogWarning(ex, "Thèmes illisibles, fichier mis de côté sous {Quarantined}", quarantined),
                cancellationToken).ConfigureAwait(false);
            if (document is not null)
            {
                Volatile.Write(ref _themes, document.Themes);
                logger.LogInformation("Thèmes chargés : {Count}", document.Themes.Count);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task SaveAsync(CustomTheme theme, CancellationToken cancellationToken = default) =>
        UpdateAsync(
            themes => themes.Any(t => t.Id == theme.Id) ? [.. themes.Select(t => t.Id == theme.Id ? theme : t)] : [.. themes, theme],
            cancellationToken);

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        UpdateAsync(themes => [.. themes.Where(t => t.Id != id)], cancellationToken);

    private async Task UpdateAsync(Func<IReadOnlyList<CustomTheme>, List<CustomTheme>> update, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var themes = update(Themes);
            await AtomicJsonFile.WriteAsync(paths.ThemesFile, new ThemesDocument { Themes = themes }, WinSUJsonContext.Default.ThemesDocument, cancellationToken)
                .ConfigureAwait(false);
            Volatile.Write(ref _themes, themes);
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
