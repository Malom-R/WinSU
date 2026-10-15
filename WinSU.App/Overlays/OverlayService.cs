using WinSU.Core.Audio;

namespace WinSU.App.Overlays;

/// <summary>
/// Un choix d'un menu. <see cref="StartsGroup"/> : il ouvre un nouveau groupe de choix semblables, séparé du précédent par
/// un espace (30.14, maquette de l'utilisateur : « Lancer, Renommer, Retirer » ; ce qui touche à l'image ; à la bannière).
/// </summary>
public sealed record MenuChoice(string Label, bool StartsGroup = false);

/// <summary>
/// Un choix d'un menu et ce qu'il fait (étape 36) : les menus Options d'une console et d'un jeu et le menu d'alimentation
/// se décrivent en une liste d'actions (<see cref="IOverlayService.ShowActionsAsync"/>).
/// </summary>
public sealed record MenuAction(MenuChoice Choice, Func<Task> Run)
{
    public MenuAction(string label, Func<Task> run, bool startsGroup = false)
        : this(new MenuChoice(label, startsGroup), run)
    {
    }
}

public interface IOverlayService
{
    OverlayViewModel? Current { get; }

    event EventHandler? CurrentChanged;

    /// <summary>
    /// Affiche un menu (panneau collé au bord droit, arrivé depuis la droite, le même pour tous les styles) et renvoie
    /// l'index du choix, ou null si l'utilisateur a fermé le menu.
    /// </summary>
    Task<int?> ShowMenuAsync(string title, IReadOnlyList<MenuChoice> choices);

    /// <summary>Un menu d'un seul groupe.</summary>
    Task<int?> ShowMenuAsync(string title, IReadOnlyList<string> items) => ShowMenuAsync(title, [.. items.Select(item => new MenuChoice(item))]);

    /// <summary>Un menu d'actions : montre leurs choix, puis lance celle qui est choisie (rien si le menu est fermé).</summary>
    async Task ShowActionsAsync(string title, IReadOnlyList<MenuAction> actions)
    {
        if (await ShowMenuAsync(title, [.. actions.Select(action => action.Choice)]) is { } index && index >= 0 && index < actions.Count)
            await actions[index].Run();
    }

    /// <summary>Demande une confirmation ; renvoie true seulement si l'utilisateur confirme.</summary>
    Task<bool> ConfirmAsync(string title, string message, string confirmLabel, string cancelLabel);

    /// <summary>Affiche un message avec un seul bouton.</summary>
    Task AlertAsync(string title, string message, string okLabel);

    /// <summary>Demande un texte ; renvoie null si l'utilisateur annule.</summary>
    Task<string?> PromptTextAsync(string title, string initialText, string confirmLabel, string cancelLabel);
}

/// <summary>Une seule fenêtre superposée à la fois ; en ouvrir une nouvelle annule la précédente.</summary>
public sealed class OverlayService(IAudioService audio) : IOverlayService
{
    public OverlayViewModel? Current { get; private set; }

    public event EventHandler? CurrentChanged;

    public async Task<int?> ShowMenuAsync(string title, IReadOnlyList<MenuChoice> choices)
    {
        var menu = new ContextMenuViewModel(title, choices);
        Open(menu, UiSound.ContextMenu);
        var choice = await menu.Result;
        Close(menu, choice is null ? UiSound.Close : UiSound.Open);
        return choice;
    }

    public async Task<bool> ConfirmAsync(string title, string message, string confirmLabel, string cancelLabel)
    {
        var dialog = new ConfirmDialogViewModel(title, message, confirmLabel, cancelLabel);
        Open(dialog, UiSound.Open);
        var confirmed = await dialog.Result;
        Close(dialog, confirmed ? UiSound.Open : UiSound.Close);
        return confirmed;
    }

    public async Task AlertAsync(string title, string message, string okLabel)
    {
        var dialog = new ConfirmDialogViewModel(title, message, okLabel, cancelLabel: null);
        Open(dialog, UiSound.Open);
        await dialog.Result;
        Close(dialog, UiSound.Close);
    }

    public async Task<string?> PromptTextAsync(string title, string initialText, string confirmLabel, string cancelLabel)
    {
        var dialog = new TextInputDialogViewModel(title, initialText, confirmLabel, cancelLabel);
        Open(dialog, UiSound.Open);
        var text = await dialog.Result;
        Close(dialog, text is null ? UiSound.Close : UiSound.Open);
        return text;
    }

    private void Open(OverlayViewModel overlay, UiSound sound)
    {
        Current?.Cancel();
        Current = overlay;
        audio.Play(sound);
        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Close(OverlayViewModel overlay, UiSound sound)
    {
        if (Current != overlay)
            return;

        Current = null;
        audio.Play(sound);
        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }
}
