using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace WinSU.App.Views.WiiU;

/// <summary>
/// Nom d'un élément du style Wii U (tuile de la grille, icône du bas), affiché dans la pastille en haut à droite quand il
/// a le focus (D-66, à la place de la bulle au-dessus de la tuile). Posé sur la racine du modèle d'un élément de liste ou
/// sur le bouton d'une icône.
/// </summary>
public sealed class FocusName
{
    public static readonly AttachedProperty<string?> TextProperty =
        AvaloniaProperty.RegisterAttached<FocusName, Control, string?>("Text");

    private FocusName()
    {
    }

    public static string? GetText(Control control) => control.GetValue(TextProperty);

    public static void SetText(Control control, string? value) => control.SetValue(TextProperty, value);

    /// <summary>
    /// Nom de l'élément qui a pris le focus : le sien, celui d'un parent (icône), ou celui du contenu d'un élément de
    /// liste (le focus est sur le conteneur) ; null s'il n'en a pas (Réglages…).
    /// </summary>
    public static string? Of(Visual? focused)
    {
        for (var current = focused; current is not null; current = current.GetVisualParent())
        {
            if (current is Control control && GetText(control) is { } own)
                return own;
            if (current is ListBoxItem)
                break;
        }

        return focused?.GetVisualDescendants().OfType<Control>().Select(GetText).FirstOrDefault(text => text is not null);
    }
}
