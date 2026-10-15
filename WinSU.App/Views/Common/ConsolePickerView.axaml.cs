using Avalonia.Controls;
using Avalonia.Interactivity;
using WinSU.App.Input;

namespace WinSU.App.Views.Common;

/// <summary>
/// « Ajouter une console » (M3 ; étape 32, maquette Sprite-0003) : commun aux trois styles ; passe sous la barre d'aide du
/// Standard et du Wii U, ses tuiles atténuées dessous.
/// </summary>
public partial class ConsolePickerView : UserControl, IHintBarPlacement
{
    public ConsolePickerView()
    {
        InitializeComponent();
        FocusRequests.Follow(this, TileList); // saut d'un fabricant à l'autre
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        ListFocus.FocusSelected(TileList);
    }
}
