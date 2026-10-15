using Avalonia.Controls;
using Avalonia.Interactivity;
using WinSU.App.Controls;
using WinSU.App.Overlays;

namespace WinSU.App.Views.Common;

/// <summary>
/// Menu de tous les styles (30.14) : le premier choix de chaque groupe porte la classe « groupStart » (l'espace au-dessus,
/// Themes/Controls.axaml) ; à l'ouverture, le panneau arrive depuis le bord droit.
/// </summary>
public partial class ContextMenuView : UserControl
{
    public ContextMenuView()
    {
        InitializeComponent();
        // Hors de l'écran jusqu'à l'entrée (Loaded vient après le premier affichage : il s'y montrait une image)
        SlideIn.Hold(MenuPanel, 4000);
        MenuList.ContainerPrepared += (_, e) =>
            e.Container.Classes.Set("groupStart", e.Index < MenuList.ItemCount && MenuList.Items[e.Index] is ContextMenuItem { StartsGroup: true });
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        SlideIn.Play(MenuPanel, (MenuPanel.Bounds.Width > 0 ? MenuPanel.Bounds.Width : MenuPanel.Width) + 24);
    }
}
