using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace WinSU.Infrastructure.Win32;

/// <summary>Contenu utile d'un raccourci Windows (.lnk).</summary>
public sealed record ShellLinkInfo(string TargetPath, string Arguments, string WorkingDirectory);

/// <summary>Lecture (et écriture, pour les tests) des raccourcis .lnk via l'objet COM ShellLink de Windows.</summary>
public static class ShellLinkFile
{
    private const int MaxPath = 32_768;
    private const int StgmRead = 0;

    public static ShellLinkInfo Read(string path)
    {
        var link = (IShellLinkW)new ShellLinkCoClass();
        try
        {
            ((IPersistFile)link).Load(path, StgmRead);

            var target = new StringBuilder(MaxPath);
            link.GetPath(target, target.Capacity, IntPtr.Zero, 0);
            var arguments = new StringBuilder(MaxPath);
            link.GetArguments(arguments, arguments.Capacity);
            var workingDirectory = new StringBuilder(MaxPath);
            link.GetWorkingDirectory(workingDirectory, workingDirectory.Capacity);

            return new ShellLinkInfo(
                Environment.ExpandEnvironmentVariables(target.ToString()),
                arguments.ToString(),
                Environment.ExpandEnvironmentVariables(workingDirectory.ToString()));
        }
        finally
        {
            Marshal.FinalReleaseComObject(link);
        }
    }

    public static void Write(string path, ShellLinkInfo info)
    {
        var link = (IShellLinkW)new ShellLinkCoClass();
        try
        {
            link.SetPath(info.TargetPath);
            link.SetArguments(info.Arguments);
            link.SetWorkingDirectory(info.WorkingDirectory);
            ((IPersistFile)link).Save(path, fRemember: true);
        }
        finally
        {
            Marshal.FinalReleaseComObject(link);
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLinkCoClass;

    /// <summary>Interface COM IShellLinkW : l'ordre des méthodes doit suivre celui de la vtable Windows.</summary>
    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }
}
