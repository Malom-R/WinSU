using WinSU.Core.Library;
using WinSU.Core.Media;

namespace WinSU.Infrastructure.Media;

/// <summary>Image d'une entrée (M6) : celle du dossier <c>media</c>, choisie ou déposée (ES-DE retiré à l'étape 30, 30.10).</summary>
public sealed class MediaResolver(ICustomArtStore custom) : IMediaResolver
{
    public EntryArt? Find(LibraryGame entry) =>
        custom.Find(entry) is { } path ? new EntryArt(path, ArtSource.Custom) : null;
}
