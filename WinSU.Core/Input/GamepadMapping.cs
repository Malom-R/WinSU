using System.Globalization;

namespace WinSU.Core.Input;

/// <summary>
/// Correspondance de SDL d'une manette reconnue (« mapping » : <c>guid,nom,a:b0,b:b1,dpup:h0.1,leftx:a0,…</c>) : traduit
/// une liaison brute (bouton, croix ou axe par numéro) vers la position qu'elle occupe sur cette manette. Sert à afficher
/// le glyphe d'une ancienne liaison brute, d'avant M11 (DF-4, étape 35) ; les liaisons enregistrées ne changent pas.
/// </summary>
public static class GamepadMapping
{
    private static readonly Dictionary<string, GamepadButton> Buttons = new(StringComparer.OrdinalIgnoreCase)
    {
        ["a"] = GamepadButton.South,
        ["b"] = GamepadButton.East,
        ["x"] = GamepadButton.West,
        ["y"] = GamepadButton.North,
        ["back"] = GamepadButton.Back,
        ["guide"] = GamepadButton.Guide,
        ["start"] = GamepadButton.Start,
        ["leftstick"] = GamepadButton.LeftStick,
        ["rightstick"] = GamepadButton.RightStick,
        ["leftshoulder"] = GamepadButton.LeftShoulder,
        ["rightshoulder"] = GamepadButton.RightShoulder,
        ["dpup"] = GamepadButton.DPadUp,
        ["dpdown"] = GamepadButton.DPadDown,
        ["dpleft"] = GamepadButton.DPadLeft,
        ["dpright"] = GamepadButton.DPadRight,
        ["misc1"] = GamepadButton.Misc1,
        ["paddle1"] = GamepadButton.RightPaddle1,
        ["paddle2"] = GamepadButton.LeftPaddle1,
        ["paddle3"] = GamepadButton.RightPaddle2,
        ["paddle4"] = GamepadButton.LeftPaddle2,
        ["touchpad"] = GamepadButton.Touchpad,
    };

    private static readonly Dictionary<string, GamepadAxis> Axes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["leftx"] = GamepadAxis.LeftX,
        ["lefty"] = GamepadAxis.LeftY,
        ["rightx"] = GamepadAxis.RightX,
        ["righty"] = GamepadAxis.RightY,
        ["lefttrigger"] = GamepadAxis.LeftTrigger,
        ["righttrigger"] = GamepadAxis.RightTrigger,
    };

    /// <summary>La liaison telle qu'on l'affiche : sa position sur la manette si la correspondance la connaît, sinon elle-même.</summary>
    public static InputBinding ForDisplay(InputBinding binding, string? mapping) =>
        TryToPosition(mapping, binding, out var position) ? position : binding;

    /// <summary>La position (bouton ou axe dans un sens) d'une liaison brute selon la correspondance ; false si elle n'y est pas.</summary>
    public static bool TryToPosition(string? mapping, InputBinding raw, out InputBinding position)
    {
        position = raw;
        if (string.IsNullOrEmpty(mapping) || raw.IsPosition())
            return false;

        foreach (var field in mapping.Split(',').Skip(2)) // le guid et le nom d'abord
        {
            var colon = field.IndexOf(':');
            if (colon <= 0 || Source(field[(colon + 1)..].Trim(), raw) is not { } sign)
                continue;

            var output = field[..colon].Trim();
            var half = output[0] switch { '+' => 1, '-' => -1, _ => 0 }; // « +leftx » : une moitié de l'axe
            var name = half == 0 ? output : output[1..];
            if (half == 0 && Buttons.TryGetValue(name, out var button))
            {
                position = InputBinding.Pad(button);
                return true;
            }

            if (Axes.TryGetValue(name, out var axis))
            {
                var direction = half != 0 ? half : sign;
                if (direction == 0 || (direction < 0 && axis is GamepadAxis.LeftTrigger or GamepadAxis.RightTrigger))
                    continue;

                position = InputBinding.Pad(axis, direction > 0 ? InputDirection.Positive : InputDirection.Negative);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// La liaison brute correspond-elle à cette source (<c>b3</c>, <c>h0.1</c>, <c>a2</c>, <c>+a2</c>, <c>-a2</c>, <c>a2~</c>) ?
    /// Null si non ; sinon le sens qu'elle donne à un axe de sortie : +1, −1, ou 0 pour un bouton ou une croix.
    /// </summary>
    private static int? Source(string source, InputBinding raw)
    {
        if (source.Length < 2)
            return null;

        switch (raw.Kind)
        {
            case InputBindingKind.Button:
                return source == "b" + raw.Index.ToString(CultureInfo.InvariantCulture) ? 0 : null;

            case InputBindingKind.Hat:
                var mask = raw.Direction switch
                {
                    InputDirection.Up => 1,
                    InputDirection.Right => 2,
                    InputDirection.Down => 4,
                    InputDirection.Left => 8,
                    _ => 0,
                };
                return source == $"h{raw.Index.ToString(CultureInfo.InvariantCulture)}.{mask.ToString(CultureInfo.InvariantCulture)}" ? 0 : null;

            case InputBindingKind.Axis:
                var half = source[0] switch { '+' => 1, '-' => -1, _ => 0 };
                var body = half == 0 ? source : source[1..];
                var inverted = body.EndsWith('~');
                if (inverted)
                    body = body[..^1];
                if (body != "a" + raw.Index.ToString(CultureInfo.InvariantCulture))
                    return null;

                var pushed = raw.Direction == InputDirection.Negative ? -1 : 1;
                if (half != 0 && half != pushed)
                    return null;
                return inverted ? -pushed : pushed;

            default:
                return null;
        }
    }
}
