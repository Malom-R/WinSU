using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using WinSU.App.Input;
using WinSU.App.ViewModels;

namespace WinSU.App.Views.Common;

/// <summary>
/// Écran « Boutons » (M11 ; étape 32, maquette Sprite-0006) : commun aux trois styles ; ses panneaux s'arrêtent au-dessus
/// de la barre d'aide.
/// </summary>
public partial class ButtonsView : UserControl
{
    public ButtonsView()
    {
        InitializeComponent();
        FocusRequests.Follow(this, ButtonOf);

        // Les aides (Remplacer, Retirer, Ajouter) et Menu dépendent de l'élément qui a le focus.
        GotFocus += (_, e) =>
        {
            if (DataContext is ButtonsViewModel buttons && e.Source is Control source)
                buttons.Focused = source.DataContext is BindingChipViewModel or BindingRowViewModel ? source.DataContext : null;
        };
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        FocusNavigator.FocusFirstIn(RowsPanel);
    }

    /// <summary>
    /// Après une modification, les pastilles de la ligne sont recréées : le focus revient sur la pastille demandée, ou
    /// sur « Ajouter » pour une ligne (sur sa dernière pastille quand elle en a deux : plus d'« Ajouter », D-130).
    /// </summary>
    private Button? ButtonOf(object target)
    {
        var buttons = RowsPanel.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible).ToList();
        var button = buttons.FirstOrDefault(b => b.DataContext == target && (target is not BindingRowViewModel || b.Classes.Contains("add")));
        if (button is null && target is BindingRowViewModel row)
            button = buttons.LastOrDefault(b => b.DataContext is BindingChipViewModel chip && chip.Row == row);
        return button;
    }
}
