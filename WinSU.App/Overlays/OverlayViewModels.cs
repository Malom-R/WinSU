using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinSU.App.Input;
using WinSU.App.Localization;
using WinSU.App.ViewModels;
using WinSU.Core.Input;

namespace WinSU.App.Overlays;

/// <summary>Fenêtre superposée à l'écran courant (menu, confirmation), pilotable à la manette.</summary>
public abstract class OverlayViewModel : ViewModelBase, IInputHandler, IHintSource
{
    public abstract IReadOnlyList<Hint> Hints { get; }

    public abstract bool HandleAction(InputActionEvent e);

    /// <summary>Ferme sans choix (Retour).</summary>
    public abstract void Cancel();
}

/// <summary>
/// Menu contextuel : une liste d'actions en groupes (<see cref="MenuChoice.StartsGroup"/>), fermée par Retour ou par la
/// touche Menu.
/// </summary>
public sealed class ContextMenuViewModel : OverlayViewModel
{
    private readonly TaskCompletionSource<int?> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ContextMenuViewModel(string title, IReadOnlyList<MenuChoice> choices)
    {
        Title = title;
        // Le premier choix n'est jamais précédé d'un espace
        Items = [.. choices.Select((choice, index) =>
            new ContextMenuItem(choice.Label, choice.StartsGroup && index > 0, () => _result.TrySetResult(index)))];
    }

    public string Title { get; }

    public IReadOnlyList<ContextMenuItem> Items { get; }

    public Task<int?> Result => _result.Task;

    public override IReadOnlyList<Hint> Hints { get; } =
    [
        Hint.Navigate,
        new(InputAction.Confirm, Tr.Get("win_hint_choose")),
        new(InputAction.Back, Tr.Get("win_hint_close")),
    ];

    public override bool HandleAction(InputActionEvent e)
    {
        if (e.IsRepeat || e.Action is not (InputAction.Back or InputAction.ContextMenu))
            return false;

        Cancel();
        return true;
    }

    public override void Cancel() => _result.TrySetResult(null);
}

public sealed class ContextMenuItem(string label, bool startsGroup, Action choose) : IActivatable
{
    public string Label { get; } = label;

    /// <summary>Un espace le sépare du choix d'avant (nouveau groupe).</summary>
    public bool StartsGroup { get; } = startsGroup;

    public void Activate() => choose();
}

/// <summary>
/// Confirmation (deux boutons) ou simple message (un bouton, si <paramref name="cancelLabel"/> est null).
/// Le focus démarre sur le premier bouton, « Annuler », pour éviter une action accidentelle (voir D-9).
/// </summary>
public sealed partial class ConfirmDialogViewModel(string title, string message, string confirmLabel, string? cancelLabel)
    : OverlayViewModel
{
    private readonly TaskCompletionSource<bool> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string Title { get; } = title;
    public string Message { get; } = message;
    public string ConfirmLabel { get; } = confirmLabel;
    public string CancelLabel { get; } = cancelLabel ?? string.Empty;
    public bool HasCancel { get; } = cancelLabel is not null;

    public Task<bool> Result => _result.Task;

    public override IReadOnlyList<Hint> Hints { get; } = cancelLabel is null
        ? [new(InputAction.Confirm, confirmLabel)]
        : [Hint.Navigate, new(InputAction.Confirm, Tr.Get("win_hint_choose")), new(InputAction.Back, cancelLabel)];

    public override bool HandleAction(InputActionEvent e)
    {
        if (e.IsRepeat || e.Action != InputAction.Back)
            return false;

        Cancel();
        return true;
    }

    public override void Cancel() => _result.TrySetResult(false);

    [RelayCommand]
    private void Confirm() => _result.TrySetResult(true);

    [RelayCommand]
    private void Dismiss() => Cancel();
}

/// <summary>Saisie d'un texte (renommer un jeu). Au clavier : Entrée valide, Échap annule.</summary>
public sealed partial class TextInputDialogViewModel(string title, string initialText, string confirmLabel, string cancelLabel)
    : OverlayViewModel
{
    private readonly TaskCompletionSource<string?> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string Title { get; } = title;
    public string ConfirmLabel { get; } = confirmLabel;
    public string CancelLabel { get; } = cancelLabel;

    [ObservableProperty]
    public partial string Text { get; set; } = initialText;

    public Task<string?> Result => _result.Task;

    public override IReadOnlyList<Hint> Hints { get; } =
    [
        new(InputAction.Confirm, confirmLabel),
        new(InputAction.Back, cancelLabel),
    ];

    public override bool HandleAction(InputActionEvent e)
    {
        if (e.IsRepeat || e.Action != InputAction.Back)
            return false;

        Cancel();
        return true;
    }

    public override void Cancel() => _result.TrySetResult(null);

    [RelayCommand]
    public void Confirm() => _result.TrySetResult(Text);

    [RelayCommand]
    private void Dismiss() => Cancel();
}
