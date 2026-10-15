using WinSU.App.Localization;
using WinSU.App.Overlays;
using WinSU.App.Services;
using WinSU.Core.Library;
using WinSU.Core.Media;

namespace WinSU.App.ViewModels.Shell;

/// <summary>
/// Menu contextuel d'un jeu ou d'une application, le même dans les grilles et sur l'accueil (D-70), en quatre groupes
/// (30.14, maquette de l'utilisateur ; étape 33) : Lancer, Renommer, Retirer (avec confirmation ; seule l'entrée quitte la
/// bibliothèque, aucun fichier n'est effacé) ; puis un groupe par sorte d'image : l'image, la bannière héro, le logo
/// (étape 33, D-140). Chaque groupe : « Choisir… » (un fichier du PC) ou, avec une clé SteamGridDB, « … SteamGridDB »
/// (l'écran de choix du site, à la place du fichier : D-139) ; puis « Retirer… » s'il y en a une.
/// </summary>
public sealed class EntryMenu(
    ILibraryService library,
    LaunchCoordinator launcher,
    IOverlayService overlays,
    EntryImageService images,
    EntryCovers covers)
{
    /// <summary>Juste avant un renommage : l'écran garde l'entrée choisie quand ses tuiles sont reconstruites.</summary>
    public Action<Guid>? BeforeRename { get; init; }

    /// <summary>Juste avant un retrait confirmé : l'écran donne la sélection à la voisine de l'entrée.</summary>
    public Action<Guid>? BeforeRemove { get; init; }

    /// <summary>Après le choix ou le retrait d'une bannière héro ou d'un logo (l'accueil les réaffiche).</summary>
    public Action<LibraryGame>? ArtChanged { get; init; }

    public async Task ShowAsync(GameTileViewModel tile)
    {
        List<MenuAction> items =
        [
            new(Tr.Get("win_hint_launch"), () =>
            {
                _ = launcher.LaunchAsync(tile.Game); // dure toute la session : on ne l'attend pas
                return Task.CompletedTask;
            }),
            new(Tr.Get("win_game_rename"), () => RenameAsync(tile)),
            new(Tr.Get("win_console_remove"), () => RemoveAsync(tile)),
        ];
        AddArtGroup(items, tile, ArtKind.Cover, "win_entry_choose_image", "win_entry_sgdb_image", "win_entry_remove_image");
        AddArtGroup(items, tile, ArtKind.Hero, "win_entry_choose_hero", "win_entry_sgdb_hero", "win_entry_remove_hero");
        AddArtGroup(items, tile, ArtKind.Logo, "win_entry_choose_logo", "win_entry_sgdb_logo", "win_entry_remove_logo");

        await overlays.ShowActionsAsync(tile.Name, items);
    }

    /// <summary>Le groupe d'une sorte d'image : la choisir (sur le disque, ou sur SteamGridDB avec une clé), la retirer.</summary>
    private void AddArtGroup(List<MenuAction> items, GameTileViewModel tile, ArtKind kind, string choose, string search, string remove)
    {
        items.Add(images.CanSearchOnline
            ? new(Tr.Get(search), () => SearchImage(tile, kind), startsGroup: true)
            : new(Tr.Get(choose), () => ChooseImageAsync(tile, kind), startsGroup: true));
        if (images.HasCustomImage(tile.Game, kind))
            items.Add(new(Tr.Get(remove), () => RemoveImageAsync(tile, kind)));
    }

    private async Task RenameAsync(GameTileViewModel tile)
    {
        var name = await overlays.PromptTextAsync(Tr.Get("win_game_rename"), tile.Name, Tr.Get("win_ok"), Tr.Get("win_cancel"));
        if (string.IsNullOrWhiteSpace(name))
            return;

        // L'image choisie suit le nom : déplacée avant le renommage, que la reconstruction des tuiles la trouve.
        images.Rename(tile.Game, name);
        BeforeRename?.Invoke(tile.Game.Id);
        if (!await library.RenameEntryAsync(tile.Game.Id, name))
            images.Rename(tile.Game with { Name = name.Trim() }, tile.Game.Name);
    }

    private async Task ChooseImageAsync(GameTileViewModel tile, ArtKind kind)
    {
        if (await images.ChooseAsync(tile.Game, kind) is { } saved)
            await ShowChangeAsync(tile, kind, saved);
    }

    /// <summary>Écran de choix SteamGridDB (étape 31) ; l'image enregistrée se voit comme une image choisie sur le disque.</summary>
    private Task SearchImage(GameTileViewModel tile, ArtKind kind)
    {
        images.SearchOnline(tile.Game, kind, saved => ShowChangeAsync(tile, kind, saved));
        return Task.CompletedTask;
    }

    private async Task RemoveImageAsync(GameTileViewModel tile, ArtKind kind)
    {
        if (images.Remove(tile.Game, kind))
            await ShowChangeAsync(tile, kind, saved: null);
    }

    /// <summary>
    /// Une image choisie (<paramref name="saved"/>) ou retirée se voit : la jaquette refait la tuile (son cache oublié ;
    /// retirée : la tuile par défaut) ; une bannière ou un logo, l'écran les relit (<see cref="ArtChanged"/>).
    /// </summary>
    private async Task ShowChangeAsync(GameTileViewModel tile, ArtKind kind, string? saved)
    {
        if (kind != ArtKind.Cover)
        {
            ArtChanged?.Invoke(tile.Game);
            return;
        }

        if (saved is not null)
            covers.Forget(saved);
        await covers.ApplyAsync(tile);
    }

    private async Task RemoveAsync(GameTileViewModel tile)
    {
        var message = Tr.Get("win_game_remove_message");
        var confirmed = await overlays.ConfirmAsync(
            Tr.Format("win_console_remove_title", tile.Name), message, Tr.Get("win_console_remove"), Tr.Get("win_cancel"));
        if (!confirmed)
            return;

        BeforeRemove?.Invoke(tile.Game.Id);
        await library.RemoveEntryAsync(tile.Game.Id);
    }
}
