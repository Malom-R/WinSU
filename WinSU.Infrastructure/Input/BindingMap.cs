using System.Collections.Frozen;
using WinSU.Core.Input;

namespace WinSU.Infrastructure.Input;

/// <summary>Table « entrée (brute ou par position) → actions », construite à partir des liaisons des réglages.</summary>
internal sealed class BindingMap
{
    private readonly FrozenDictionary<(InputBindingKind Kind, int Index, InputDirection? Direction), InputAction[]> _map;

    private BindingMap(FrozenDictionary<(InputBindingKind, int, InputDirection?), InputAction[]> map)
    {
        _map = map;
        HasRaw = map.Keys.Any(key => key.Item1 is InputBindingKind.Button or InputBindingKind.Axis or InputBindingKind.Hat);
    }

    /// <summary>
    /// La table contient au moins une entrée brute. Sinon, une manette non reconnue n'aurait aucune liaison : elle
    /// prend la <see cref="GenericJoystick"/>.
    /// </summary>
    public bool HasRaw { get; }

    public static BindingMap From(IReadOnlyDictionary<InputAction, IReadOnlyList<InputBinding>> bindings) =>
        new(bindings
            .SelectMany(pair => pair.Value.Select(binding => (Key: KeyOf(binding), Action: pair.Key)))
            .GroupBy(entry => entry.Key)
            .ToFrozenDictionary(group => group.Key, group => group.Select(entry => entry.Action).Distinct().ToArray()));

    public IReadOnlyList<InputAction> Lookup(InputBinding binding) =>
        _map.TryGetValue(KeyOf(binding), out var actions) ? actions : [];

    /// <summary>
    /// Profil par défaut pour une manette que SDL ne reconnaît pas comme « gamepad » :
    /// croix et stick gauche pour les directions, boutons 0 à 3 pour les actions principales.
    /// </summary>
    public static BindingMap GenericJoystick { get; } = From(new Dictionary<InputAction, IReadOnlyList<InputBinding>>
    {
        [InputAction.Up] = [Hat(InputDirection.Up), Axis(1, InputDirection.Negative)],
        [InputAction.Down] = [Hat(InputDirection.Down), Axis(1, InputDirection.Positive)],
        [InputAction.Left] = [Hat(InputDirection.Left), Axis(0, InputDirection.Negative)],
        [InputAction.Right] = [Hat(InputDirection.Right), Axis(0, InputDirection.Positive)],
        [InputAction.Confirm] = [Button(0)],
        [InputAction.Back] = [Button(1)],
        [InputAction.ContextMenu] = [Button(2)],
        [InputAction.Start] = [Button(3)],
    });

    // Un bouton n'a pas de direction, même si le fichier de réglages en contient une par erreur.
    private static (InputBindingKind, int, InputDirection?) KeyOf(InputBinding binding)
    {
        var normalized = binding.Normalized();
        return (normalized.Kind, normalized.Index, normalized.Direction);
    }

    private static InputBinding Button(int index) => InputBinding.RawButton(index);

    private static InputBinding Hat(InputDirection direction) => InputBinding.RawHat(0, direction);

    private static InputBinding Axis(int index, InputDirection direction) => InputBinding.RawAxis(index, direction);
}
