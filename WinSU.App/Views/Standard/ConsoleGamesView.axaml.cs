using Avalonia.Controls;
using Avalonia.Interactivity;
using WinSU.App.Input;

namespace WinSU.App.Views.Standard;

public partial class ConsoleGamesView : UserControl
{
    public ConsoleGamesView()
    {
        InitializeComponent();
        ListFocus.KeepOnReset(TileList); // tuiles recréées à la fin d'une partie (date de dernière partie)
        FocusRequests.Follow(this, TileList);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        ListFocus.FocusSelected(TileList);
    }
}
