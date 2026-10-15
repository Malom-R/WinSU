using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace WinSU.Infrastructure.Storage;

/// <summary>Lecture et écriture sûres des fichiers JSON de WinSU (réglages, bibliothèque, thèmes).</summary>
internal static class AtomicJsonFile
{
    /// <summary>Écrit dans un fichier temporaire, puis remplace l'original : un plantage ne laisse jamais un fichier à moitié écrit.</summary>
    public static async Task WriteAsync<T>(string path, T value, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempPath = path + ".tmp";

        await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, value, typeInfo, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, path, overwrite: true);
    }

    public static async Task<T?> ReadAsync<T>(string path, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        return await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Lit un fichier de WinSU (réglages, bibliothèque, thèmes ; étape 36). Illisible ou vide (<c>null</c>) : il est mis de
    /// côté (<see cref="Quarantine"/>), <paramref name="onCorrupt"/> reçoit l'erreur et le nouveau chemin (pour le journal),
    /// et la lecture renvoie null.
    /// </summary>
    public static async Task<T?> ReadOrQuarantineAsync<T>(
        string path, JsonTypeInfo<T> typeInfo, TimeProvider time, Action<JsonException, string> onCorrupt, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            return await ReadAsync(path, typeInfo, cancellationToken).ConfigureAwait(false)
                ?? throw new JsonException($"{Path.GetFileName(path)} contient null.");
        }
        catch (JsonException ex)
        {
            onCorrupt(ex, Quarantine(path, time));
            return null;
        }
    }

    /// <summary>Met de côté un fichier illisible (<c>nom.corrupt-AAAAMMJJ-HHmmss</c>) et renvoie son nouveau chemin.</summary>
    public static string Quarantine(string path, TimeProvider time)
    {
        var stamp = time.GetLocalNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var target = $"{path}.corrupt-{stamp}";
        File.Move(path, target, overwrite: true);
        return target;
    }
}
