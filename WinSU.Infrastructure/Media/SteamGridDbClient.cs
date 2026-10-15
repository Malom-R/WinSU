using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using WinSU.Core.Media;
using WinSU.Core.Settings;
using Microsoft.Extensions.Logging;

namespace WinSU.Infrastructure.Media;

/// <summary>
/// Client de l'API SteamGridDB v2 (étape 31, D-114 à D-116), avec la clé de l'utilisateur (Réglages, D-115). La clé n'est
/// envoyée qu'à l'adresse de l'API, jamais au serveur des images, et jamais écrite au journal. Formats d'après la
/// documentation de l'API (notes 31.0) ; une demande de l'utilisateur = une recherche, puis une page d'images.
/// </summary>
public sealed class SteamGridDbClient : IArtworkSearch
{
    public const string DefaultBaseAddress = "https://www.steamgriddb.com/api/v2/";

    public static readonly TimeSpan ApiTimeout = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Taille maximale d'une image téléchargée (une bannière 3840 × 1240 en PNG fait quelques Mo).</summary>
    public const long MaxImageBytes = 25 * 1024 * 1024;

    /// <summary>Jaquettes : carrées d'abord (nos tuiles sont carrées), puis portrait (rognée au carré comme une image choisie).</summary>
    internal static readonly string[] CoverDimensions = ["512x512", "1024x1024", "600x900", "660x930", "342x482"];

    /// <summary>Bannières héro, dans l'ordre de préférence : 1920 × 620 est le format conseillé par WinSU (9.1).</summary>
    internal static readonly string[] HeroDimensions = ["1920x620", "3840x1240", "1600x650"];

    /// <summary>Images fixes seulement, sans contenu adulte, humoristique ni épileptogène (31.3).</summary>
    internal const string Filters = "types=static&mimes=image/png,image/jpeg&nsfw=false&humor=false&epilepsy=false";

    /// <summary>
    /// Logos (étape 33, D-140) : les mêmes filtres, en PNG ou WebP (fond transparent ; le site n'en a pas en JPEG) ; pas de
    /// format imposé, chaque logo a sa forme.
    /// </summary>
    internal const string LogoFilters = "types=static&mimes=image/png,image/webp&nsfw=false&humor=false&epilepsy=false";

    private readonly ISettingsStore _settings;
    private readonly HttpClient _http;
    private readonly Uri _baseAddress;
    private readonly ILogger<SteamGridDbClient> _logger;

    /// <param name="baseAddress">Autre adresse de l'API (faux serveur des essais), sinon <see cref="DefaultBaseAddress"/>.</param>
    public SteamGridDbClient(ISettingsStore settings, HttpClient http, ILogger<SteamGridDbClient> logger, string? baseAddress = null)
    {
        _settings = settings;
        _http = http;
        _logger = logger;
        var address = baseAddress ?? DefaultBaseAddress;
        _baseAddress = new Uri(address.EndsWith('/') ? address : address + "/");
    }

    public bool HasKey => Key is not null;

    private string? Key => _settings.Current.Online.SteamGridDbKey?.Trim() is { Length: > 0 } key ? key : null;

    public async Task<ArtworkResult<IReadOnlyList<ArtworkGame>>> FindGamesAsync(string name, CancellationToken cancellationToken = default)
    {
        if (Key is not { } key)
            return ArtworkResult<IReadOnlyList<ArtworkGame>>.Fail(ArtworkError.NoKey);

        var result = await GetAsync($"search/autocomplete/{Uri.EscapeDataString(name.Trim())}", key, SteamGridDbJsonContext.Default.SgdbEnvelopeListSgdbGame, cancellationToken);
        return result.Succeeded
            ? ArtworkResult<IReadOnlyList<ArtworkGame>>.Ok([.. result.Value!.Where(g => g.Name is not null).Select(ToGame)])
            : ArtworkResult<IReadOnlyList<ArtworkGame>>.Fail(result.Error);
    }

