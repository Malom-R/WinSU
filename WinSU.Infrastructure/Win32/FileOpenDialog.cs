using System.Runtime.InteropServices;

namespace WinSU.Infrastructure.Win32;

/// <summary>
/// Boîte « Ouvrir » de Windows (IFileOpenDialog) avec l'option <c>FOS_NODEREFERENCELINKS</c> : choisir un raccourci
/// renvoie le <b>.lnk lui-même</b> (ou le .url) et non le programme visé, sinon on perdrait ses arguments (D-7).
/// Le sélecteur d'Avalonia ne permet pas ce réglage.
/// </summary>
public static class FileOpenDialog
{
    private const uint FosNoDereferenceLinks = 0x00100000;
    private const uint FosAllowMultiselect = 0x00000200;
    private const uint FosFileMustExist = 0x00001000;
    private const uint FosPathMustExist = 0x00000800;
    private const uint FosForceFileSystem = 0x00000040;
    private const uint SigdnFileSysPath = 0x80058000;
    private const int ErrorCancelled = unchecked((int)0x800704C7);

    /// <summary>
    /// Affiche la boîte (modale pour <paramref name="owner"/>) et renvoie les fichiers choisis, ou une liste vide.
    /// Par défaut : sélection multiple, raccourcis renvoyés tels quels (jeux et applications). Pour une image :
    /// un seul fichier, et un raccourci vers une image renvoie l'image.
    /// </summary>
    public static IReadOnlyList<string> PickFiles(
        IntPtr owner,
        string title,
        IReadOnlyList<(string Name, string Pattern)> filters,
        bool multiselect = true,
        bool keepShortcuts = true)
    {
        var options = FosFileMustExist | FosPathMustExist | FosForceFileSystem;
        if (multiselect)
            options |= FosAllowMultiselect;
        if (keepShortcuts)
            options |= FosNoDereferenceLinks;
        return Show(owner, title, options, filters);
    }

    private static IReadOnlyList<string> Show(
        IntPtr owner,
        string title,
        uint extraOptions,
        IReadOnlyList<(string Name, string Pattern)> filters)
    {
        var dialog = (IFileOpenDialog)new FileOpenDialogCoClass();
        try
        {
            dialog.GetOptions(out var options);
            dialog.SetOptions(options | extraOptions);
            dialog.SetTitle(title);
            if (filters.Count > 0)
                dialog.SetFileTypes((uint)filters.Count, [.. filters.Select(f => new FilterSpec { Name = f.Name, Spec = f.Pattern })]);

            var result = dialog.Show(owner);
            if (result == ErrorCancelled)
                return [];
            Marshal.ThrowExceptionForHR(result);

            dialog.GetResults(out var items);
            items.GetCount(out var count);
            var paths = new List<string>((int)count);
            for (uint i = 0; i < count; i++)
            {
                items.GetItemAt(i, out var item);
                item.GetDisplayName(SigdnFileSysPath, out var path);
                paths.Add(path);
            }

            return paths;
        }
        finally
        {
            Marshal.FinalReleaseComObject(dialog);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FilterSpec
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string Name;
        [MarshalAs(UnmanagedType.LPWStr)] public string Spec;
    }

    [ComImport]
    [Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
    private class FileOpenDialogCoClass;

    /// <summary>IFileOpenDialog (hérite d'IFileDialog et d'IModalWindow) : méthodes dans l'ordre de la vtable.</summary>
    [ComImport]
    [Guid("D57C7288-D4AD-4768-BE02-9D969532D960")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog
    {
        [PreserveSig] int Show(IntPtr parent);
        void SetFileTypes(uint count, [MarshalAs(UnmanagedType.LPArray)] FilterSpec[] filters);
        void SetFileTypeIndex(uint index);
        void GetFileTypeIndex(out uint index);
        void Advise(IntPtr events, out uint cookie);
        void Unadvise(uint cookie);
        void SetOptions(uint options);
        void GetOptions(out uint options);
        void SetDefaultFolder(IShellItem folder);
        void SetFolder(IShellItem folder);
        void GetFolder(out IShellItem folder);
        void GetCurrentSelection(out IShellItem item);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        void GetResult(out IShellItem item);
        void AddPlace(IShellItem item, int place);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
        void Close(int result);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(IntPtr filter);
        void GetResults(out IShellItemArray items);
        void GetSelectedItems(out IShellItemArray items);
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr bindContext, ref Guid handler, ref Guid interfaceId, out IntPtr result);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint form, [MarshalAs(UnmanagedType.LPWStr)] out string name);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IShellItem other, uint hint, out int order);
    }

    [ComImport]
    [Guid("B63EA76D-1F85-456F-A19C-48159EFA858B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemArray
    {
        void BindToHandler(IntPtr bindContext, ref Guid handler, ref Guid interfaceId, out IntPtr result);
        void GetPropertyStore(int flags, ref Guid interfaceId, out IntPtr result);
        void GetPropertyDescriptionList(IntPtr keyType, ref Guid interfaceId, out IntPtr result);
        void GetAttributes(int flags, uint mask, out uint attributes);
        void GetCount(out uint count);
        void GetItemAt(uint index, out IShellItem item);
        void EnumItems(out IntPtr enumerator);
    }
}
