namespace WinSU.App.Views.Switch;

public partial class ConsoleGamesView : SwitchGridPage, IHintBarPlacement
{
    public ConsoleGamesView()
    {
        InitializeComponent();
        UseGrid(TileList, AddButton);
    }
}
