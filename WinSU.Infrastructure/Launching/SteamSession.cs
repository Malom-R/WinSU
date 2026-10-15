using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using WinSU.Core.Launching;

namespace WinSU.Infrastructure.Launching;

/// <summary>Ce que Steam dit de ses jeux en cours (étape 35, D-177).</summary>
internal interface ISteamState
{
    bool IsRunning(uint appId);

    /// <summary>Un guet des écritures de Steam, armé tout de suite : à prendre avant de lire <see cref="IsRunning"/>.</summary>
    ISteamWatch Watch();
}

/// <summary>Attend que Steam écrive dans son état (un jeu démarre ou se ferme).</summary>
internal interface ISteamWatch : IDisposable
{
    /// <summary>Windows prévient d'une écriture ; sinon, seul le regard régulier la voit.</summary>
    bool IsNotified { get; }

    /// <summary>Attend la prochaine écriture de Steam, au plus <paramref name="timeout"/> (le regard de secours).</summary>
    Task WaitAsync(TimeSpan timeout, CancellationToken token);
}

/// <summary>
/// Lu dans le registre de l'utilisateur, là où Steam l'écrit : <c>HKCU\Software\Valve\Steam\Apps\&lt;numéro&gt;\Running</c>
/// (1 pendant le jeu) et <c>RunningAppID</c> (le jeu en cours, 0 sinon). Sans Steam : aucun jeu en cours.
/// </summary>
internal sealed class RegistrySteamState(string keyPath = RegistrySteamState.SteamKey) : ISteamState
{
    private const string SteamKey = @"Software\Valve\Steam";

    public bool IsRunning(uint appId)
    {
        using var steam = Registry.CurrentUser.OpenSubKey(keyPath);
        if (steam is null)
            return false;

        if (steam.GetValue("RunningAppID") is int running && unchecked((uint)running) == appId)
            return true;

        using var app = steam.OpenSubKey($@"Apps\{appId}");
        return app?.GetValue("Running") is int value && value != 0;
    }

    public ISteamWatch Watch() => (ISteamWatch?)RegistrySteamWatch.TryOpen(keyPath) ?? new TimerSteamWatch();
}

/// <summary>
/// Windows prévient dès que Steam écrit sous sa clé, sous-clés comprises (<c>RegNotifyChangeKeyValue</c>, réarmé à chaque
/// réveil) : WinSU revient dès que Steam dit le jeu fermé, sans attendre le regard suivant (35.7, demande de l'utilisateur :
/// jusqu'à une seconde de plus avec un regard par seconde).
/// </summary>
internal sealed partial class RegistrySteamWatch : ISteamWatch
{
    private const uint ChangeName = 0x1;
    private const uint ChangeLastSet = 0x4;

    /// <summary>L'avis ne dépend pas du fil qui l'a demandé (les attentes reprennent sur n'importe quel fil).</summary>
    private const uint ThreadAgnostic = 0x10000000;

    private readonly RegistryKey _key;
    private readonly AutoResetEvent _changed = new(initialState: false);

    private RegistrySteamWatch(RegistryKey key) => _key = key;

    public bool IsNotified => true;

    /// <summary>Le guet de la clé <paramref name="keyPath"/> de l'utilisateur ; null si elle n'existe pas ou si Windows refuse.</summary>
    public static RegistrySteamWatch? TryOpen(string keyPath)
    {
        if (Registry.CurrentUser.OpenSubKey(keyPath) is not { } key)
            return null;

        var watch = new RegistrySteamWatch(key);
        if (watch.Arm())
            return watch;

        watch.Dispose();
        return null;
    }

    public async Task WaitAsync(TimeSpan timeout, CancellationToken token)
    {
        var woken = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var wait = ThreadPool.RegisterWaitForSingleObject(_changed, (_, timedOut) => woken.TrySetResult(!timedOut), null, timeout, executeOnlyOnce: true);
        try
        {
            // Prévenu : l'avis est consommé, on le redemande ; au bout du délai, il attend encore.
            if (await woken.Task.WaitAsync(token).ConfigureAwait(false))
                Arm();
        }
        finally
        {
            wait.Unregister(null);
        }
    }

    // La clé d'abord : la fermer réveille l'avis en attente, qui doit encore trouver son événement.
    public void Dispose()
    {
        _key.Dispose();
        _changed.Dispose();
    }

