using System.Runtime.InteropServices;

namespace WinSU.Infrastructure.Runtime;

/// <summary>
/// Garantit une seule instance de WinSU par session Windows. Une seconde instance ne démarre pas :
/// elle signale la première, qui revient au premier plan.
/// </summary>
/// <remarks>
/// Le Mutex appartient au thread qui l'a créé : <see cref="Dispose"/> doit être appelé sur ce même thread.
/// </remarks>
public sealed partial class SingleInstanceGuard : IDisposable
{
    public const string DefaultName = "WinSU.Launcher";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activateSignal;
    private RegisteredWaitHandle? _registration;
    private bool _disposed;

    private SingleInstanceGuard(Mutex mutex, EventWaitHandle activateSignal)
    {
        _mutex = mutex;
        _activateSignal = activateSignal;
    }

    /// <summary>Tente de devenir l'instance principale. Renvoie null si une instance tourne déjà.</summary>
    public static SingleInstanceGuard? TryAcquire(string name = DefaultName)
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName(name), out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            return null;
        }

        var signal = new EventWaitHandle(false, EventResetMode.AutoReset, SignalName(name));
        return new SingleInstanceGuard(mutex, signal);
    }

    /// <summary>
    /// Demande à l'instance principale de revenir au premier plan. Appelé par la seconde instance,
    /// qui a reçu le droit de premier plan (lancée par l'utilisateur) et le cède à la première.
    /// </summary>
    public static bool SignalExistingInstance(string name = DefaultName)
    {
        if (!EventWaitHandle.TryOpenExisting(SignalName(name), out var signal))
            return false;

        using (signal)
        {
            AllowSetForegroundWindow(AsfwAny);
            return signal.Set();
        }
    }

    /// <summary>Appelle <paramref name="onActivationRequested"/> (sur un thread du pool) à chaque signal d'une autre instance.</summary>
    public void ListenForActivation(Action onActivationRequested)
    {
        ArgumentNullException.ThrowIfNull(onActivationRequested);
        ObjectDisposedException.ThrowIf(_disposed, this);

        _registration?.Unregister(null);
        _registration = ThreadPool.RegisterWaitForSingleObject(
            _activateSignal,
            (_, _) => onActivationRequested(),
            state: null,
            millisecondsTimeOutInterval: Timeout.Infinite,
            executeOnlyOnce: false);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _registration?.Unregister(null);
        _activateSignal.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }

    private static string MutexName(string name) => $@"Local\{name}.Instance";

    private static string SignalName(string name) => $@"Local\{name}.Activate";

    private const int AsfwAny = -1;

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllowSetForegroundWindow(int processId);
}
