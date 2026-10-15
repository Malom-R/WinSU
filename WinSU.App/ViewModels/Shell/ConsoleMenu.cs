using WinSU.App.Localization;
using WinSU.App.Overlays;
using WinSU.Core.Library;
using Microsoft.Extensions.Logging;

namespace WinSU.App.ViewModels.Shell;

/// <summary>
/// Menu contextuel d'une console, le même dans l'onglet Jeux et sur l'accueil (D-70) : Ouvrir, Déplacer à gauche,
/// Déplacer à droite (selon sa place), Retirer (avec confirmation ; ses jeux quittent la bibliothèque avec elle). Le retrait
/// sert aussi à « Ajouter une console » (étape 34 : Confirmer sur une console déjà ajoutée, <see cref="RemoveAsync"/>).
/// </summary>
public sealed class ConsoleMenu(ILibraryService library, IOverlayService overlays, ILogger logger)
{
    /// <summary>Juste avant un déplacement : l'écran garde la console choisie quand ses tuiles sont reconstruites.</summary>
    public Action<string>? BeforeMove { get; init; }

    /// <summary>Juste avant un retrait : place de la console retirée (sa voisine prend la sélection).</summary>
    public Action<int>? BeforeRemove { get; init; }

    /// <param name="tile">Console choisie.</param>
    /// <param name="consoles">Toutes les consoles, dans l'ordre de la bibliothèque (pour savoir où l'on peut déplacer).</param>
    /// <param name="open">Ouvre l'écran des jeux de la console.</param>
    public async Task ShowAsync(ConsoleTileViewModel tile, IReadOnlyList<ConsoleTileViewModel> consoles, Action<ConsoleTileViewModel> open)
    {
        // Deux groupes (30.14) : Ouvrir et Retirer ; les déplacements
        var index = consoles.ToList().IndexOf(tile);
        List<MenuAction> actions =
        [
            new(Tr.Get("win_hint_open"), () =>
            {
                open(tile);
                return Task.CompletedTask;
            }),
            new(Tr.Get("win_console_remove"), () => RemoveAsync(tile.Console.Id, tile.Name, () => BeforeRemove?.Invoke(index))),
        ];
        List<MenuAction> moves = [];
        if (index > 0)
            moves.Add(new(Tr.Get("win_console_move_left"), () => MoveAsync(tile, -1)));
        if (index >= 0 && index < consoles.Count - 1)
            moves.Add(new(Tr.Get("win_console_move_right"), () => MoveAsync(tile, +1)));
        actions.AddRange(moves.Select((move, i) => move with { Choice = move.Choice with { StartsGroup = i == 0 } }));

        await overlays.ShowActionsAsync(tile.Name, actions);
    }

    private async Task MoveAsync(ConsoleTileViewModel tile, int offset)
    {
        BeforeMove?.Invoke(tile.Console.Id);
        await RunSafeAsync(() => library.MoveConsoleAsync(tile.Console.Id, offset));
    }

    /// <summary>
    /// Retire la console <paramref name="consoleId"/> (identifiant de la bibliothèque) après confirmation : le message dit
    /// combien de jeux partent avec elle (aucun fichier n'est effacé). <paramref name="beforeRemove"/> juste avant le retrait.
    /// True si elle a été retirée.
    /// </summary>
    public async Task<bool> RemoveAsync(string consoleId, string name, Action? beforeRemove = null)
    {
        var games = library.GamesOf(consoleId).Count();
        var confirmed = await overlays.ConfirmAsync(
            Tr.Format("win_console_remove_title", name),
            games == 0 ? Tr.Get("win_console_remove_message") : Tr.Format("win_console_remove_message_games", games),
            Tr.Get("win_console_remove"),
            Tr.Get("win_cancel"));
        if (!confirmed)
            return false;

        beforeRemove?.Invoke();
        return await RunSafeAsync(() => library.RemoveConsoleAsync(consoleId));
    }

    private async Task<bool> RunSafeAsync(Func<Task> action)
    {
        try
        {
            await action();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Impossible de modifier la bibliothèque");
            return false;
        }
    }
}
