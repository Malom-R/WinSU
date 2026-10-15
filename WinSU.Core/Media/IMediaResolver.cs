using WinSU.Core.Catalog;
using WinSU.Core.Library;

namespace WinSU.Core.Media;

/// <summary>D'où vient l'image d'une tuile.</summary>
public enum ArtSource
{
    /// <summary>Image du dossier <c>media</c> : choisie dans WinSU (copiée, D-14) ou déposée à la main.</summary>
    Custom,
}

/// <summary>Sorte d'image choisie pour une entrée.</summary>
public enum ArtKind
{
    /// <summary>Image de la tuile (jaquette, image ou icône choisie) : dossier <c>covers</c>.</summary>
    Cover,

    /// <summary>Bannière héro (M9, E6) : grande image large affichée en fond (1920 × 620 conseillé) : dossier <c>heroes</c>.</summary>
    Hero,

    /// <summary>
    /// Logo du jeu (étape 33, D-140) : image détourée (fond transparent), affichée entière sur l'accueil du Standard et du
    /// Switch : dossier <c>logos</c>.
    /// </summary>
    Logo,
}

public sealed record EntryArt(string Path, ArtSource Source)
{
    /// <summary>Icône (fichier <c>.ico</c>) : affichée entière et centrée sur la tuile, pas agrandie pour la remplir (D-25).</summary>
    public bool IsIcon => IsIconFile(Path);

    public static bool IsIconFile(string path) =>
        System.IO.Path.GetExtension(path).Equals(".ico", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Image d'un jeu ou d'une application (M6, notes 3.7e) : celle du dossier <c>media</c> (choisie ou déposée) ; ES-DE n'est
/// plus lu depuis l'étape 30 (30.10). Null : la tuile par défaut (fond de la console et nom, ou icône Windows).
/// </summary>
public interface IMediaResolver
{
    /// <summary>Lit le disque : à appeler hors du thread d'interface.</summary>
    EntryArt? Find(LibraryGame entry);
}

/// <summary>
/// Images choisies par l'utilisateur, copiées dans <c>media\&lt;console&gt;\covers\&lt;Nom du jeu&gt;.&lt;ext&gt;</c>
/// (<c>media\apps\covers\</c> pour les applications, D-14 et D-15), bannières héro dans <c>heroes</c> au lieu de
/// <c>covers</c> (M9), logos dans <c>logos</c> (étape 33). Les fichiers suivent le nom de l'entrée.
/// </summary>
public interface ICustomArtStore
{
    /// <summary>Extensions reconnues, dans l'ordre de recherche.</summary>
    IReadOnlyList<string> Extensions { get; }

    string? Find(LibraryGame entry, ArtKind kind = ArtKind.Cover);

    /// <summary>Copie l'image (et remplace celle d'une autre extension). Renvoie le chemin de la copie.</summary>
    string Save(LibraryGame entry, string imagePath, ArtKind kind = ArtKind.Cover);

    /// <summary>Supprime la copie. Renvoie false s'il n'y en avait pas.</summary>
    bool Remove(LibraryGame entry, ArtKind kind = ArtKind.Cover);

    /// <summary>Renomme les copies (image, bannière, logo) pour qu'elles suivent le nouveau nom de l'entrée (sans écraser une image existante).</summary>
    void Rename(LibraryGame entry, string newName);
}
