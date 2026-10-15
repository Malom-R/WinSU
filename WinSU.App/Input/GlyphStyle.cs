using WinSU.Core.Input;
using WinSU.Core.Settings;

namespace WinSU.App.Input;

/// <summary>Couleur d'un glyphe (réglage « Glyphes en couleurs », D-34) ; <see cref="None"/> : couleur du texte.</summary>
public enum GlyphTint
{
    None,
    Green,
    Red,
    Blue,
    Yellow,
    Pink,
}

/// <summary>
/// Style des glyphes en vigueur (M12) : marque des boutons (jamais <see cref="ButtonStyle.Automatic"/>) et couleurs.
/// Les liaisons étant enregistrées par position (M11), chaque style donne le glyphe de chaque position : Xbox (A B X Y,
/// LB RB, LT RT), PlayStation (✕ ○ □ △, L1 R1, L2 R2), Switch (la lettre suit la position, D-28 : bas = B, droite = A,
/// gauche = Y, haut = X ; L R, ZL ZR, + −). La croix et les sticks sont les mêmes pour tous.
/// </summary>
public readonly record struct GlyphStyle(ButtonStyle Style, bool Colored = false)
{
    /// <summary>Xbox, d'une seule couleur : les valeurs par défaut quand aucune manette n'est connue.</summary>
    public static GlyphStyle Default => new(ButtonStyle.Xbox);

    /// <summary>Style des réglages ; l'ancien « Automatique » (retiré, D-42) vaut Xbox.</summary>
    public static GlyphStyle From(AppearanceSettings appearance) => new(Normalize(appearance.ButtonStyle), appearance.ColoredGlyphs);

    public static ButtonStyle Normalize(ButtonStyle style) => style == ButtonStyle.Automatic ? ButtonStyle.Xbox : style;

    /// <summary>Glyphe d'un bouton de face (Sud, Est, Ouest, Nord), en couleur si le réglage le demande.</summary>
    public BindingText Face(GamepadButton button)
    {
        var (glyph, tint) = (Style, button) switch
        {
            (ButtonStyle.PlayStation, GamepadButton.South) => (ControllerGlyphs.Cross, GlyphTint.Blue),
            (ButtonStyle.PlayStation, GamepadButton.East) => (ControllerGlyphs.Circle, GlyphTint.Red),
            (ButtonStyle.PlayStation, GamepadButton.West) => (ControllerGlyphs.Square, GlyphTint.Pink),
            (ButtonStyle.PlayStation, _) => (ControllerGlyphs.Triangle, GlyphTint.Green),

            // Les boutons d'une manette Switch sont tous gris : pas de couleur.
            (ButtonStyle.Switch, GamepadButton.South) => (ControllerGlyphs.B, GlyphTint.None),
            (ButtonStyle.Switch, GamepadButton.East) => (ControllerGlyphs.A, GlyphTint.None),
            (ButtonStyle.Switch, GamepadButton.West) => (ControllerGlyphs.Y, GlyphTint.None),
            (ButtonStyle.Switch, _) => (ControllerGlyphs.X, GlyphTint.None),

            (_, GamepadButton.South) => (ControllerGlyphs.A, GlyphTint.Green),
            (_, GamepadButton.East) => (ControllerGlyphs.B, GlyphTint.Red),
            (_, GamepadButton.West) => (ControllerGlyphs.X, GlyphTint.Blue),
            _ => (ControllerGlyphs.Y, GlyphTint.Yellow),
        };
        return new BindingText(glyph, null, Colored ? tint : GlyphTint.None);
    }

    /// <summary>Les quatre boutons de face dans l'ordre habituel de la marque (A B X Y, ✕ ○ □ △) : aperçu des Réglages.</summary>
    public IReadOnlyList<BindingText> FaceButtons
    {
        get
        {
            GamepadButton[] order = Style == ButtonStyle.Switch
                ? [GamepadButton.East, GamepadButton.South, GamepadButton.North, GamepadButton.West]
                : [GamepadButton.South, GamepadButton.East, GamepadButton.West, GamepadButton.North];
            var style = this;
            return [.. order.Select(button => style.Face(button))];
        }
    }

    /// <summary>Le glyphe de la marque parmi trois : Xbox, PlayStation, Switch.</summary>
    public string Pick(string xbox, string playStation, string nintendo) => Style switch
    {
        ButtonStyle.PlayStation => playStation,
        ButtonStyle.Switch => nintendo,
        _ => xbox,
    };
}
