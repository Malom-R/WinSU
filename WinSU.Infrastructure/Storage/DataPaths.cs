using WinSU.Core.Storage;

namespace WinSU.Infrastructure.Storage;

public sealed class DataPaths : IDataPaths
{
    public const string PortableMarkerFileName = "portable.txt";
    public const string AppFolderName = "WinSU";
    public const string DatabaseFileName = "winsu.db";

    public DataPaths(string root, bool isPortable)
    {
        Root = Path.GetFullPath(root);
        IsPortable = isPortable;
    }

    public string Root { get; }
    public bool IsPortable { get; }

    public string SettingsFile => Path.Combine(Root, "settings.json");
    public string LibraryFile => Path.Combine(Root, "library.json");
    public string DatabaseFile => Path.Combine(Root, DatabaseFileName);
    public string LogsDirectory => Path.Combine(Root, "logs");
    public string MediaDirectory => Path.Combine(Root, "media");
    public string ThemesFile => Path.Combine(Root, "themes.json");

    /// <summary>
    /// Choisit le dossier de données, par ordre de priorité : <paramref name="dataDirOverride"/>
    /// (<c>--data-dir</c>), mode portable (<c>portable.txt</c> à côté de l'exe), puis <c>%LOCALAPPDATA%\WinSU</c>.
    /// (Les anciens noms, dossier <c>iiSU</c> et base <c>iisu.db</c>, ne sont plus repris depuis l'étape 38.)
    /// </summary>
    public static DataPaths Resolve(string appBaseDirectory, string? dataDirOverride = null, string? localAppData = null)
    {
        if (!string.IsNullOrWhiteSpace(dataDirOverride))
            return new DataPaths(dataDirOverride, isPortable: false);

        if (File.Exists(Path.Combine(appBaseDirectory, PortableMarkerFileName)))
            return new DataPaths(Path.Combine(appBaseDirectory, "data"), isPortable: true);

        localAppData ??= Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new DataPaths(Path.Combine(localAppData, AppFolderName), isPortable: false);
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(MediaDirectory);
    }
}
