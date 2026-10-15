namespace WinSU.Core.Media;

/// <summary>Un jeu trouvé sur le site d'images (SteamGridDB, étape 31) ; <see cref="Year"/> null s'il n'est pas connu.</summary>
public sealed record ArtworkGame(int Id, string Name, int? Year, bool Verified);

/// <summary>
/// Une image proposée par le site : <see cref="ThumbUrl"/> pour le choix, <see cref="Url"/> pour l'image entière.
/// <see cref="Author"/> : son auteur (les images de SteamGridDB sont faites par la communauté).
/// </summary>
public sealed record ArtworkImage(int Id, string ThumbUrl, string Url, int Width, int Height, string? Author)
{
    public bool IsSquare => Width > 0 && Width == Height;
}

/// <summary>Pourquoi une recherche ou un téléchargement n'a pas abouti.</summary>
public enum ArtworkError
{
    None,

    /// <summary>Aucune clé dans les Réglages : rien n'est envoyé.</summary>
    NoKey,

    /// <summary>Le site refuse la clé (401, 403).</summary>
    KeyRefused,

    /// <summary>Le jeu n'existe pas (ou plus) sur le site (404).</summary>
    NotFound,

    /// <summary>Trop de demandes d'affilée (429) : réessayer plus tard.</summary>
    TooManyRequests,

    /// <summary>Pas de réseau, site injoignable ou trop lent.</summary>
    Offline,

    /// <summary>Autre erreur (réponse illisible, image refusée…).</summary>
    Failed,
}

/// <summary>Le résultat d'une demande : la valeur, ou l'erreur.</summary>
public sealed record ArtworkResult<T>(T? Value, ArtworkError Error)
{
    public bool Succeeded => Error == ArtworkError.None;

    public static ArtworkResult<T> Ok(T value) => new(value, ArtworkError.None);

    public static ArtworkResult<T> Fail(ArtworkError error) => new(default, error);
}

/// <summary>
/// Recherche d'images sur un site en ligne (étape 31, D-114 à D-116 : SteamGridDB, avec la clé de l'utilisateur).
/// Rien n'est envoyé sans clé, ni sans une action de l'utilisateur.
/// </summary>
public interface IArtworkSearch
{
    /// <summary>Une clé est enregistrée dans les Réglages (sinon l'option n'apparaît pas).</summary>
    bool HasKey { get; }

    /// <summary>Les jeux qui portent ce nom (plusieurs possibles) ; liste vide : rien trouvé.</summary>
    Task<ArtworkResult<IReadOnlyList<ArtworkGame>>> FindGamesAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Les images d'un jeu pour une sorte d'image : jaquettes (carrées d'abord, puis portrait), bannières héro (larges) ou
    /// logos (fond transparent, étape 33), les mieux notées d'abord dans chaque format ; images fixes, sans contenu adulte,
    /// humoristique ni épileptogène.
    /// </summary>
    Task<ArtworkResult<IReadOnlyList<ArtworkImage>>> FindImagesAsync(int gameId, ArtKind kind, CancellationToken cancellationToken = default);

    /// <summary>
    /// Télécharge l'image entière (ou sa vignette, <paramref name="thumbnail"/>) dans un fichier temporaire, que l'appelant
    /// supprime après l'avoir lu ou copié.
    /// </summary>
    Task<ArtworkResult<string>> DownloadAsync(ArtworkImage image, bool thumbnail = false, CancellationToken cancellationToken = default);

    /// <summary>Vérifie une clé avant de l'enregistrer (une petite recherche).</summary>
    Task<ArtworkError> CheckKeyAsync(string key, CancellationToken cancellationToken = default);
}