    public async Task<ArtworkResult<IReadOnlyList<ArtworkImage>>> FindImagesAsync(int gameId, ArtKind kind, CancellationToken cancellationToken = default)
    {
        if (Key is not { } key)
            return ArtworkResult<IReadOnlyList<ArtworkImage>>.Fail(ArtworkError.NoKey);

        var path = kind switch
        {
            ArtKind.Hero => $"heroes/game/{gameId}?dimensions={string.Join(',', HeroDimensions)}&{Filters}",
            ArtKind.Logo => $"logos/game/{gameId}?{LogoFilters}",
            _ => $"grids/game/{gameId}?dimensions={string.Join(',', CoverDimensions)}&{Filters}",
        };
        var result = await GetAsync(path, key, SteamGridDbJsonContext.Default.SgdbEnvelopeListSgdbImage, cancellationToken);
        if (!result.Succeeded)
            return ArtworkResult<IReadOnlyList<ArtworkImage>>.Fail(result.Error);

        var images = result.Value!
            .Where(i => i.Url is not null && i.Thumb is not null)
            .Select(i => new ArtworkImage(i.Id, i.Thumb!, i.Url!, i.Width, i.Height, i.Author?.Name));
        // Tri stable : le format d'abord, l'ordre du site (les mieux notées) ensuite
        return ArtworkResult<IReadOnlyList<ArtworkImage>>.Ok([.. images.OrderBy(i => FormatRank(i, kind))]);
    }

