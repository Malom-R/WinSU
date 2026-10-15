namespace WinSU.Core.Storage;

/// <summary>Emplacements des données de WinSU (voir notes, section 3.5).</summary>
public interface IDataPaths
{
    /// <summary><c>%LOCALAPPDATA%\WinSU</c>, <c>.\data</c> en mode portable, ou le dossier passé avec <c>--data-dir</c>.</summary>
    string Root { get; }

    bool IsPortable { get; }

    string SettingsFile { get; }
    string LibraryFile { get; }
    string DatabaseFile { get; }
    string LogsDirectory { get; }
    string MediaDirectory { get; }

    /// <summary>Thèmes enregistrés (M14).</summary>
    string ThemesFile { get; }

    /// <summary>Crée les dossiers s'ils n'existent pas.</summary>
    void EnsureCreated();
}
