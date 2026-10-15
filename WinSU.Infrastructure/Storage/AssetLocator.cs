using WinSU.Core.Storage;

namespace WinSU.Infrastructure.Storage;

public sealed class AssetLocator : IAssetLocator
{
    public AssetLocator(string root)
    {
        Root = Path.GetFullPath(root);
    }

    public string Root { get; }

    public string Resolve(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var combined = Path.GetFullPath(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSeparator = Path.EndsInDirectorySeparator(Root) ? Root : Root + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Le chemin sort du dossier des assets : {relativePath}", nameof(relativePath));

        return combined;
    }
}
