using System.Globalization;
using Avalonia.Input;
using WinSU.App.Localization;
using WinSU.Core.Input;

namespace WinSU.App.Input;

/// <summary>
/// Représentation d'une liaison : glyphe de manette (police Console Sans, <see cref="Glyph"/>) et/ou texte
/// (<see cref="Text"/> : direction d'un stick, bouton brut, touche du clavier), et couleur du glyphe (M12).
/// </summary>
public sealed record BindingText(string? Glyph, string? Text, GlyphTint Tint = GlyphTint.None);

/// <summary>
/// Glyphes privés de Console Sans (U+E0A0 → U+E0CB), relevés sur planche en M11 et vérifiés en M12 (notes 18.1). La
/// police a aussi des glyphes de position (quatre boutons dont un plein, U+E0B4 → U+E0B7), plus utilisés depuis M12.
/// </summary>
public static class ControllerGlyphs
{
    // Lettres dans un cercle (Xbox ; Switch selon la position) et symboles PlayStation.
    public const string A = "\uE0A0";
    public const string B = "\uE0A1";
    public const string X = "\uE0A2";
    public const string Y = "\uE0A3";
    public const string Circle = "\uE0A4";
    public const string Cross = "\uE0A5";
    public const string Triangle = "\uE0A6";
    public const string Square = "\uE0A7";

    // Deux pilules : celle de droite pleine = Start (Menu, Options), celle de gauche = Select (Affichage, Share).
    public const string Start = "\uE0A8";
    public const string Select = "\uE0A9";
    public const string Plus = "\uE0AA";
    public const string Minus = "\uE0AB";

    public const string DPad = "\uE0AC";
    public const string DPadUp = "\uE0B9";
    public const string DPadDown = "\uE0BA";
    public const string DPadLeft = "\uE0BB";
    public const string DPadRight = "\uE0BC";
    public const string LeftStick = "\uE0BE";
    public const string RightStick = "\uE0BF";

    // Gâchettes : L R / ZL ZR (Switch), L1 R1 / L2 R2 (PlayStation), LB RB / LT RT (Xbox).
    public const string L = "\uE0C0";
    public const string R = "\uE0C1";
    public const string L1 = "\uE0C2";
    public const string R1 = "\uE0C3";
    public const string ZL = "\uE0C4";
    public const string L2 = "\uE0C5";
    public const string ZR = "\uE0C6";
    public const string R2 = "\uE0C7";
    public const string LB = "\uE0C8";
    public const string RB = "\uE0C9";
    public const string LT = "\uE0CA";
    public const string RT = "\uE0CB";
}

/// <summary>Noms des actions, des entrées de manette et des touches, pour l'assistant, l'écran « Boutons » et la barre d'aide.</summary>
public static class ActionLabels
{
    public static string Of(InputAction action) => Tr.Get(action switch
    {
        InputAction.Up => "win_action_up",
        InputAction.Down => "win_action_down",
        InputAction.Left => "win_action_left",
        InputAction.Right => "win_action_right",
        InputAction.Confirm => "win_action_confirm",
        InputAction.Back => "win_action_back",
        InputAction.ContextMenu => "win_action_context_menu",
        InputAction.Start => "win_action_start",
        InputAction.PagePrevious => "win_action_page_previous",
        _ => "win_action_page_next",
    });

    /// <summary>
    /// Glyphe et/ou texte d'une liaison de manette dans le style en vigueur (M12) : « A » (ou « ✕ »…), « stick gauche ↑ »,
    /// « Bouton 3 »…
    /// </summary>
    public static BindingText Show(InputBinding binding, GlyphStyle glyphs) => binding.Kind switch
    {
        InputBindingKind.GamepadButton => Show((GamepadButton)binding.Index, glyphs),
        InputBindingKind.GamepadAxis => Show((GamepadAxis)binding.Index, binding.Direction, glyphs),
        _ => new BindingText(null, Describe(binding)),
    };

    /// <summary>Texte seul (liaison brute, ou position sans glyphe) : « Bouton 3 », « Axe 1 − », « Croix ↑ ».</summary>
    public static string Describe(InputBinding binding) => binding.Kind switch
    {
        InputBindingKind.Button => Tr.Format("win_binding_button", binding.Index),
        InputBindingKind.Axis => Tr.Format("win_binding_axis", binding.Index, binding.Direction == InputDirection.Negative ? "−" : "+"),
        InputBindingKind.Hat => Tr.Format("win_binding_hat", Arrow(binding.Direction)
            + (binding.Index > 0 ? " " + binding.Index.ToString(CultureInfo.InvariantCulture) : string.Empty)),
        _ => Show(binding, GlyphStyle.Default) is { Text: { } text } ? text : "?",
    };

