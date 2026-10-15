using CommunityToolkit.Mvvm.ComponentModel;
using WinSU.App.Input;
using WinSU.App.Localization;
using WinSU.Core.Input;

namespace WinSU.App.ViewModels.Wizard;

public enum MappingRowState
{
    Pending,
    Current,
    Done,
}

/// <summary>Une ligne de l'étape de mapping : une action et l'entrée qui lui a été associée (glyphe et/ou texte).</summary>
public sealed partial class MappingRowViewModel(InputAction action) : ObservableObject
{
    public InputAction Action { get; } = action;

    public string Label { get; } = ActionLabels.Of(action);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCurrent), nameof(IsDone))]
    public partial MappingRowState State { get; set; }

    [ObservableProperty]
    public partial string? BindingText { get; set; }

    /// <summary>Glyphe de la position (manette reconnue), en police Console Sans.</summary>
    [ObservableProperty]
    public partial string? BindingGlyph { get; set; }

    /// <summary>Couleur du glyphe (réglage « Glyphes en couleurs », M12).</summary>
    [ObservableProperty]
    public partial GlyphTint BindingTint { get; set; }

    public bool IsCurrent => State == MappingRowState.Current;

    public bool IsDone => State == MappingRowState.Done;

    public void Show(BindingText binding)
    {
        BindingGlyph = binding.Glyph;
        BindingText = binding.Text;
        BindingTint = binding.Tint;
    }

    public void Reset()
    {
        State = MappingRowState.Pending;
        BindingText = null;
        BindingGlyph = null;
        BindingTint = GlyphTint.None;
    }
}

/// <summary>Témoin de l'écran de test : s'allume brièvement quand l'action est déclenchée.</summary>
public sealed partial class ActionIndicatorViewModel(InputAction action) : ObservableObject
{
    private int _flashes;

    public InputAction Action { get; } = action;

    public string Label { get; } = ActionLabels.Of(action);

    [ObservableProperty]
    public partial bool IsLit { get; set; }

    public async void Flash()
    {
        var flash = ++_flashes;
        IsLit = true;
        await Task.Delay(450);
        if (flash == _flashes)
            IsLit = false;
    }
}
