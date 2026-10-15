using Avalonia.Data.Converters;

namespace WinSU.App.Controls;

/// <summary>
/// Mesures de l'aperçu de la manette (<see cref="GamepadPreview"/>, étape 22 ; dessin propre à WinSU depuis l'étape 38,
/// tracés écrits par tools/dev/gamepad_shape.py) : la manette est dessinée dans un repère de <see cref="Width"/> ×
/// <see cref="Height"/>, agrandi ou réduit à la place disponible. Les sticks et les gâchettes reçoivent de l'état de la
/// manette des valeurs de −1 à 1 (0 à 1 pour une gâchette), converties ici en positions.
/// </summary>
public static class GamepadMetrics
{
    public const double Width = 766;
    public const double Height = 660;

    /// <summary>Pastille d'un stick (cercle) et stick lui-même, qui s'y déplace de <see cref="StickTravel"/> au plus.</summary>
    public const double StickBaseSize = 120;
    public const double StickKnobSize = 90;
    public const double StickTravel = 12;

    /// <summary>Hauteur d'une gâchette (pilule verticale) : elle se remplit par le bas selon la pression.</summary>
    public const double TriggerHeight = 68;

    /// <summary>Position du stick dans sa pastille, d'après la valeur d'un axe (−1 à 1).</summary>
    public static readonly IValueConverter StickKnobOffset =
        new FuncValueConverter<double, double>(value => ((StickBaseSize - StickKnobSize) / 2) + (Math.Clamp(value, -1, 1) * StickTravel));

    /// <summary>Hauteur remplie d'une gâchette, d'après sa pression (0 à 1).</summary>
    public static readonly IValueConverter TriggerFill =
        new FuncValueConverter<double, double>(value => Math.Clamp(value, 0, 1) * TriggerHeight);
}
