namespace WinSU.Core.Storage;

/// <summary>
/// Emplacement des assets livrés avec l'application (dossier <c>Assets</c> à côté de l'exe),
/// à ne pas confondre avec les données de l'utilisateur (<see cref="IDataPaths"/>).
/// </summary>
public interface IAssetLocator
{
    /// <summary>Dossier <c>Assets</c> à côté de l'exécutable.</summary>
    string Root { get; }

    /// <summary>Chemin absolu d'un asset à partir de son chemin relatif (<c>Platforms/gc.webp</c>).</summary>
    string Resolve(string relativePath);
}
