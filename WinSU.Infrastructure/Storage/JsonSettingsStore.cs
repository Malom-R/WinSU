using WinSU.Core.Settings;
using WinSU.Core.Storage;
using Microsoft.Extensions.Logging;

namespace WinSU.Infrastructure.Storage;

/// <summary>
/// <see cref="ISettingsStore"/> adossé à <c>settings.json</c>. Les écritures sont atomiques :
/// on écrit un fichier temporaire, puis on remplace l'original.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string _filePath;
    private readonly ILogger<JsonSettingsStore> _logger;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private AppSettings _current = new();

    public JsonSettingsStore(IDataPaths paths, ILogger<JsonSettingsStore> logger, TimeProvider? time = null)
    {
        _filePath = paths.SettingsFile;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    public AppSettings Current => Volatile.Read(ref _current);

    public event EventHandler<SettingsChangedEventArgs>? Changed;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath))
            {
                _logger.LogInformation("Aucun fichier de réglages, création de {File} avec les valeurs par défaut", _filePath);
                await WriteAsync(new AppSettings(), cancellationToken).ConfigureAwait(false);
                Volatile.Write(ref _current, new AppSettings());
                return;
            }

            var loaded = await AtomicJsonFile.ReadOrQuarantineAsync(_filePath, WinSUJsonContext.Default.AppSettings, _time,
                (ex, quarantined) => _logger.LogWarning(ex, "Réglages illisibles, fichier mis de côté sous {Quarantined} ; valeurs par défaut utilisées", quarantined),
                cancellationToken).ConfigureAwait(false);
            if (loaded is null)
            {
                loaded = new AppSettings();
                await WriteAsync(loaded, cancellationToken).ConfigureAwait(false);
            }

            if (loaded.SchemaVersion > AppSettings.CurrentSchemaVersion)
            {
                _logger.LogWarning(
                    "settings.json vient d'une version plus récente (schéma {FileVersion} > {KnownVersion}) ; les champs inconnus seront perdus à la prochaine sauvegarde",
                    loaded.SchemaVersion, AppSettings.CurrentSchemaVersion);
            }

            Volatile.Write(ref _current, loaded);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateAsync(Func<AppSettings, AppSettings> update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        AppSettings previous;
        AppSettings next;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            previous = _current;
            next = update(previous) with { SchemaVersion = AppSettings.CurrentSchemaVersion };
            if (next == previous)
                return;

            // On écrit d'abord : si l'écriture échoue, l'état en mémoire reste cohérent avec le disque.
            await WriteAsync(next, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _current, next);
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, new SettingsChangedEventArgs(previous, next));
    }

    private Task WriteAsync(AppSettings settings, CancellationToken cancellationToken) =>
        AtomicJsonFile.WriteAsync(_filePath, settings, WinSUJsonContext.Default.AppSettings, cancellationToken);
}
