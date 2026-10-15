using Avalonia.Controls;

namespace WinSU.App.Controls;

/// <summary>
/// Le squelette d'une tuile, écrit une seule fois pour toutes les tuiles (étape 36, C-6) : dans sa boîte, les équerres
/// (<see cref="TileCursor"/>) et le verre (<see cref="TileGlass"/>), puis la face que la vue pose dedans (jaquette, image de
/// la console, « + »…), puis l'éclair de l'appui (<c>Border.tileFlash</c>) et le balayage (<see cref="TileSheen"/>), toujours
/// par-dessus la face. Les styles le voient comme un <c>Panel.tileArt</c> (Themes/Controls.axaml : couches, survol, tuile
/// choisie) ; Input/PressFeedback y trouve la boîte et l'éclair. Ses classes s'ajoutent une à une (<c>Classes.logo</c>) :
/// une liste entière (<c>Classes="…"</c>) remplacerait <c>tileArt</c>.
/// </summary>
public class TileArt : Panel
{
    public TileArt()
    {
        Classes.Add("tileArt");
        Children.Add(new TileCursor());
        Children.Add(new TileGlass());
        // Par-dessus tout ce que la vue ajoute ensuite (la face, un badge) : un rang d'affichage plus haut
        Children.Add(new Border { Classes = { "tileFlash" }, ZIndex = 1 });
        Children.Add(new TileSheen { ZIndex = 1 });
    }

    /// <summary>Côté de la boîte carrée : <see cref="TileMetrics.GameSize"/> (jeux), <see cref="TileMetrics.ConsoleSize"/> (consoles).</summary>
    public double Size
    {
        get => Width;
        set
        {
            Width = value;
            Height = value;
        }
    }

    protected override Type StyleKeyOverride => typeof(Panel);
}
