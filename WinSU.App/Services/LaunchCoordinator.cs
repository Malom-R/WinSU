using System.ComponentModel;
using System.Runtime.InteropServices;
using WinSU.App.Localization;
using WinSU.App.Overlays;
using WinSU.Core.Audio;
using WinSU.Core.Launching;
using WinSU.Core.Library;
using WinSU.Core.Playtime;
using WinSU.Core.Settings;
using Microsoft.Extensions.Logging;

namespace WinSU.App.Services;

/// <summary>
/// Déroulé d'un lancement (notes 3.7d), pour un jeu comme pour une application : vérifier que le fichier existe
/// (sinon popup D-9), relire le raccourci, lancer en suivant tous les processus (D-8), réduire WinSU, attendre la fin,
/// revenir au premier plan, enregistrer la session (temps de jeu) et la date de dernier lancement.
/// </summary>
public sealed class LaunchCoordinator(
    ILibraryService library,
    ILaunchTargetResolver resolver,
    IProcessLauncher launcher,
    IPlaytimeService playtime,
    IOverlayService overlays,
    IAudioService audio,
    ISettingsStore settings,
    IWindowControl window,
    TimeProvider time,
    ILogger<LaunchCoordinator> logger)
{
    private bool _isRunning;
    private TaskCompletionSource? _stopFollowing;

    /// <summary>
    /// Un jeu lancé depuis WinSU est en cours : WinSU n'accepte alors aucune entrée (D-182, <see cref="Input.InputRouter"/>,
    /// la fenêtre) jusqu'à la fin du jeu et son retour au premier plan.
    /// </summary>
    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (_isRunning == value)
                return;
            _isRunning = value;
            RunningChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Un lancement commence ou se termine (le fond animé s'arrête, puis reprend).</summary>
    public event EventHandler? RunningChanged;

    /// <summary>Déclenché après l'enregistrement d'une session (les écrans rafraîchissent le temps de jeu).</summary>
    public event EventHandler<PlaySession>? SessionEnded;

    /// <summary>
    /// « Reprendre WinSU » (D-182, le bouton de la barre d'aide pendant un jeu) : WinSU cesse de suivre le jeu en cours, qui
    /// reste ouvert (un programme du jeu qui ne se ferme jamais le tiendrait sinon sourd) ; la session finit là.
    /// </summary>
    public void StopFollowing() => _stopFollowing?.TrySetResult();

    public async Task LaunchAsync(LibraryGame entry)
    {
        try
        {
            await LaunchCoreAsync(entry);
        }
        catch (Exception ex)
        {
            IsRunning = false;
            logger.LogError(ex, "Erreur pendant le lancement de {Entry}", entry.Name);
        }
    }

    private async Task LaunchCoreAsync(LibraryGame entry)
    {
        if (IsRunning)
        {
            await overlays.AlertAsync(Tr.Get("win_launch_busy_title"), Tr.Get("win_launch_busy_message"), Tr.Get("win_ok"));
            return;
        }

        ResolveResult resolved;
        try
        {
            resolved = resolver.Resolve(entry.SourcePath);
        }
        catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Raccourci illisible : {Path}", entry.SourcePath);
            await overlays.AlertAsync(Tr.Format("win_launch_failed_title", entry.Name), Tr.Format("win_launch_unreadable", entry.SourcePath), Tr.Get("win_ok"));
            return;
        }

        switch (resolved)
        {
            case ResolveResult.SourceMissing:
                await OfferRemovalAsync(entry, Tr.Format("win_launch_missing_message", entry.SourcePath));
                break;
            case ResolveResult.TargetMissing missing:
                await OfferRemovalAsync(entry, Tr.Format("win_launch_target_missing_message", missing.TargetPath));
                break;
            case ResolveResult.Resolved ok:
                await RunAsync(entry, ok.Target);
                break;
        }
    }

    /// <summary>D-9 : fichier introuvable → proposer de retirer l'entrée (le focus démarre sur « Garder »).</summary>
    private async Task OfferRemovalAsync(LibraryGame entry, string message)
    {
        logger.LogInformation("Introuvable au lancement : {Entry} ({Path})", entry.Name, entry.SourcePath);
        var remove = await overlays.ConfirmAsync(
            Tr.Format("win_launch_missing_title", entry.Name), message, Tr.Get("win_console_remove"), Tr.Get("win_launch_keep"));
        if (remove)
            await library.RemoveEntryAsync(entry.Id);
    }

    private async Task RunAsync(LibraryGame entry, LaunchTarget target)
    {
        audio.Play(UiSound.LaunchGame);
        IRunningSession session;
        try
        {
            session = launcher.Start(target);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            logger.LogWarning(ex, "Lancement refusé : {Entry}", entry.Name);
            await overlays.AlertAsync(Tr.Format("win_launch_failed_title", entry.Name), ex.Message, Tr.Get("win_ok"));
            return;
        }

        var stop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _stopFollowing = stop;
        IsRunning = true;
        var startedAt = time.GetUtcNow();
        var launch = settings.Current.Launch;
        SessionEndReason reason;
        try
        {
            // Réduit (par défaut), ou en veille derrière le jeu (D-181) : défait avant le retour au premier plan
            using (launch.MinimizeWhileRunning ? window.MinimizeWhenDeactivated() : window.RestWhenDeactivated())
            using (session)
            {
                if (await Task.WhenAny(session.Completion, stop.Task) == stop.Task)
                {
                    logger.LogInformation("{Entry} : suivi arrêté par l'utilisateur (Reprendre WinSU), le jeu reste ouvert", entry.Name);
                    reason = SessionEndReason.UserReturned;
                }
                else
                {
                    var exit = await session.Completion;
                    var elapsed = time.GetUtcNow() - startedAt;

                    // Fin quasi immédiate d'un programme qui ne s'est jamais affiché : c'était un relais (Steam, lanceur
                    // d'application du Store…) et non le jeu lui-même. La session durera jusqu'au retour de l'utilisateur dans
                    // WinSU (ou jusqu'à « Reprendre WinSU »). S'il s'est affiché, c'est une vraie fermeture (essai rapide) :
                    // retour à WinSU, quelle que soit la durée.
                    var handedOff = elapsed < TimeSpan.FromSeconds(launch.ShellOnlyGraceSeconds) && !session.HeldForeground;
                    if (exit == SessionExit.NotTracked || handedOff)
                    {
                        logger.LogInformation("{Entry} : suivi impossible ou relais vers un autre programme ; fin de session au retour dans WinSU", entry.Name);
                        using var cancel = new CancellationTokenSource();
                        await Task.WhenAny(window.WaitForActivationAsync(cancel.Token), stop.Task);
                        await cancel.CancelAsync();
                        reason = SessionEndReason.UserReturned;
                    }
                    else
                    {
                        reason = SessionEndReason.Exited;
                    }
                }
            }

            if (reason == SessionEndReason.Exited && !window.BringToFront())
                logger.LogWarning("Windows a refusé de remettre WinSU au premier plan après {Entry}", entry.Name);
        }
        finally
        {
            _stopFollowing = null;
            IsRunning = false;
        }

        var finished = new PlaySession(entry.Id, startedAt, time.GetUtcNow(), reason);
        await playtime.RecordAsync(finished);
        await library.MarkPlayedAsync(entry.Id, startedAt);
        SessionEnded?.Invoke(this, finished);
    }
}
