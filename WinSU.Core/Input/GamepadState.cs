namespace WinSU.Core.Input;

/// <summary>
/// État instantané d'une manette reconnue, pour l'aperçu de la manette de l'assistant (étape 22) : boutons enfoncés,
/// indexés par <see cref="GamepadButton"/>, et axes, indexés par <see cref="GamepadAxis"/> : sticks de −1 à 1 (vers le
/// haut = négatif, comme SDL), gâchettes de 0 à 1.
/// </summary>
public sealed record GamepadState(ControllerInfo Controller, IReadOnlyList<bool> Buttons, IReadOnlyList<float> Axes)
{
    public bool IsPressed(GamepadButton button) => (int)button < Buttons.Count && Buttons[(int)button];

    public float Axis(GamepadAxis axis) => (int)axis < Axes.Count ? Axes[(int)axis] : 0f;

    /// <summary>
    /// La liaison est-elle tenue en ce moment ? Bouton enfoncé, ou axe poussé au-delà du seuil dans son sens. Une
    /// liaison brute (numéro de joystick) n'est jamais reconnue ici.
    /// </summary>
    public bool Holds(InputBinding binding, double threshold) => binding.Kind switch
    {
        InputBindingKind.GamepadButton => IsPressed((GamepadButton)binding.Index),
        InputBindingKind.GamepadAxis when binding.Direction == InputDirection.Negative => Axis((GamepadAxis)binding.Index) <= -threshold,
        InputBindingKind.GamepadAxis => Axis((GamepadAxis)binding.Index) >= threshold,
        _ => false,
    };

    /// <summary>Valeur d'un axe de SDL (−32768 à 32767) ramenée de −1 à 1.</summary>
    public static float Normalize(short value) => Math.Max(-1f, value / 32767f);
}
