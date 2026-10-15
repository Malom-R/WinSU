using Avalonia.Media.Imaging;
using WinSU.Core.Library;
using WinSU.Core.Media;

namespace WinSU.App.Services;

/// <summary>Image d'une entrée trouvée sur le disque (bannière héro, logo), avec la date du fichier (une image remplacée est relue).</summary>
public sealed record ArtFile(string Path, DateTime Stamp);

/// <summary>
/// Images d'une sorte (<see cref="ArtKind.Hero"/> : bannières héro, M9 ; <see cref="ArtKind.Logo"/> : logos, étape 33)
/// pour un écran : recherche dans le dossier <c>media</c> hors du thread d'interface, décodage à une largeur au plus, un
/// seul bitmap par fichier. À vider (<see cref="Clear"/>) en quittant l'écran.
/// </summary>
public sealed class ArtImages(ICustomArtStore store, IImageLoader images, ArtKind kind, int decodeWidth)
{
    /// <summary>
    /// Largeur de décodage des bannières au plus (étape 28, D-82) : les héros de SteamGridDB (1920 × 620, 3840 × 1240)
    /// gardent leur taille d'origine, jamais agrandis au décodage. Avant : 2400, et une image de 3840 était réduite puis
    /// agrandie de nouveau à l'écran (×1,9 en plein écran à 200 %). Un bitmap de 3840 × 1240 occupe ≈ 19 Mo, jusqu'à ce
    /// qu'on quitte l'accueil (<see cref="Clear"/>).
    /// </summary>
    public const int HeroDecodeWidth = 3840;

    /// <summary>Largeur de décodage des logos au plus : le double de leur place la plus large (320 DIP), net à 200 %.</summary>
    public const int LogoDecodeWidth = 1280;

    private readonly Dictionary<ArtFile, Task<Bitmap?>> _loaded = [];
    private readonly Lock _lock = new();

    /// <summary>L'image de l'entrée, ou null si elle n'en a pas.</summary>
    public Task<ArtFile?> FindAsync(LibraryGame entry) =>
        Task.Run(() => store.Find(entry, kind) is { } path ? new ArtFile(path, Stamp(path)) : null);

    /// <summary>Le bitmap du fichier ; un logo perd ses marges transparentes (il se centre sur ce qu'il montre, étape 33).</summary>
    public Task<Bitmap?> LoadAsync(ArtFile file)
    {
        lock (_lock)
        {
            if (!_loaded.TryGetValue(file, out var load))
            {
                _loaded[file] = load = kind == ArtKind.Logo
                    ? images.LoadTrimmedToWidthAsync(file.Path, decodeWidth)
                    : images.LoadToWidthAsync(file.Path, decodeWidth);
            }

            return load;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            foreach (var load in _loaded.Values.Where(t => t.IsCompletedSuccessfully))
                load.Result?.Dispose();
            _loaded.Clear();
        }
    }

    private static DateTime Stamp(string path)
    {
        try
        {
            return File.GetLastWriteTimeUtc(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return default;
        }
    }
}
