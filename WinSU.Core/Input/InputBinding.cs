namespace WinSU.Core.Input;

public enum InputBindingKind
{
    /// <summary>Bouton brut (API Joystick de SDL), par son numéro : manettes que SDL ne reconnaît pas.</summary>
    Button,

    /// <summary>Axe brut, dans un sens.</summary>
    Axis,

    /// <summary>Croix brute (« hat »), dans une direction.</summary>
    Hat,

    /// <summary>Bouton d'une manette reconnue, par sa position (<see cref="Input.GamepadButton"/>, M11).</summary>
    GamepadButton,

    /// <summary>Axe d'une manette reconnue (sticks, gâchettes : <see cref="Input.GamepadAxis"/>), dans un sens (M11).</summary>
    GamepadAxis,
}

public enum InputDirection
{
    /// <summary>Axe poussé vers les valeurs positives.</summary>
    Positive,

    /// <summary>Axe poussé vers les valeurs négatives.</summary>
    Negative,

    Up,
    Down,
    Left,
    Right,
}

/// <summary>
/// Boutons d'une manette reconnue par SDL, désignés par leur position et non par leur étiquette (le bouton du bas est
/// <see cref="South"/> : A sur une manette Xbox, ✕ sur une manette PlayStation, B sur une manette Nintendo).
/// Même numérotation que <c>SDL_GamepadButton</c> de SDL3.
/// </summary>
public enum GamepadButton
{
    South,
    East,
    West,
    North,
    Back,
    Guide,
    Start,
    LeftStick,
    RightStick,
    LeftShoulder,
    RightShoulder,
    DPadUp,
    DPadDown,
    DPadLeft,
    DPadRight,
    Misc1,
    RightPaddle1,
    LeftPaddle1,
    RightPaddle2,
    LeftPaddle2,
    Touchpad,
    Misc2,
    Misc3,
    Misc4,
    Misc5,
    Misc6,
}

/// <summary>Axes d'une manette reconnue. Même numérotation que <c>SDL_GamepadAxis</c> de SDL3.</summary>
public enum GamepadAxis
{
    LeftX,
    LeftY,
    RightX,
    RightY,
    LeftTrigger,
    RightTrigger,
}

/// <summary>
/// Entrée d'une manette associée à une <see cref="InputAction"/> : entrée brute (bouton, axe ou croix de l'API
/// Joystick de SDL, par numéro) ou, pour une manette reconnue, bouton ou axe désigné par sa position (M11).
/// <see cref="Index"/> : numéro brut, ou valeur de <see cref="GamepadButton"/> / <see cref="GamepadAxis"/>.
/// <see cref="Direction"/> ne sert qu'aux axes (<see cref="InputDirection.Positive"/> ou
/// <see cref="InputDirection.Negative"/>) et aux croix (haut, bas, gauche, droite).
/// </summary>
public sealed record InputBinding
{
    public InputBindingKind Kind { get; init; }
    public int Index { get; init; }
    public InputDirection? Direction { get; init; }

    /// <summary>
    /// Bouton ou axe désigné par sa position : n'a de sens que pour une manette reconnue. Méthode et non propriété : la
    /// sérialisation l'écrirait dans settings.json.
    /// </summary>
    public bool IsPosition() => Kind is InputBindingKind.GamepadButton or InputBindingKind.GamepadAxis;

    public static InputBinding Pad(GamepadButton button) =>
        new() { Kind = InputBindingKind.GamepadButton, Index = (int)button };

    public static InputBinding Pad(GamepadAxis axis, InputDirection direction) =>
        new() { Kind = InputBindingKind.GamepadAxis, Index = (int)axis, Direction = direction };

    public static InputBinding RawButton(int index) => new() { Kind = InputBindingKind.Button, Index = index };

    public static InputBinding RawAxis(int index, InputDirection direction) =>
        new() { Kind = InputBindingKind.Axis, Index = index, Direction = direction };

    public static InputBinding RawHat(int index, InputDirection direction) =>
        new() { Kind = InputBindingKind.Hat, Index = index, Direction = direction };

    /// <summary>
    /// La même liaison sans direction pour un bouton (un fichier de réglages peut en contenir une par erreur) : deux
    /// liaisons normalisées égales désignent la même entrée.
    /// </summary>
    public InputBinding Normalized() =>
        Kind is InputBindingKind.Button or InputBindingKind.GamepadButton && Direction is not null ? this with { Direction = null } : this;
}
