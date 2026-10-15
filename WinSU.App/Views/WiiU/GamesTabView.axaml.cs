namespace WinSU.App.Views.WiiU;

public partial class GamesTabView : WiiUGridPage
{
    public GamesTabView()
    {
        InitializeComponent();
        UseGrid(TileList, Slots, Pager);
    }
}
