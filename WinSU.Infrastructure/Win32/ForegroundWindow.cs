using System.Runtime.InteropServices;

namespace WinSU.Infrastructure.Win32;

/// <summary>
/// Ramène une fenêtre au premier plan malgré le verrou de Windows (voir notes 3.8) : une application en arrière-plan
/// n'a normalement pas le droit de prendre le focus. On essaie du plus sage au plus insistant.
/// </summary>
public static partial class ForegroundWindow
{
    private const int SwShowMinNoActive = 7;
    private const int SwRestore = 9;
    private const byte VkMenu = 0x12;
    private const uint KeyEventFExtendedKey = 0x1;
    private const uint KeyEventFKeyUp = 0x2;

    /// <returns>True si la fenêtre est bien au premier plan à la fin.</returns>
    public static bool BringToFront(IntPtr window)
    {
        if (IsIconic(window))
            ShowWindow(window, SwRestore);

        // 1. Tentative directe (suffit si WinSU a encore le droit de premier plan).
        if (SetForegroundWindow(window) && GetForegroundWindow() == window)
            return true;

        // 2. Se greffer sur la file d'entrée de la fenêtre active, le temps de prendre le focus.
        var foreground = GetForegroundWindow();
        var foregroundThread = GetWindowThreadProcessId(foreground, IntPtr.Zero);
        var ownThread = GetCurrentThreadId();
        if (foregroundThread != 0 && foregroundThread != ownThread && AttachThreadInput(ownThread, foregroundThread, true))
        {
            BringWindowToTop(window);
            SetForegroundWindow(window);
            AttachThreadInput(ownThread, foregroundThread, false);
            if (GetForegroundWindow() == window)
                return true;
        }

        // 3. Dernier recours : un appui simulé sur Alt redonne le droit de premier plan au processus appelant.
        keybd_event(VkMenu, 0, KeyEventFExtendedKey, UIntPtr.Zero);
        keybd_event(VkMenu, 0, KeyEventFExtendedKey | KeyEventFKeyUp, UIntPtr.Zero);
        SetForegroundWindow(window);
        return GetForegroundWindow() == window;
    }

    /// <summary>
    /// Réduit la fenêtre sans activer personne. Une réduction classique (<c>SW_MINIMIZE</c>) active la fenêtre suivante
    /// dans l'ordre d'empilement (navigateur, éditeur…), qui passerait alors devant le jeu qui vient de s'ouvrir.
    /// </summary>
    public static void MinimizeWithoutActivating(IntPtr window) => ShowWindow(window, SwShowMinNoActive);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(IntPtr window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr window, int command);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr window);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool BringWindowToTop(IntPtr window);

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(IntPtr window, IntPtr processId);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetCurrentThreadId();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachThreadInput(uint attach, uint attachTo, [MarshalAs(UnmanagedType.Bool)] bool doAttach);

    [LibraryImport("user32.dll")]
    private static partial void keybd_event(byte key, byte scan, uint flags, UIntPtr extraInfo);
}
