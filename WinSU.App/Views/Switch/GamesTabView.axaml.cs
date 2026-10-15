namespace WinSU.App.Views.Switch;

public partial class GamesTabView : SwitchGridPage
{
    public GamesTabView()
    {
        InitializeComponent();
        UseGrid(TileList, AddButton);
    }
}
