using System.Text.Json;
using WinSU.Infrastructure.Storage;

namespace WinSU.Infrastructure.Catalog;

/// <summary>Lecture et écriture de <see cref="CatalogDocument"/>, avec les mêmes options JSON que les réglages.</summary>
public static class CatalogSerializer
{
    public static async Task<CatalogDocument> ReadAsync(Stream stream, CancellationToken cancellationToken = default) =>
        await JsonSerializer.DeserializeAsync(stream, WinSUJsonContext.Default.CatalogDocument, cancellationToken).ConfigureAwait(false)
        ?? throw new JsonException("Le catalogue contient null.");

    public static Task WriteAsync(Stream stream, CatalogDocument document, CancellationToken cancellationToken = default) =>
        JsonSerializer.SerializeAsync(stream, document, WinSUJsonContext.Default.CatalogDocument, cancellationToken);
}
