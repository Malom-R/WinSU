using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using WinSU.Core.Launching;
using Microsoft.Extensions.Logging;

namespace WinSU.Infrastructure.Launching;

/// <summary>
/// Lance un jeu et suit <b>tout son arbre de processus</b> (D-8) : le processus est créé suspendu, rattaché à un
/// Job Object, puis relancé ; ses enfants héritent du job. La session se termine quand le job n'a plus aucun processus
/// (message <c>JOB_OBJECT_MSG_ACTIVE_PROCESS_ZERO</c>), ce qui gère les lanceurs qui se ferment après avoir lancé le jeu.
/// Fermer WinSU ne ferme jamais le jeu (pas de <c>KILL_ON_JOB_CLOSE</c>).
/// </summary>
public sealed partial class JobObjectProcessLauncher(ILogger<JobObjectProcessLauncher> logger) : IProcessLauncher
{
    /// <summary>Tests : pas de fenêtre de console pour les programmes en mode console.</summary>
    internal bool HideConsoleWindows { get; init; }

    /// <summary>Tests : remplace l'ouverture par le shell, qui afficherait une fenêtre Windows.</summary>
    internal Func<ProcessStartInfo, Process?> ShellStart { get; init; } = Process.Start;

    /// <summary>État des jeux Steam (étape 35, D-177) ; tests : un état simulé.</summary>
    internal ISteamState Steam { get; init; } = new RegistrySteamState();

    /// <summary>Délais du suivi d'un jeu Steam ; tests : plus courts.</summary>
    internal SteamTimings SteamTimings { get; init; } = SteamTimings.Default;

    public IRunningSession Start(LaunchTarget target)
    {
        // WinSU est au premier plan (l'utilisateur vient de valider) : il cède ce droit au jeu et aux programmes qu'il lance
        // (script → émulateur, lanceur → jeu), sinon leur fenêtre peut s'ouvrir derrière. Le droit tombe à la prochaine entrée.
        AllowSetForegroundWindow(AsfwAny);

        if (target is { Mode: LaunchMode.Steam, SteamAppId: { } appId })
            return StartSteam(target, appId);

        if (target.Mode is LaunchMode.ShellOnly or LaunchMode.Steam)
            return StartWithShell(target);

        var job = CreateJobObjectW(IntPtr.Zero, IntPtr.Zero);
        var port = CreateIoCompletionPort(new IntPtr(-1), IntPtr.Zero, UIntPtr.Zero, 1);
        if (job == IntPtr.Zero || port == IntPtr.Zero)
        {
            CloseHandleIfSet(job);
            CloseHandleIfSet(port);
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Création du Job Object impossible");
        }

        try
        {
            AssociateCompletionPort(job, port);
            var process = CreateSuspended(target);
            var inJob = AssignProcessToJobObject(job, process.hProcess);
            if (!inJob)
            {
                logger.LogWarning(
                    "Impossible de rattacher le processus {Pid} au Job Object (erreur {Error}) : seul ce processus sera suivi",
                    process.dwProcessId, Marshal.GetLastPInvokeError());
            }

            ResumeThread(process.hThread);
            CloseHandle(process.hThread);
            logger.LogInformation("Lancé (suivi) : {File} {Arguments} — PID {Pid}", target.FileName, target.Arguments, process.dwProcessId);
            return new JobSession(job, port, process.hProcess, process.dwProcessId, inJob);
        }
        catch
        {
            CloseHandle(job);
            CloseHandle(port);
            throw;
        }
    }

    private IRunningSession StartWithShell(LaunchTarget target)
    {
        var processId = OpenWithShell(target);
        logger.LogInformation("Lancé par le shell (sans suivi) : {File} {Arguments}", target.FileName, target.Arguments);
        return new UntrackedSession(processId);
    }

    /// <summary>Jeu Steam (D-177) : ouvert par Windows (Steam le lance), puis suivi par ce que Steam dit de lui.</summary>
    private SteamSession StartSteam(LaunchTarget target, uint appId)
    {
        OpenWithShell(target);
        logger.LogInformation("Lancé par Steam (suivi par Steam) : {File} — jeu {AppId}", target.FileName, appId);
        return new SteamSession(appId, Steam, SteamTimings, logger);
    }

