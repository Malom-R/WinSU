using WinSU.Core.Library;
using WinSU.Core.Media;
using WinSU.Core.Storage;

namespace WinSU.Infrastructure.Media;

/// <summary>
/// <see cref="ICustomArtStore"/> dans le dossier de données : <c>media\&lt;console&gt;\covers\</c> pour un jeu ; <c>heroes</c> au lieu de
/// <c>covers</c> pour les bannières héro, <c>logos</c> pour les logos (étape 33). Le nom du fichier est celui de l'entrée, avec les caractères interdits remplacés par « _ ».
/// </summary>
public sealed class CustomArtStore(IDataPaths paths) : ICustomArtStore
{
    public const string CoversFolder = "covers";
    public const string HeroesFolder = "heroes";
    public const string LogosFolder = "logos";

    private static readonly string[] ReservedNames =
        ["CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
         "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"];

    /// <summary>Formats lus par Skia ; <c>.ico</c> : Skia prend l'image la plus grande de l'icône.</summary>
    public IReadOnlyList<string> Extensions { get; } = [".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp", ".ico"];

    public string? Find(LibraryGame entry, ArtKind kind = ArtKind.Cover) => Variants(entry, entry.Name, kind).FirstOrDefault(File.Exists);

    public string Save(LibraryGame entry, string imagePath, ArtKind kind = ArtKind.Cover)
    {
        var source = Path.GetFullPath(imagePath);
        var extension = Path.GetExtension(source).ToLowerInvariant();
        if (!Extensions.Contains(extension))
            throw new ArgumentException($"Type d'image non pris en charge : {extension}", nameof(imagePath));

        var directory = DirectoryFor(entry, kind);
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, FileStem(entry.Name) + extension);
        if (!string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
        {
            // Copie puis remplacement : une copie interrompue ne laisse jamais une image tronquée à sa place.
            var temp = target + ".tmp";
            File.Copy(source, temp, overwrite: true);
            File.Move(temp, target, overwrite: true);
        }

        foreach (var other in Variants(entry, entry.Name, kind).Where(p => !string.Equals(p, target, StringComparison.OrdinalIgnoreCase)))
            File.Delete(other);

        return target;
    }

    public bool Remove(LibraryGame entry, ArtKind kind = ArtKind.Cover)
    {
        var removed = false;
        foreach (var path in Variants(entry, entry.Name, kind).Where(File.Exists))
        {
            File.Delete(path);
            removed = true;
        }

        return removed;
    }

    public void Rename(LibraryGame entry, string newName)
    {
        var newStem = FileStem(newName);
        if (string.Equals(FileStem(entry.Name), newStem, StringComparison.OrdinalIgnoreCase))
            return; // même fichier pour Windows, casse comprise

        foreach (var kind in Enum.GetValues<ArtKind>())
        {
            if (Variants(entry, newName, kind).Any(File.Exists))
                continue; // une image de cette sorte porte déjà le nouveau nom : elle l'emporte

            foreach (var path in Variants(entry, entry.Name, kind).Where(File.Exists))
                File.Move(path, Path.Combine(Path.GetDirectoryName(path)!, newStem + Path.GetExtension(path)));
        }
    }

    /// <summary>« Zelda: Ocarina » → « Zelda_ Ocarina » ; un nom réservé de Windows (« CON ») reçoit un « _ ».</summary>
    internal static string FileStem(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var stem = new string([.. name.Trim().Select(c => invalid.Contains(c) ? '_' : c)]).TrimEnd('.', ' ');
        if (stem.Length == 0)
            return "_";
        return ReservedNames.Contains(stem, StringComparer.OrdinalIgnoreCase) ? stem + "_" : stem;
    }

    private string DirectoryFor(LibraryGame entry, ArtKind kind)
    {
        var folder = kind switch
        {
            ArtKind.Hero => HeroesFolder,
            ArtKind.Logo => LogosFolder,
            _ => CoversFolder,
        };
        return Path.Combine(paths.MediaDirectory, FileStem(entry.ConsoleId), folder);
    }

    private IEnumerable<string> Variants(LibraryGame entry, string name, ArtKind kind)
    {
        var directory = DirectoryFor(entry, kind);
        var stem = FileStem(name);
        return Extensions.Select(extension => Path.Combine(directory, stem + extension));
    }
}
