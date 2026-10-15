using Avalonia;
using Avalonia.Controls;

namespace WinSU.App.Input;

/// <summary>
/// Marque une liste de choix où l'on choisit par Confirmer (étape 26, D-78) : le focus s'y promène sans changer la
/// sélection, qui reste l'option en vigueur (boule verte) ; Confirmer (ou un clic) choisit l'élément qui a le focus.
/// Posée par le style des rangées de cartes (<c>ListBox.cards</c>). Ailleurs (grilles de tuiles…), la sélection suit
/// toujours le focus (<see cref="FocusNavigator.Focus"/>).
/// </summary>
public sealed class ConfirmToSelect
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<ConfirmToSelect, Control, bool>("IsEnabled");

    private ConfirmToSelect()
    {
    }

    public static bool GetIsEnabled(Control control) => control.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(Control control, bool value) => control.SetValue(IsEnabledProperty, value);
}
