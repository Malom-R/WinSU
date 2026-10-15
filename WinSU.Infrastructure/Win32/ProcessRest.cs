using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WinSU.Infrastructure.Win32;

/// <summary>
/// WinSU laisse la place au jeu pendant sa veille derrière lui (D-181) : priorité « inférieure à la normale » pour Windows,
/// mémoire inutilisée rendue (ramasse-miettes qui rend ses pages, puis pages de travail retirées : Windows les relit à la
/// demande au retour). La libération du résultat rend la priorité d'avant.
/// </summary>
public static partial class ProcessRest
{
    public static IDisposable Enter()
    {
        using var process = Process.GetCurrentProcess();
        var before = process.PriorityClass;
        process.PriorityClass = ProcessPriorityClass.BelowNormal;

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        SetProcessWorkingSetSize(process.Handle, -1, -1);
        return new Restore(before);
    }

    private sealed class Restore(ProcessPriorityClass before) : IDisposable
    {
        private bool _done;

        public void Dispose()
        {
            if (_done)
                return;

            _done = true;
            using var process = Process.GetCurrentProcess();
            process.PriorityClass = before;
        }
    }

    // (-1, -1) : retire autant de pages que possible des pages de travail du processus.
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetProcessWorkingSetSize(IntPtr process, nint minimumSize, nint maximumSize);
}