    private int? OpenWithShell(LaunchTarget target)
    {
        using var process = ShellStart(new ProcessStartInfo(target.FileName, target.Arguments)
        {
            UseShellExecute = true,
            WorkingDirectory = target.WorkingDirectory ?? string.Empty,
        });
        return process?.Id;
    }

    private unsafe ProcessInformation CreateSuspended(LaunchTarget target)
    {
        // CreateProcess peut modifier la ligne de commande : il lui faut un tampon modifiable, terminé par \0.
        var commandLine = $"\"{target.FileName}\" {target.Arguments}".TrimEnd() + '\0';
        var buffer = commandLine.ToCharArray();
        var startup = new StartupInfo { cb = sizeof(StartupInfo) };
        var workingDirectory = string.IsNullOrWhiteSpace(target.WorkingDirectory) ? null : target.WorkingDirectory + '\0';
        var flags = CreateSuspendedFlag | (HideConsoleWindows ? CreateNoWindow : 0);

        fixed (char* command = buffer)
        fixed (char* directory = workingDirectory)
        {
            // D'abord en sortant du job éventuel de WinSU (lancé depuis un terminal…) pour que le jeu survive à sa fermeture ;
            // si ce job l'interdit, on lance normalement.
            if (CreateProcessW(null, command, IntPtr.Zero, IntPtr.Zero, 0, flags | CreateBreakawayFromJob, IntPtr.Zero, directory, &startup, out var info) != 0)
                return info;

            var error = Marshal.GetLastPInvokeError();
            if (error != ErrorAccessDenied)
                throw new Win32Exception(error, $"Impossible de lancer {target.FileName}");

            if (CreateProcessW(null, command, IntPtr.Zero, IntPtr.Zero, 0, flags, IntPtr.Zero, directory, &startup, out info) != 0)
                return info;

            throw new Win32Exception(Marshal.GetLastPInvokeError(), $"Impossible de lancer {target.FileName}");
        }
    }

    private static unsafe void AssociateCompletionPort(IntPtr job, IntPtr port)
    {
        var association = new AssociateCompletionPortInfo { CompletionKey = job, CompletionPort = port };
        if (!SetInformationJobObject(job, JobObjectAssociateCompletionPortInformation, &association, (uint)sizeof(AssociateCompletionPortInfo)))
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Association du port de complétion impossible");
    }

    private static void CloseHandleIfSet(IntPtr handle)
    {
        if (handle != IntPtr.Zero)
            CloseHandle(handle);
    }

    /// <summary>
    /// Session suivie : un thread attend la fin du job (ou du processus, si le rattachement a échoué), et note au passage
    /// si une fenêtre d'un processus suivi est passée au premier plan (vérifié toutes les <see cref="ForegroundCheckMs"/> ms).
    /// </summary>
    private sealed class JobSession : IRunningSession
    {
        private const uint ForegroundCheckMs = 250;

        private readonly IntPtr _job;
        private readonly IntPtr _port;
        private readonly IntPtr _process;
        private readonly bool _inJob;
        private readonly TaskCompletionSource<SessionExit> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Thread _waiter;
        private volatile bool _heldForeground;
        private int _disposed;

        public JobSession(IntPtr job, IntPtr port, IntPtr process, int processId, bool inJob)
        {
            _job = job;
            _port = port;
            _process = process;
            _inJob = inJob;
            ProcessId = processId;
            _waiter = new Thread(inJob ? WaitForEmptyJob : WaitForProcess) { IsBackground = true, Name = "WinSU session " + processId };
            _waiter.Start();
        }

        public int? ProcessId { get; }

        public Task<SessionExit> Completion => _completion.Task;

        public bool HeldForeground => _heldForeground;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            PostQueuedCompletionStatus(_port, 0, UIntPtr.Zero, IntPtr.Zero); // réveille l'attente (clé 0 = arrêt)
            _waiter.Join(TimeSpan.FromSeconds(2));
            CloseHandle(_process);
            CloseHandle(_port);
            CloseHandle(_job);
            _completion.TrySetCanceled();
        }

