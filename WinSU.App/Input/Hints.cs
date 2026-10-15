using Avalonia.Input;
using WinSU.App.Localization;
using WinSU.Core.Input;
using WinSU.Core.Settings;

namespace WinSU.App.Input;

/// <summary>Une entrée de la barre d'aide : une action et ce qu'elle fait sur l'écran courant.</summary>
public sealed record Hint(InputAction Action, string Label)
{
    /// <summary>« Se déplacer » : représente les quatre directions.</summary>
    public static Hint Navigate => new(InputAction.Up, Tr.Get("win_hint_move"));

    public static Hint Tabs => new(InputAction.PagePrevious, Tr.Get("win_hint_tabs"));

    public static IReadOnlyList<Hint> Default =>
    [
        Navigate,
        new(InputAction.Confirm, Tr.Get("win_hint_confirm")),
        new(InputAction.Back, Tr.Get("win_hint_back")),
    ];
}

/// <summary>Implémenté par un écran (ou une fenêtre superposée) qui fournit ses propres aides.</summary>
public interface IHintSource
{
    IReadOnlyList<Hint> Hints { get; }
}

/// <summary>La touche d'une aide : texte, affiché avec la police des glyphes si <see cref="IsGlyph"/>, et couleur du glyphe.</summary>
public readonly record struct HintKey(string Text, bool IsGlyph, GlyphTint Tint = GlyphTint.None);

/// <summary>
/// Comment afficher la touche d'une action dans la barre d'aide : sa première liaison (M11), manette ou clavier selon le
/// dernier périphérique utilisé, dans le style de glyphes en vigueur (M12).
/// </summary>
public static class HintKeys
{
    /// <param name="mapping">Correspondance SDL de la manette reconnue : une ancienne liaison brute y prend son glyphe (DF-4).</param>
    public static HintKey For(InputAction action, InputDevice device, InputSettings input, GlyphStyle glyphs, string? mapping = null) =>
        device == InputDevice.Keyboard
            ? new(KeyboardText(action, input.GetKeys()), false)
            : ControllerText(action, ForDisplay(input.GetControllerBindings(), mapping), glyphs);

    private static IReadOnlyDictionary<InputAction, IReadOnlyList<InputBinding>> ForDisplay(
        IReadOnlyDictionary<InputAction, IReadOnlyList<InputBinding>> bindings, string? mapping) =>
        mapping is null
            ? bindings
            : bindings.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<InputBinding>)[.. pair.Value.Select(b => GamepadMapping.ForDisplay(b, mapping))]);

    private static HintKey ControllerText(InputAction action, IReadOnlyDictionary<InputAction, IReadOnlyList<InputBinding>> bindings, GlyphStyle glyphs)
    {
        if (ActionRepeater.IsDirection(action))
        {
            // « Se déplacer » : la croix ou le stick qui porte la première liaison de Haut.
            return First(bindings, InputAction.Up) switch
            {
                { Kind: InputBindingKind.GamepadButton } => new(ControllerGlyphs.DPad, true),
                { Kind: InputBindingKind.GamepadAxis, Index: (int)GamepadAxis.LeftX or (int)GamepadAxis.LeftY } => new(ControllerGlyphs.LeftStick, true),
                { Kind: InputBindingKind.GamepadAxis } => new(ControllerGlyphs.RightStick, true),
                null => new("?", false),
                _ => new("↑↓←→", false),
            };
        }

        if (action is InputAction.PagePrevious or InputAction.PageNext)
        {
            var (previous, next) = (First(bindings, InputAction.PagePrevious), First(bindings, InputAction.PageNext));
            return Glyph(previous, glyphs) is { } left && Glyph(next, glyphs) is { } right
                ? new($"{left.Glyph} {right.Glyph}", true)
                : new($"{Text(previous)} / {Text(next)}", false);
        }

        var binding = First(bindings, action);
        return Glyph(binding, glyphs) is { } glyph ? new(glyph.Glyph!, true, glyph.Tint) : new(Text(binding), false);
    }

    private static string KeyboardText(InputAction action, IReadOnlyDictionary<InputAction, IReadOnlyList<string>> keys)
    {
        string? FirstKey(InputAction a) => keys.TryGetValue(a, out var list) && list.Count > 0 ? list[0] : null;
        string Name(InputAction a) => FirstKey(a) is { } name ? ActionLabels.KeyName(name) : "?";

        if (ActionRepeater.IsDirection(action))
        {
            InputAction[] directions = [InputAction.Up, InputAction.Left, InputAction.Down, InputAction.Right];
            if (directions.All(d => KeyboardInput.Parse(FirstKey(d) ?? string.Empty) is Key.Up or Key.Down or Key.Left or Key.Right))
                return Tr.Get("win_key_arrows");
            var names = directions.Select(Name).ToList();
            return names.All(n => n.Length == 1) ? string.Concat(names) : string.Join(" ", names);
        }

        if (action is InputAction.PagePrevious or InputAction.PageNext)
        {
            return KeyboardInput.Parse(FirstKey(InputAction.PagePrevious) ?? string.Empty) == Key.PageUp
                && KeyboardInput.Parse(FirstKey(InputAction.PageNext) ?? string.Empty) == Key.PageDown
                ? Tr.Get("win_key_pages")
                : $"{Name(InputAction.PagePrevious)} {Name(InputAction.PageNext)}";
        }

        return Name(action);
    }

    private static InputBinding? First(IReadOnlyDictionary<InputAction, IReadOnlyList<InputBinding>> bindings, InputAction action) =>
        bindings.TryGetValue(action, out var list) && list.Count > 0 ? list[0] : null;

    /// <summary>Glyphe seul d'une position (sans flèche de stick), ou null pour une entrée brute ou sans glyphe.</summary>
    private static BindingText? Glyph(InputBinding? binding, GlyphStyle glyphs) =>
        binding is not null && binding.IsPosition() && ActionLabels.Show(binding, glyphs) is { Glyph: not null } text ? text : null;

    private static string Text(InputBinding? binding) => binding is null ? "?" : ActionLabels.Describe(binding);
}
