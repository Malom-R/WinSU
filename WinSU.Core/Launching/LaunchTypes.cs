namespace WinSU.Core.Launching;

public enum LaunchMode
{
    /// <summary>Programme lancé directement : WinSU suit tous ses processus (Job Object) et sait quand le jeu se ferme.</summary>
    Tracked,

    /// <summary>Lancé par le shell Windows (fichier non exécutable, raccourci sans chemin) : pas de suivi possible.</summary>
    ShellOnly,

    /// <summary>
    /// Jeu Steam (<c>steam://rungameid/…</c>, <c>steam.exe -applaunch …</c>) : lancé par Windows, suivi par l'état de Steam
    /// (étape 35, D-177) : la session dure tant que Steam dit le jeu « en cours ».
    /// </summary>
    Steam,
}

/// <summary>Ce qu'il faut réellement lancer, calculé à chaque lancement à partir du fichier ajouté.</summary>
/// <param name="SteamAppId">Numéro du jeu Steam, en mode <see cref="LaunchMode.Steam"/>.</param>
public sealed record LaunchTarget(string FileName, string Arguments, string? WorkingDirectory, LaunchMode Mode, uint? SteamAppId = null);

/// <summary>Résultat de la résolution d'un fichier de la bibliothèque.</summary>
public abstract record ResolveResult
{
    private ResolveResult()
    {
    }

    public sealed record Resolved(LaunchTarget Target) : ResolveResult;

    /// <summary>Le fichier ajouté (raccourci ou programme) n'existe plus (D-9).</summary>
    public sealed record SourceMissing(string SourcePath) : ResolveResult;

    /// <summary>Le raccourci existe, mais le programme qu'il vise a disparu (émulateur désinstallé…).</summary>
    public sealed record TargetMissing(string SourcePath, string TargetPath) : ResolveResult;
}

public interface ILaunchTargetResolver
{
    ResolveResult Resolve(string sourcePath);
}

public enum SessionExit
{
    /// <summary>Tous les processus lancés (et leurs enfants) se sont terminés.</summary>
    AllProcessesExited,

    /// <summary>Lancement confié au shell : on ne sait pas quand il se termine.</summary>
    NotTracked,
}

/// <summary>Un jeu en cours. Libérer la session ne ferme pas le jeu.</summary>
public interface IRunningSession : IDisposable
{
    int? ProcessId { get; }

    Task<SessionExit> Completion { get; }

    /// <summary>
    /// Vrai si une fenêtre d'un des processus suivis a été au premier plan : le programme s'est bien affiché.
    /// Une fin rapide est alors une vraie fermeture, et non un relais vers un autre lanceur (Steam, application du Store).
    /// </summary>
    bool HeldForeground { get; }
}

public interface IProcessLauncher
{
    /// <summary>Lance la cible. Lève une <see cref="System.ComponentModel.Win32Exception"/> si Windows refuse.</summary>
    IRunningSession Start(LaunchTarget target);
}
