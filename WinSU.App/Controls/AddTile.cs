using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace WinSU.App.Controls;

/// <summary>
/// La tuile « + » (Ajouter des jeux, Ajouter une console), la même partout (étape 36) : une icône générée magenta
/// (<c>Border.tileGenerated.add</c>), son signe à la taille de sa boîte (<see cref="TileMetrics.AddGlyphSize"/>, D-186).
/// Dans la boîte d'un jeu sauf si <see cref="TileArt.Size"/> dit autre chose.
/// </summary>
public sealed class AddTile : TileArt
{
    private readonly TextBlock _glyph = new()
    {
        Text = "+",
        FontWeight = FontWeight.Light,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public AddTile()
    {
        Children.Add(new Border { Classes = { "tileGenerated", "add" }, Child = _glyph });
        Size = TileMetrics.GameSize;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WidthProperty)
            _glyph.FontSize = TileMetrics.AddGlyphSize(Width);
    }
}
