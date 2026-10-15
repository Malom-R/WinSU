using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using WinSU.App.Controls;
using WinSU.App.Input;
using WinSU.App.Localization;
using WinSU.Core.Input;

namespace WinSU.App.ViewModels;

/// <summary>Une touche du clavier dessiné : sa place dans le dessin (DIP), son étiquette, allumée ou non.</summary>
public sealed partial class KeyCapViewModel(KeySlot slot) : ObservableObject
{
    public PhysicalKey Key { get; } = slot.Key;

    public double X { get; } = slot.X * KeyboardLayout.Unit;

    public double Y { get; } = slot.Y * KeyboardLayout.Unit;

    public double Width { get; } = (slot.Width * KeyboardLayout.Unit) - KeyboardLayout.Gap;

    public double Height { get; } = (slot.Height * KeyboardLayout.Unit) - KeyboardLayout.Gap;

    [ObservableProperty]
    public partial string Label { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLit { get; set; }
}

/// <summary>
/// Clavier dessiné de l'écran « Boutons », onglet Clavier (étape 32, D-130 : à la place de la manette) : un clavier complet
/// (<see cref="KeyboardLayout"/>), aux lettres de la disposition de Windows (AZERTY chez l'utilisateur) ; une touche
/// enfoncée s'allume tant qu'elle l'est (appui en direct seulement, choix de l'utilisateur). Suit le clavier entre
/// <see cref="Begin"/> et <see cref="End"/>.
/// </summary>
public sealed class KeyboardPreviewViewModel
{
    private readonly KeyboardInput _keyboard;
    private readonly IKeyboardLayout _layout;
    private readonly Dictionary<PhysicalKey, KeyCapViewModel> _byKey;
    private bool _running;

    public KeyboardPreviewViewModel(KeyboardInput keyboard, IKeyboardLayout layout)
    {
        _keyboard = keyboard;
        _layout = layout;
        Keys = [.. KeyboardLayout.Keys.Select(slot => new KeyCapViewModel(slot))];
        _byKey = Keys.ToDictionary(key => key.Key);
        Relabel();
    }

    public IReadOnlyList<KeyCapViewModel> Keys { get; }

    /// <summary>Taille du dessin, en DIP (mis à l'échelle de sa zone par la vue).</summary>
    public double Width => KeyboardLayout.Columns * KeyboardLayout.Unit;

    public double Height => KeyboardLayout.Rows * KeyboardLayout.Unit;

    /// <summary>Commence à suivre le clavier ; les lettres sont relues (la disposition a pu changer).</summary>
    public void Begin()
    {
        Relabel();
        if (_running)
            return;

        _running = true;
        _keyboard.PhysicalKeyChanged += OnPhysicalKeyChanged;
    }

    /// <summary>Arrête de suivre le clavier ; plus rien n'est allumé.</summary>
    public void End()
    {
        if (_running)
        {
            _running = false;
            _keyboard.PhysicalKeyChanged -= OnPhysicalKeyChanged;
        }

        foreach (var key in Keys)
            key.IsLit = false;
    }

    private void OnPhysicalKeyChanged(PhysicalKey key, bool down)
    {
        if (_byKey.TryGetValue(key, out var cap))
            cap.IsLit = down;
    }

    /// <summary>Lettres selon la disposition de Windows ; noms des autres touches dans la langue de WinSU.</summary>
    private void Relabel()
    {
        foreach (var (slot, cap) in KeyboardLayout.Keys.Zip(Keys))
        {
            cap.Label = slot.ScanCode != 0
                ? _layout.CharacterOf(slot.ScanCode) ?? slot.Label
                : slot.Label.StartsWith("win_", StringComparison.Ordinal) ? Tr.Get(slot.Label) : slot.Label;
        }
    }
}
