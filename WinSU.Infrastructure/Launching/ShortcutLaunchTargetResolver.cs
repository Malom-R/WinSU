using WinSU.Core.Launching;
using WinSU.Infrastructure.Win32;
using Microsoft.Extensions.Logging;

namespace WinSU.Infrastructure.Launching;

/// <summary>
/// Détermine quoi lancer à partir d'un fichier de la bibliothèque (D-6, D-8) :
/// un raccourci est relu à chaque lancement (cible, arguments, dossier), puis ramené au cas du programme visé. Un jeu Steam
/// (raccourci Internet <c>steam://rungameid/…</c>, ou raccourci vers <c>steam.exe -applaunch …</c>) est reconnu et suivi par
/// Steam (étape 35, D-177).
/// </summary>
public sealed class ShortcutLaunchTargetResolver(ILogger<ShortcutLaunchTargetResolver> logger) : ILaunchTargetResolver
{
    public ResolveResult Resolve(string sourcePath)
    {
        if (!File.Exists(sourcePath))
            return new ResolveResult.SourceMissing(sourcePath);

        var extension = Path.GetExtension(sourcePath);
        if (string.Equals(extension, ".url", StringComparison.OrdinalIgnoreCase))
            return new ResolveResult.Resolved(ForInternetShortcut(sourcePath));

        if (!string.Equals(extension, ".lnk", StringComparison.OrdinalIgnoreCase))
            return new ResolveResult.Resolved(ForFile(sourcePath, string.Empty, Path.GetDirectoryName(sourcePath)));

        var link = ShellLinkFile.Read(sourcePath);
        if (string.IsNullOrWhiteSpace(link.TargetPath))
        {
            // Raccourci vers un objet du shell (application du Store…) : seul Windows sait l'ouvrir.
            logger.LogInformation("Raccourci sans chemin de fichier, lancé par le shell : {Path}", sourcePath);
            return new ResolveResult.Resolved(new LaunchTarget(sourcePath, string.Empty, null, LaunchMode.ShellOnly));
        }

        if (!File.Exists(link.TargetPath) && !Directory.Exists(link.TargetPath))
            return new ResolveResult.TargetMissing(sourcePath, link.TargetPath);

        var workingDirectory = string.IsNullOrWhiteSpace(link.WorkingDirectory)
            ? Path.GetDirectoryName(link.TargetPath)
            : link.WorkingDirectory;
        if (string.Equals(Path.GetFileName(link.TargetPath), "steam.exe", StringComparison.OrdinalIgnoreCase)
            && SteamGames.TryGetAppId(link.Arguments, out var appId))
            return new ResolveResult.Resolved(new LaunchTarget(link.TargetPath, link.Arguments, workingDirectory, LaunchMode.Steam, appId));

        return new ResolveResult.Resolved(ForFile(link.TargetPath, link.Arguments, workingDirectory));
    }

    /// <summary>
    /// Raccourci Internet (.url) : un jeu Steam (<c>steam://rungameid/…</c>) est suivi par Steam ; toute autre adresse est
    /// confiée à Windows, sans suivi (la session finit au retour de l'utilisateur dans WinSU).
    /// </summary>
    internal static LaunchTarget ForInternetShortcut(string path)
    {
        var folder = Path.GetDirectoryName(path);
        return SteamGames.TryGetAppId(InternetShortcutUrl(path), out var appId)
            ? new LaunchTarget(path, string.Empty, folder, LaunchMode.Steam, appId)
            : new LaunchTarget(path, string.Empty, folder, LaunchMode.ShellOnly);
    }

    /// <summary>L'adresse d'un raccourci Internet (ligne « URL= »), ou null si elle manque ou si le fichier est illisible.</summary>
    private static string? InternetShortcutUrl(string path)
    {
        try
        {
            return File.ReadLines(path)
                .Select(line => line.Trim())
                .FirstOrDefault(line => line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))?[4..];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Programme (.exe) et script (.bat, .cmd) : suivis ; tout le reste passe par le shell (sans suivi : la session finit au
    /// retour de l'utilisateur dans WinSU).
    /// </summary>
    internal static LaunchTarget ForFile(string file, string arguments, string? workingDirectory)
    {
        switch (Path.GetExtension(file).ToLowerInvariant())
        {
            case ".exe" or ".com":
                return new LaunchTarget(file, arguments, workingDirectory, LaunchMode.Tracked);

            case ".bat" or ".cmd":
                // cmd /c ""C:\chemin\script.bat" arguments" : les guillemets extérieurs protègent la ligne entière.
                var commandProcessor = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
                var command = string.IsNullOrWhiteSpace(arguments) ? $"\"{file}\"" : $"\"{file}\" {arguments}";
                return new LaunchTarget(commandProcessor, $"/c \"{command}\"", workingDirectory, LaunchMode.Tracked);

            default:
                return new LaunchTarget(file, arguments, workingDirectory, LaunchMode.ShellOnly);
        }
    }
}
