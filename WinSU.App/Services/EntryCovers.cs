using Avalonia.Media.Imaging;
using WinSU.App.ViewModels.Shell;
using WinSU.Core.Media;

namespace WinSU.App.Services;

/// <summary>
/// Jaquettes des tuiles d'un écran (M6) : recherche de l'image hors du thread d'interface (<see cref="IMediaResolver"/>),
/// décodage à la taille de la tuile, un seul bitmap par fichier. À vider (<see cref="Clear"/>) en quittant l'écran.
/// </summary>
public sealed class EntryCovers(IMediaResolver resolver, IImageLoader images)
{
    /// <summary>Côté d'une tuile en pixels : 192 px d'interface, affichés à 200 %.</summary>
    public const int TileSize = 384;

    private const int MaxConcurrentLoads = 4;

    private readonly Dictionary<string, Task<Bitmap?>> _byPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Task<Bitmap?>> _forgotten = [];
    private readonly SemaphoreSlim _gate = new(MaxConcurrentLoads);
    private readonly Lock _lock = new();
    private int _generation;

    public Task ApplyAsync(IEnumerable<GameTileViewModel> tiles) => Task.WhenAll(tiles.ToList().Select(ApplyAsync));

    /// <summary>Cherche l'image de l'entrée et l'affiche sur la tuile (null : tuile par défaut).</summary>
    public async Task ApplyAsync(GameTileViewModel tile)
    {
        var generation = Volatile.Read(ref _generation);
        await _gate.WaitAsync();
        try
        {
            var art = await Task.Run(() => resolver.Find(tile.Game));
            var cover = art is null ? null : await Get(art.Path);
            if (generation == Volatile.Read(ref _generation)) // écran quitté entre-temps : ne rien afficher
                tile.ShowCover(cover, art?.IsIcon == true);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Le fichier a changé (image choisie à nouveau) : le prochain affichage le relira.</summary>
    public void Forget(string path)
    {
        lock (_lock)
        {
            if (_byPath.Remove(path, out var load))
                _forgotten.Add(load);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            Interlocked.Increment(ref _generation);
            foreach (var load in _byPath.Values.Concat(_forgotten).Where(t => t.IsCompletedSuccessfully))
                load.Result?.Dispose();

            _byPath.Clear();
            _forgotten.Clear();
        }
    }

    private Task<Bitmap?> Get(string path)
    {
        lock (_lock)
        {
            if (!_byPath.TryGetValue(path, out var load))
                _byPath[path] = load = images.LoadToFillAsync(path, TileSize);
            return load;
        }
    }
}
