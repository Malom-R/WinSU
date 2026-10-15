namespace WinSU.Infrastructure.Library;

/// <summary>
/// Étape 35 (D-175) : la notion d'application (onglet Apps) est retirée. Les applications d'un ancien <c>library.json</c>
/// deviennent des jeux de la console « windows » (même identifiant : le temps de jeu suit), et leurs images passent de
/// <c>media\apps\</c> à <c>media\windows\</c>.
/// </summary>
internal static class LegacyApps
{
    /// <summary>La console des programmes de Windows (catalogue de WinSU).</summary>
    public const string WindowsConsoleId = "windows";

    /// <summary>Ancien dossier des images des applications, sous <c>media</c>.</summary>
    public const string MediaFolder = "apps";

    /// <summary>
    /// Range les applications du document dans la console « windows » (ajoutée à la fin si besoin) ; un programme déjà
    /// présent dans cette console, ou un identifiant déjà pris par un jeu, n'est pas ajouté une seconde fois. Le document
    /// n'a plus d'applications ensuite. Renvoie le nombre d'applications devenues des jeux.
    /// </summary>
    public static int Convert(LibraryDocument document)
    {
        var apps = (document.Apps ?? []).Where(a => !string.IsNullOrWhiteSpace(a.SourcePath)).ToList();
        document.Apps = null;
        if (apps.Count == 0)
            return 0;

        var windows = document.Consoles.FirstOrDefault(c => string.Equals(c.ConsoleId, WindowsConsoleId, StringComparison.OrdinalIgnoreCase));
        var ids = document.Consoles.SelectMany(c => c.Games).Select(g => g.Id).ToHashSet();
        var paths = (windows?.Games ?? []).Select(g => g.SourcePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var moved = apps.Where(a => (a.Id == Guid.Empty || ids.Add(a.Id)) && paths.Add(a.SourcePath)).ToList();
        if (moved.Count == 0)
            return 0;

        if (windows is null)
        {
            windows = new LibraryConsoleEntry { ConsoleId = WindowsConsoleId, AddedAt = moved.Min(a => a.AddedAt) };
            document.Consoles.Add(windows);
        }

        windows.Games.AddRange(moved);
        return moved.Count;
    }

    /// <summary>
    /// Déplace les images de <c>media\apps\&lt;sorte&gt;\</c> vers <c>media\windows\&lt;sorte&gt;\</c> (une image déjà là
    /// l'emporte et l'ancienne reste), puis retire les dossiers devenus vides. Renvoie le nombre d'images déplacées.
    /// </summary>
    public static int MoveMedia(string mediaDirectory)
    {
        var source = Path.Combine(mediaDirectory, MediaFolder);
        if (!Directory.Exists(source))
            return 0;

        var moved = 0;
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).ToList())
        {
            var target = Path.Combine(mediaDirectory, WindowsConsoleId, Path.GetRelativePath(source, file));
            if (File.Exists(target))
                continue;

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Move(file, target);
            moved++;
        }

        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length).Append(source))
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
                Directory.Delete(directory);

        return moved;
    }
}
