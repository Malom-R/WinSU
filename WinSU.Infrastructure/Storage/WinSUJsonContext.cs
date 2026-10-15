using System.Text.Json;
using System.Text.Json.Serialization;
using WinSU.Core.Settings;
using WinSU.Infrastructure.Catalog;
using WinSU.Infrastructure.Library;

namespace WinSU.Infrastructure.Storage;

/// <summary>
/// Sérialisation générée à la compilation pour les fichiers JSON de WinSU.
/// Propriétés en camelCase, enums écrits par leur nom, commentaires tolérés à la lecture.
/// Un <c>null</c> dans une propriété non nullable est refusé : le fichier est alors traité comme illisible.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    RespectNullableAnnotations = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(CatalogDocument))]
[JsonSerializable(typeof(LibraryDocument))]
[JsonSerializable(typeof(ThemesDocument))]
internal sealed partial class WinSUJsonContext : JsonSerializerContext;
