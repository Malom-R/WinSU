using System.Collections.Frozen;
using System.Text.Json;
using WinSU.Core.Appearance;
using WinSU.Core.Catalog;
using WinSU.Core.Storage;
using Microsoft.Extensions.Logging;

namespace WinSU.Infrastructure.Catalog;

public sealed class JsonConsoleCatalog(IAssetLocator assets, ILogger<JsonConsoleCatalog> logger) : IConsoleCatalog
{
    private Snapshot _snapshot = Snapshot.Empty;

    public IReadOnlyList<ConsoleDefinition> All => Volatile.Read(ref _snapshot).All;

    public ConsoleDefinition? Find(string id) =>
        Volatile.Read(ref _snapshot).ById.GetValueOrDefault(id);

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var path = assets.Resolve(CatalogDocument.RelativePath);
        if (!File.Exists(path))
        {
            logger.LogError("Catalogue des consoles introuvable ({Path}) : réinstallez WinSU", path);
            return;
        }

        CatalogDocument document;
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            document = await CatalogSerializer.ReadAsync(stream, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Catalogue des consoles illisible ({Path}) : réinstallez WinSU", path);
            return;
        }

        var missingFiles = 0;
        string? Existing(string? relative)
        {
            if (string.IsNullOrEmpty(relative))
            {
                missingFiles++;
                return null;
            }

            var absolute = assets.Resolve(relative);
            if (File.Exists(absolute))
                return absolute;

            missingFiles++;
            return null;
        }

        var consoles = new List<ConsoleDefinition>(document.Consoles.Count);
        var byId = new Dictionary<string, ConsoleDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in document.Consoles)
        {
            var definition = new ConsoleDefinition
            {
                Id = entry.Id,
                Aliases = entry.Aliases,
                Name = entry.Name,
                Manufacturer = entry.Manufacturer,
                ReleaseYear = entry.ReleaseYear,
                AlternativeNames = entry.AlternativeNames,
                Color = ThemeColor.TryParse(entry.Color, out var color) ? color : ConsoleDefinition.DefaultColor,
                IconPath = Existing(entry.Icon),
                PadIconPath = Existing(entry.Pad),
            };
            if (!ThemeColor.TryParse(entry.Color, out _))
                logger.LogWarning("Console {Id} : couleur absente ou illisible ({Color}), gris par défaut", entry.Id, entry.Color);

            if (!byId.TryAdd(definition.Id, definition))
            {
                logger.LogWarning("Console en double dans le catalogue, seule la première est gardée : {Id}", definition.Id);
                continue;
            }

            consoles.Add(definition);
            // Un alias rend la console sous son propre identifiant : celui que la bibliothèque emploie
            foreach (var alias in definition.Aliases)
            {
                if (!byId.TryAdd(alias, definition with { Id = alias }))
                    logger.LogWarning("Alias en double dans le catalogue, ignoré : {Alias}", alias);
            }
        }

        consoles.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        Volatile.Write(ref _snapshot, new Snapshot(consoles, byId.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase)));

        if (missingFiles > 0)
            logger.LogWarning("Catalogue : {Missing} icône(s) introuvable(s) dans {AssetsRoot}", missingFiles, assets.Root);

        logger.LogInformation("Catalogue chargé : {Count} consoles", consoles.Count);
    }

    private sealed record Snapshot(IReadOnlyList<ConsoleDefinition> All, FrozenDictionary<string, ConsoleDefinition> ById)
    {
        public static readonly Snapshot Empty = new([], FrozenDictionary<string, ConsoleDefinition>.Empty);
    }
}