        private void WaitForEmptyJob()
        {
            while (true)
            {
                if (!GetQueuedCompletionStatus(_port, out var message, out var key, out var overlapped, ForegroundCheckMs))
                {
                    if (overlapped == IntPtr.Zero && Marshal.GetLastPInvokeError() == WaitTimeout)
                    {
                        CheckForeground();
                        continue;
                    }

                    return; // port fermé
                }

                if (key == UIntPtr.Zero)
                    return; // arrêt demandé par Dispose

                if (message == JobObjectMsgActiveProcessZero)
                {
                    _completion.TrySetResult(SessionExit.AllProcessesExited);
                    return;
                }
            }
        }

        private void WaitForProcess()
        {
            while (WaitForSingleObject(_process, ForegroundCheckMs) == WaitTimeout)
                CheckForeground();

            _completion.TrySetResult(SessionExit.AllProcessesExited);
        }

        private void CheckForeground()
        {
            if (_heldForeground)
                return;

            GetWindowThreadProcessId(GetForegroundWindow(), out var foregroundProcessId);
            if (foregroundProcessId != 0 && Contains((int)foregroundProcessId))
                _heldForeground = true;
        }

        /// <summary>Le processus fait-il partie de la session (du job, ou c'est le processus lancé) ?</summary>
        private bool Contains(int processId)
        {
            if (!_inJob)
                return processId == ProcessId;

            var process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
            if (process == IntPtr.Zero)
                return false;

            try
            {
                return IsProcessInJob(process, _job, out var inJob) && inJob;
            }
            finally
            {
                CloseHandle(process);
            }
        }
    }

    private sealed class UntrackedSession(int? processId) : IRunningSession
    {
        public int? ProcessId { get; } = processId;

        public Task<SessionExit> Completion { get; } = Task.FromResult(SessionExit.NotTracked);

        public bool HeldForeground => false;

        public void Dispose()
        {
        }
    }

    // ---------------------------------------------------------------- Win32

    private const uint CreateSuspendedFlag = 0x00000004;
    private const uint CreateBreakawayFromJob = 0x01000000;
    private const uint CreateNoWindow = 0x08000000;
    private const int ErrorAccessDenied = 5;
    private const int AsfwAny = -1;
    private const int JobObjectAssociateCompletionPortInformation = 7;
    private const uint JobObjectMsgActiveProcessZero = 4;
    private const uint WaitTimeout = 258;
    private const uint ProcessQueryLimitedInformation = 0x1000;

    [StructLayout(LayoutKind.Sequential)]
    private struct AssociateCompletionPortInfo
    {
        public IntPtr CompletionKey;
        public IntPtr CompletionPort;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
    {
        public int cb;
        public IntPtr lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr hProcess, hThread;
        public int dwProcessId, dwThreadId;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr CreateJobObjectW(IntPtr jobAttributes, IntPtr name);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr CreateIoCompletionPort(IntPtr fileHandle, IntPtr existingPort, UIntPtr key, uint threads);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool SetInformationJobObject(IntPtr job, int infoClass, void* info, uint length);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static unsafe partial int CreateProcessW(
        char* applicationName, char* commandLine, IntPtr processAttributes, IntPtr threadAttributes, int inheritHandles,
        uint creationFlags, IntPtr environment, char* currentDirectory, StartupInfo* startupInfo, out ProcessInformation processInformation);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial uint ResumeThread(IntPtr thread);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr handle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetQueuedCompletionStatus(IntPtr port, out uint bytes, out UIntPtr key, out IntPtr overlapped, uint milliseconds);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostQueuedCompletionStatus(IntPtr port, uint bytes, UIntPtr key, IntPtr overlapped);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllowSetForegroundWindow(int processId);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsProcessInJob(IntPtr process, IntPtr job, [MarshalAs(UnmanagedType.Bool)] out bool result);
}
