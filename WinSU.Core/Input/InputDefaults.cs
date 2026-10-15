namespace WinSU.Core.Input;

/// <summary>
/// Liaisons par défaut (M11) : disposition standard des manettes reconnues (celle du profil par défaut depuis M2) et
/// touches du clavier (celles de M2, fixes jusqu'à M11). « Rétablir par défaut » y revient.
/// </summary>
public static class InputDefaults
{
    /// <summary>Croix et stick gauche pour les directions ; Sud, Est, Ouest, Start, LB et RB pour les actions.</summary>
    public static IReadOnlyDictionary<InputAction, IReadOnlyList<InputBinding>> Controller { get; } =
        new Dictionary<InputAction, IReadOnlyList<InputBinding>>
        {
            [InputAction.Up] = [InputBinding.Pad(GamepadButton.DPadUp), InputBinding.Pad(GamepadAxis.LeftY, InputDirection.Negative)],
            [InputAction.Down] = [InputBinding.Pad(GamepadButton.DPadDown), InputBinding.Pad(GamepadAxis.LeftY, InputDirection.Positive)],
            [InputAction.Left] = [InputBinding.Pad(GamepadButton.DPadLeft), InputBinding.Pad(GamepadAxis.LeftX, InputDirection.Negative)],
            [InputAction.Right] = [InputBinding.Pad(GamepadButton.DPadRight), InputBinding.Pad(GamepadAxis.LeftX, InputDirection.Positive)],
            [InputAction.Confirm] = [InputBinding.Pad(GamepadButton.South)],
            [InputAction.Back] = [InputBinding.Pad(GamepadButton.East)],
            [InputAction.ContextMenu] = [InputBinding.Pad(GamepadButton.West)],
            [InputAction.Start] = [InputBinding.Pad(GamepadButton.Start)],
            [InputAction.PagePrevious] = [InputBinding.Pad(GamepadButton.LeftShoulder)],
            [InputAction.PageNext] = [InputBinding.Pad(GamepadButton.RightShoulder)],
        };

    /// <summary>Touches par action, par leur nom dans l'énumération <c>Key</c> d'Avalonia (l'interface les lit).</summary>
    public static IReadOnlyDictionary<InputAction, IReadOnlyList<string>> Keyboard { get; } =
        new Dictionary<InputAction, IReadOnlyList<string>>
        {
            [InputAction.Up] = ["Up"],
            [InputAction.Down] = ["Down"],
            [InputAction.Left] = ["Left"],
            [InputAction.Right] = ["Right"],
            [InputAction.Confirm] = ["Enter", "Space"],
            [InputAction.Back] = ["Escape", "Back"],
            [InputAction.ContextMenu] = ["Apps"],
            [InputAction.Start] = ["Home"],
            [InputAction.PagePrevious] = ["PageUp"],
            [InputAction.PageNext] = ["PageDown"],
        };

    /// <summary>
    /// Actions sans lesquelles on ne peut plus se servir de WinSU (les directions, Valider, Retour) : elles gardent
    /// toujours au moins une liaison au clavier et à la manette (garde-fou de M11).
    /// </summary>
    public static bool IsEssential(InputAction action) =>
        action is InputAction.Up or InputAction.Down or InputAction.Left or InputAction.Right or InputAction.Confirm or InputAction.Back;
}
