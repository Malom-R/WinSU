using Avalonia;
using Avalonia.Controls;

namespace WinSU.App.Input;

/// <summary>
/// Marque une zone parcourue « en lignes » (<see cref="Core.Input.SpatialNavigation.FindInRows"/>) : Haut et Bas passent
/// à la ligne suivante sans en sauter aucune. Pour les pages de réglages, où des éléments étroits (interrupteurs,
/// curseurs) côtoient de larges rangées de cartes et la navigation spatiale les enjambait.
/// </summary>
public sealed class RowNavigation
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<RowNavigation, Control, bool>("IsEnabled");

    private RowNavigation()
    {
    }

    public static bool GetIsEnabled(Control control) => control.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(Control control, bool value) => control.SetValue(IsEnabledProperty, value);
}