    /// <summary>Nom affiché d'une touche : « Entrée », « Échap », « ↑ », « Z », « Pavé 5 »…</summary>
    public static string KeyName(Key key) => key switch
    {
        Key.Enter => Tr.Get("win_key_enter"),
        Key.Space => Tr.Get("win_key_space"),
        Key.Escape => Tr.Get("win_key_escape"),
        Key.Back => Tr.Get("win_key_backspace"),
        Key.Tab => Tr.Get("win_key_tab"),
        Key.Home => Tr.Get("win_key_home"),
        Key.End => Tr.Get("win_key_end"),
        Key.PageUp => Tr.Get("win_key_page_up"),
        Key.PageDown => Tr.Get("win_key_page_down"),
        Key.Insert => Tr.Get("win_key_insert"),
        Key.Delete => Tr.Get("win_key_delete"),
        Key.Apps => Tr.Get("win_key_menu"),
        Key.Up => "↑",
        Key.Down => "↓",
        Key.Left => "←",
        Key.Right => "→",
        >= Key.D0 and <= Key.D9 => ((int)(key - Key.D0)).ToString(CultureInfo.InvariantCulture),
        >= Key.NumPad0 and <= Key.NumPad9 => Tr.Format("win_key_numpad", (int)(key - Key.NumPad0)),
        _ => key.ToString().Replace("Oem", string.Empty, StringComparison.Ordinal),
    };

    /// <summary>Nom affiché d'une touche enregistrée (nom de l'énumération <see cref="Key"/>), tel quel s'il est inconnu.</summary>
    public static string KeyName(string name) => KeyboardInput.Parse(name) is { } key ? KeyName(key) : name;

    private static BindingText Show(GamepadButton button, GlyphStyle glyphs) => button switch
    {
        GamepadButton.South or GamepadButton.East or GamepadButton.West or GamepadButton.North => glyphs.Face(button),
        GamepadButton.Back => new(glyphs.Pick(ControllerGlyphs.Select, ControllerGlyphs.Select, ControllerGlyphs.Minus), null),
        GamepadButton.Start => new(glyphs.Pick(ControllerGlyphs.Start, ControllerGlyphs.Start, ControllerGlyphs.Plus), null),
        GamepadButton.LeftStick => new(ControllerGlyphs.LeftStick, Tr.Get("win_binding_stick_click")),
        GamepadButton.RightStick => new(ControllerGlyphs.RightStick, Tr.Get("win_binding_stick_click")),
        GamepadButton.LeftShoulder => new(glyphs.Pick(ControllerGlyphs.LB, ControllerGlyphs.L1, ControllerGlyphs.L), null),
        GamepadButton.RightShoulder => new(glyphs.Pick(ControllerGlyphs.RB, ControllerGlyphs.R1, ControllerGlyphs.R), null),
        GamepadButton.DPadUp => new(ControllerGlyphs.DPadUp, null),
        GamepadButton.DPadDown => new(ControllerGlyphs.DPadDown, null),
        GamepadButton.DPadLeft => new(ControllerGlyphs.DPadLeft, null),
        GamepadButton.DPadRight => new(ControllerGlyphs.DPadRight, null),
        GamepadButton.Guide => new(null, Tr.Get("win_binding_guide")),
        GamepadButton.Touchpad => new(null, Tr.Get("win_binding_touchpad")),
        _ => new(null, button.ToString()),
    };

    private static BindingText Show(GamepadAxis axis, InputDirection? direction, GlyphStyle glyphs) => axis switch
    {
        GamepadAxis.LeftX or GamepadAxis.LeftY => new(ControllerGlyphs.LeftStick, StickArrow(axis, direction)),
        GamepadAxis.RightX or GamepadAxis.RightY => new(ControllerGlyphs.RightStick, StickArrow(axis, direction)),
        GamepadAxis.LeftTrigger => new(glyphs.Pick(ControllerGlyphs.LT, ControllerGlyphs.L2, ControllerGlyphs.ZL), null),
        _ => new(glyphs.Pick(ControllerGlyphs.RT, ControllerGlyphs.R2, ControllerGlyphs.ZR), null),
    };

    // Axes vertical de SDL : vers le haut = valeurs négatives.
    private static string StickArrow(GamepadAxis axis, InputDirection? direction) =>
        (axis is GamepadAxis.LeftX or GamepadAxis.RightX, direction == InputDirection.Negative) switch
        {
            (true, true) => "←",
            (true, false) => "→",
            (false, true) => "↑",
            (false, false) => "↓",
        };

    private static string Arrow(InputDirection? direction) => direction switch
    {
        InputDirection.Up => "↑",
        InputDirection.Down => "↓",
        InputDirection.Left => "←",
        _ => "→",
    };
}
