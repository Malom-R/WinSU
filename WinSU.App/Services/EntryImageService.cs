using WinSU.App.Localization;
using WinSU.App.Navigation;
using WinSU.App.Overlays;
using WinSU.App.ViewModels.Shell;
using WinSU.Core.Library;
using WinSU.Core.Media;
using WinSU.Core.Settings;
using Microsoft.Extensions.Logging;

namespace WinSU.App.Services;

/// <summary>
/// Images des jeux et des applications (M6) : jaquettes des tuiles, cadre des consoles (réglage), et menu
/// « Choisir une image » / « Retirer l'image » (copie dans le dossier <c>media</c>, D-14 et D-15) ; de même pour la
/// bannière héro (M9, <see cref="ArtKind.Hero"/>) et le logo (étape 33, <see cref="ArtKind.Logo"/>). Avec une clé
/// SteamGridDB (étape 31), le choix passe par le site (<see cref="ArtworkPickerViewModel"/>, D-139).
/// </summary>
public sealed class EntryImageService(
    IMediaResolver resolver,
    ICustomArtStore store,
    IMediaPicker picker,
    IImageLoader images,
    IOverlayService overlays,
    ISettingsStore settings,
    IArtworkSearch search,
    INavigationService navigation,
    ILogger<EntryImageService> logger)
{
    /// <summary>Cadre de la console sur les tuiles des jeux (Réglages → Thème).</summary>
    public bool ShowBorders => settings.Current.Appearance.ShowBorders;

    public EntryCovers CreateCovers() => new(resolver, images);

    public ArtImages CreateHeroes() => new(store, images, ArtKind.Hero, ArtImages.HeroDecodeWidth);

    public ArtImages CreateLogos() => new(store, images, ArtKind.Logo, ArtImages.LogoDecodeWidth);

    public bool HasCustomImage(LibraryGame entry, ArtKind kind = ArtKind.Cover) => store.Find(entry, kind) is not null;

    /// <summary>
    /// Boîte « Ouvrir » (une image), vérification que l'image se lit, puis copie. Renvoie le chemin de la copie,
    /// ou null (annulé, ou image refusée avec un message).
    /// </summary>
    public async Task<string?> ChooseAsync(LibraryGame entry, ArtKind kind = ArtKind.Cover)
    {
        var title = Tr.Format(kind switch
        {
            ArtKind.Hero => "win_hero_pick_title",
            ArtKind.Logo => "win_logo_pick_title",
            _ => "win_image_pick_title",
        }, entry.Name);
        var path = await picker.PickImageAsync(title, store.Extensions);
        if (path is null)
            return null;

        return await SaveAsync(entry, path, kind, path);
    }

    /// <summary>
    /// Une clé SteamGridDB est enregistrée : le menu d'une entrée choisit ses images sur le site (D-115), à la place d'un
    /// fichier du PC (D-139).
    /// </summary>
    public bool CanSearchOnline => search.HasKey;

    /// <summary>
    /// Ouvre l'écran de choix SteamGridDB (étape 31, D-116) ; <paramref name="applied"/> reçoit le chemin de la copie quand
    /// une image a été téléchargée et enregistrée, juste avant le retour à l'écran d'où l'on vient.
    /// </summary>
    public void SearchOnline(LibraryGame entry, ArtKind kind, Func<string, Task> applied) =>
        navigation.NavigateTo<ArtworkPickerViewModel>(picker => picker.Show(entry, kind, applied));

    /// <summary>
    /// Enregistre une image téléchargée (fichier temporaire, que l'appelant supprime) comme celle de l'entrée, après avoir
    /// vérifié qu'elle se lit. Renvoie le chemin de la copie, ou null (image refusée, avec un message).
    /// </summary>
    public Task<string?> SaveDownloadedAsync(LibraryGame entry, string file, ArtKind kind) => SaveAsync(entry, file, kind, "SteamGridDB");

    private async Task<string?> SaveAsync(LibraryGame entry, string path, ArtKind kind, string source)
    {
        if (!store.Extensions.Contains(Path.GetExtension(path).ToLowerInvariant()) || !await images.IsImageAsync(path))
        {
            logger.LogInformation("Image refusée : {Path}", path);
            await overlays.AlertAsync(Tr.Get("win_image_invalid_title"), Tr.Format("win_image_invalid_message", Path.GetFileName(path)), Tr.Get("win_ok"));
            return null;
        }

        try
        {
            var saved = await Task.Run(() => store.Save(entry, path, kind));
            logger.LogInformation("Image ({Kind}) choisie pour {Name} : {Source} copiée dans {Target}", kind, entry.Name, source, saved);
            return saved;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Impossible de copier l'image {Source}", path);
            await overlays.AlertAsync(Tr.Get("win_image_invalid_title"), Tr.Format("win_image_copy_failed", ex.Message), Tr.Get("win_ok"));
            return null;
        }
    }

    /// <summary>Supprime la copie de l'image choisie (dans le dossier <c>media</c> de WinSU).</summary>
    public bool Remove(LibraryGame entry, ArtKind kind = ArtKind.Cover)
    {
        try
        {
            return store.Remove(entry, kind);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Impossible de retirer l'image de {Name}", entry.Name);
            return false;
        }
    }

    /// <summary>L'image choisie suit l'entrée renommée (à faire avant le renommage dans la bibliothèque).</summary>
    public void Rename(LibraryGame entry, string newName)
    {
        try
        {
            store.Rename(entry, newName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Impossible de renommer l'image de {Name}", entry.Name);
        }
    }
}
