using System.Text.Json.Serialization;

namespace WinSU.Infrastructure.Media;

// Réponses de l'API SteamGridDB v2 (étape 31) : { "success": true, "data": … } ou { "success": false, "errors": […] }.
// Seuls les champs utiles sont lus ; les autres sont ignorés. Champs en snake_case (release_date).

internal sealed class SgdbEnvelope<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }
}

internal sealed class SgdbGame
{
    public int Id { get; set; }
    public string? Name { get; set; }

    /// <summary>Date de sortie en secondes depuis 1970, si le site la connaît.</summary>
    public long? ReleaseDate { get; set; }

    public bool Verified { get; set; }
}

internal sealed class SgdbImage
{
    public int Id { get; set; }
    public int Score { get; set; }
    public string? Url { get; set; }
    public string? Thumb { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public SgdbAuthor? Author { get; set; }
}

internal sealed class SgdbAuthor
{
    public string? Name { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(SgdbEnvelope<List<SgdbGame>>))]
[JsonSerializable(typeof(SgdbEnvelope<List<SgdbImage>>))]
internal sealed partial class SteamGridDbJsonContext : JsonSerializerContext;