    private bool Arm() =>
        RegNotifyChangeKeyValue(_key.Handle, watchSubtree: true, ChangeName | ChangeLastSet | ThreadAgnostic, _changed.SafeWaitHandle, asynchronous: true) == 0;

    [LibraryImport("advapi32.dll")]
    private static partial int RegNotifyChangeKeyValue(
        Microsoft.Win32.SafeHandles.SafeRegistryHandle key,
        [MarshalAs(UnmanagedType.Bool)] bool watchSubtree,
        uint notifyFilter,
        Microsoft.Win32.SafeHandles.SafeWaitHandle changed,
        [MarshalAs(UnmanagedType.Bool)] bool asynchronous);
}

/// <summary>Sans avis de Windows (Steam absent, avis refusé) : seul le regard régulier voit les écritures.</summary>
internal sealed class TimerSteamWatch : ISteamWatch
{
    public bool IsNotified => false;

    public Task WaitAsync(TimeSpan timeout, CancellationToken token) => Task.Delay(timeout, token);

    public void Dispose()
    {
    }
}

/// <summary>Délais du suivi d'un jeu Steam.</summary>
internal sealed record SteamTimings(TimeSpan StartTimeout, TimeSpan PollInterval)
{
    /// <summary>
    /// Steam peut d'abord mettre le jeu à jour ou afficher une fenêtre : 2 minutes pour démarrer ; Windows prévient de chaque
    /// écriture de Steam (35.7), un regard par seconde reste en secours.
    /// </summary>
    public static readonly SteamTimings Default = new(TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(1));
}

/// <summary>
/// Un jeu lancé par Steam (étape 35, D-177) : la session commence quand Steam le dit « en cours » et finit quand il ne l'est
/// plus, comme un programme suivi. S'il ne démarre pas à temps (Steam fermé, jeu désinstallé, longue mise à jour), elle
/// redevient non suivie : elle finit au retour de l'utilisateur dans WinSU, comme avant.
/// </summary>
internal sealed class SteamSession : IRunningSession
{
    private readonly CancellationTokenSource _stop = new();
    private volatile bool _started;

    public SteamSession(uint appId, ISteamState steam, SteamTimings timings, ILogger logger) =>
        Completion = FollowAsync(appId, steam, timings, logger, _stop.Token);

    public int? ProcessId => null;

    public Task<SessionExit> Completion { get; }

    /// <summary>Le jeu a démarré (Steam l'a dit en cours) : une fin rapide est alors une vraie fermeture, pas un relais.</summary>
    public bool HeldForeground => _started;

    public void Dispose() => _stop.Cancel();

    private async Task<SessionExit> FollowAsync(uint appId, ISteamState steam, SteamTimings timings, ILogger logger, CancellationToken token)
    {
        try
        {
            // Armé avant la première lecture : une écriture de Steam entre la lecture et l'attente n'est pas manquée
            using var watch = steam.Watch();
            if (watch.IsNotified)
                logger.LogInformation("Jeu Steam {AppId} : Windows prévient des écritures de Steam", appId);
            else
                logger.LogInformation("Jeu Steam {AppId} : pas d'avis de Windows, un regard toutes les {Ms} ms", appId, timings.PollInterval.TotalMilliseconds);

            var startedAt = Stopwatch.GetTimestamp();
            while (!steam.IsRunning(appId))
            {
                var waited = Stopwatch.GetElapsedTime(startedAt);
                if (waited >= timings.StartTimeout)
                {
                    logger.LogInformation("Jeu Steam {AppId} : pas démarré après {Seconds} s ; fin de session au retour dans WinSU", appId, waited.TotalSeconds);
                    return SessionExit.NotTracked;
                }

                await watch.WaitAsync(timings.PollInterval, token).ConfigureAwait(false);
            }

            _started = true;
            logger.LogInformation("Jeu Steam {AppId} en cours", appId);
            while (steam.IsRunning(appId))
                await watch.WaitAsync(timings.PollInterval, token).ConfigureAwait(false);

            logger.LogInformation("Jeu Steam {AppId} fermé", appId);
            return SessionExit.AllProcessesExited;
        }
        catch (OperationCanceledException)
        {
            return SessionExit.NotTracked;
        }
    }
}
