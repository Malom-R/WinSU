using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using WinSU.Core.Power;
using Microsoft.Extensions.Logging;

namespace WinSU.Infrastructure.Power;

/// <summary>
/// <see cref="IPowerService"/> sous Windows : <c>shutdown.exe</c> pour redémarrer ou éteindre (sans délai),
/// <c>SetSuspendState</c> pour la veille classique (S3). Sur un PC en <b>veille moderne</b> (S0 faible consommation,
/// la plupart des portables récents), Windows refuse <c>SetSuspendState</c> (« non pris en charge ») : on éteint
/// alors l'écran, ce qui fait entrer ces PC en veille, comme le bouton d'alimentation (vu en réel chez l'utilisateur).
/// </summary>
public sealed partial class WindowsPowerService(ILogger<WindowsPowerService> logger) : IPowerService
{
    private const int ErrorNotSupported = 50;
    private const uint WmSysCommand = 0x0112;
    private const nint ScMonitorPower = 0xF170;
    private const nint MonitorOff = 2;
    private static readonly nint BroadcastWindow = 0xFFFF;

    /// <summary>Tests : remplace le lancement de <c>shutdown.exe</c>, qui éteindrait vraiment le PC.</summary>
    internal Func<ProcessStartInfo, Process?> StartProcess { get; init; } = Process.Start;

    /// <summary>Tests : remplace la mise en veille classique. Renvoie 0, ou le code d'erreur Win32.</summary>
    internal Func<int> Suspend { get; init; } = () =>
        SetSuspendState(hibernate: false, forceCritical: false, disableWakeEvent: false) ? 0 : Marshal.GetLastPInvokeError();

    /// <summary>Tests : remplace l'extinction de l'écran (veille moderne).</summary>
    internal Func<bool> TurnDisplayOff { get; init; } = () =>
        PostMessageW(BroadcastWindow, WmSysCommand, ScMonitorPower, MonitorOff);

    public void Execute(PowerAction action)
    {
        logger.LogInformation("Alimentation : {Action} demandé", action);
        switch (action)
        {
            case PowerAction.Sleep:
                Sleep();
                break;

            case PowerAction.Restart:
                RunShutdown("/r /t 0");
                break;

            case PowerAction.Shutdown:
                RunShutdown("/s /t 0");
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    internal static ProcessStartInfo ShutdownCommand(string arguments) =>
        new(Path.Combine(Environment.SystemDirectory, "shutdown.exe"), arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };

    private void Sleep()
    {
        var error = Suspend();
        if (error == 0)
            return;
        if (error != ErrorNotSupported)
            throw new Win32Exception(error);

        logger.LogInformation("Veille classique non prise en charge (veille moderne) : extinction de l'écran");
        if (!TurnDisplayOff())
            throw new Win32Exception(Marshal.GetLastPInvokeError());
    }

    private void RunShutdown(string arguments)
    {
        using var process = StartProcess(ShutdownCommand(arguments))
            ?? throw new InvalidOperationException("shutdown.exe n'a pas pu être lancé.");
    }

    [LibraryImport("powrprof.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool SetSuspendState(
        [MarshalAs(UnmanagedType.U1)] bool hibernate,
        [MarshalAs(UnmanagedType.U1)] bool forceCritical,
        [MarshalAs(UnmanagedType.U1)] bool disableWakeEvent);

    // Posté et non envoyé : un programme figé ne peut pas bloquer WinSU.
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessageW(nint window, uint message, nint wParam, nint lParam);
}
