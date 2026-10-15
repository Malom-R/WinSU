using Avalonia.Controls;
using Avalonia.Layout;

namespace WinSU.App.Controls;

public partial class ConsoleTile : UserControl
{
    private bool _gameBox;

    public ConsoleTile() => InitializeComponent();

    /// <summary>
    /// Dans la boîte d'un jeu (<see cref="TileMetrics.GameSize"/>) au lieu de celle d'une console : style Wii U, où consoles,
    /// jeux et cadres vides ont, une fois mis à l'échelle de la page, les mêmes coins et le même liseré (DF-6).
    /// </summary>
    public bool GameBox
    {
        get => _gameBox;
        set
        {
            _gameBox = value;
            Art.Size = value ? TileMetrics.GameSize : TileMetrics.ConsoleSize;
            if (value)
            {
                ArtImage.Width = TileMetrics.ConsoleArtSizeInGameBox;
                ArtImage.Height = TileMetrics.ConsoleArtSizeInGameBox;
            }
            else
            {
                ArtImage.ClearValue(Layoutable.WidthProperty);
                ArtImage.ClearValue(Layoutable.HeightProperty);
            }
        }
    }
}