    public async Task<ArtworkResult<string>> DownloadAsync(ArtworkImage image, bool thumbnail = false, CancellationToken cancellationToken = default)
    {
        if (!IsAllowedImageAddress(thumbnail ? image.ThumbUrl : image.Url, out var address))
            return ArtworkResult<string>.Fail(ArtworkError.Failed);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(DownloadTimeout);
        string? file = null;
        try
        {
            // Pas de clé ici : les images sont sur un autre serveur que l'API
            using var response = await _http.GetAsync(address, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("SteamGridDB : image {Id} refusée ({Status})", image.Id, (int)response.StatusCode);
                return ArtworkResult<string>.Fail(ErrorFor(response.StatusCode));
            }

            if (ExtensionFor(response.Content.Headers.ContentType?.MediaType, address) is not { } extension
                || response.Content.Headers.ContentLength > MaxImageBytes)
            {
                _logger.LogWarning("SteamGridDB : image {Id} écartée ({Type}, {Length} octets)", image.Id, response.Content.Headers.ContentType?.MediaType, response.Content.Headers.ContentLength);
                return ArtworkResult<string>.Fail(ArtworkError.Failed);
            }

            var folder = Path.Combine(Path.GetTempPath(), "WinSU");
            Directory.CreateDirectory(folder);
            file = Path.Combine(folder, $"steamgriddb-{image.Id}{(thumbnail ? "-thumb" : string.Empty)}{extension}");
            await using (var source = await response.Content.ReadAsStreamAsync(timeout.Token))
            await using (var target = File.Create(file))
            {
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, timeout.Token)) > 0)
                {
                    total += read;
                    if (total > MaxImageBytes)
                        throw new InvalidDataException("Image trop grande");
                    await target.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
                }
            }

            return ArtworkResult<string>.Ok(file);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            if (file is not null)
                TryDelete(file);
            _logger.LogWarning(ex, "SteamGridDB : téléchargement de l'image {Id} impossible", image.Id);
            return ArtworkResult<string>.Fail(ex is HttpRequestException or OperationCanceledException ? ArtworkError.Offline : ArtworkError.Failed);
        }
    }

    public async Task<ArtworkError> CheckKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return ArtworkError.NoKey;

        var result = await GetAsync("search/autocomplete/zelda", key.Trim(), SteamGridDbJsonContext.Default.SgdbEnvelopeListSgdbGame, cancellationToken);
        return result.Error;
    }

    /// <summary>
    /// Une clé ne peut contenir que des caractères ASCII visibles (elle passe dans un en-tête HTTP). Une clé tapée sur un
    /// clavier qui ajoute « ç » ou « é » est refusée tout de suite, sans rien envoyer (vu en essai réel, 31.5).
    /// </summary>
    internal static bool IsWellFormedKey(string key) => key.Length > 0 && key.All(c => c is > ' ' and <= '~');

    /// <summary>Une demande à l'API, avec la clé ; les erreurs deviennent un <see cref="ArtworkError"/> (l'annulation par l'appelant passe).</summary>
    private async Task<ArtworkResult<T>> GetAsync<T>(string path, string key, JsonTypeInfo<SgdbEnvelope<T>> type, CancellationToken cancellationToken)
        where T : class
    {
        if (!IsWellFormedKey(key))
            return ArtworkResult<T>.Fail(ArtworkError.KeyRefused);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ApiTimeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_baseAddress, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        try
        {
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("SteamGridDB : {Path} refusé ({Status})", Endpoint(path), (int)response.StatusCode);
                return ArtworkResult<T>.Fail(ErrorFor(response.StatusCode));
            }

            var envelope = await response.Content.ReadFromJsonAsync(type, timeout.Token);
            if (envelope is not { Success: true, Data: { } data })
            {
                _logger.LogWarning("SteamGridDB : {Path} sans résultat lisible ({Errors})", Endpoint(path), string.Join(", ", envelope?.Errors ?? []));
                return ArtworkResult<T>.Fail(ArtworkError.Failed);
            }

            return ArtworkResult<T>.Ok(data);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("SteamGridDB : {Path} sans réponse en {Seconds} s", Endpoint(path), ApiTimeout.TotalSeconds);
            return ArtworkResult<T>.Fail(ArtworkError.Offline);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("SteamGridDB : {Path} injoignable ({Message})", Endpoint(path), ex.Message);
            return ArtworkResult<T>.Fail(ArtworkError.Offline);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "SteamGridDB : réponse illisible pour {Path}", Endpoint(path));
            return ArtworkResult<T>.Fail(ArtworkError.Failed);
        }
    }

    internal static ArtworkError ErrorFor(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ArtworkError.KeyRefused,
        HttpStatusCode.NotFound => ArtworkError.NotFound,
        HttpStatusCode.TooManyRequests => ArtworkError.TooManyRequests,
        _ => ArtworkError.Failed,
    };

    /// <summary>
    /// Rang du format d'une image : jaquettes carrées (toutes ensemble), puis les autres ; bannières dans l'ordre de
    /// <see cref="HeroDimensions"/>, un format inconnu passe à la fin ; logos dans l'ordre du site.
    /// </summary>
    private static int FormatRank(ArtworkImage image, ArtKind kind)
    {
        if (kind == ArtKind.Logo)
            return 0;
        if (kind == ArtKind.Cover)
            return image.IsSquare ? 0 : 1;

        var index = Array.IndexOf(HeroDimensions, $"{image.Width}x{image.Height}");
        return index >= 0 ? index : HeroDimensions.Length;
    }

    private static ArtworkGame ToGame(SgdbGame game) => new(
        game.Id,
        game.Name!,
        game.ReleaseDate is { } seconds && seconds > 0 ? DateTimeOffset.FromUnixTimeSeconds(seconds).Year : null,
        game.Verified);

    /// <summary>Seulement une adresse https (ou http sur cette machine, pour le faux serveur des essais).</summary>
    internal static bool IsAllowedImageAddress(string url, out Uri address)
    {
        address = null!;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
            return false;

        address = parsed;
        return parsed.Scheme == Uri.UriSchemeHttps || (parsed.Scheme == Uri.UriSchemeHttp && parsed.IsLoopback);
    }

    /// <summary>Extension du fichier d'après le type de la réponse, sinon d'après l'adresse ; null : pas une image reconnue.</summary>
    internal static string? ExtensionFor(string? mediaType, Uri address)
    {
        switch (mediaType?.ToLowerInvariant())
        {
            case "image/png":
                return ".png";
            case "image/jpeg" or "image/jpg":
                return ".jpg";
            case "image/webp":
                return ".webp";
            case null or "application/octet-stream":
                var extension = Path.GetExtension(address.AbsolutePath).ToLowerInvariant();
                return extension is ".png" or ".jpg" or ".jpeg" or ".webp" ? extension : null;
            default:
                return null;
        }
    }

    /// <summary>Le chemin sans la requête (le nom cherché reste : ce n'est pas un secret).</summary>
    private static string Endpoint(string path) => path.Split('?')[0];

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (IOException)
        {
        }
    }
}
