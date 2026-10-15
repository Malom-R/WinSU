using Avalonia.Controls;
using WinSU.Core.Input;

namespace WinSU.App.Input;

/// <summary>
/// Zone parcourue par pages (grille du style Wii U, M16) : seuls ses éléments de la page affichée sont atteignables ;
/// Gauche et Droite y restent et tournent la page au bord ; LB/RB tournent la page (<see cref="FocusNavigator"/>).
/// </summary>
public interface IPagedArea
{
    /// <summary>L'élément est sur la page affichée.</summary>
    bool Shows(Control element);

    /// <summary>L'élément voisin (Gauche, Droite), sur la page voisine au bord ; null s'il n'y a rien de ce côté.</summary>
    Control? Neighbour(Control from, NavigationDirection direction);

    /// <summary>
    /// Tourne la page et renvoie l'élément à la même place (le premier sans élément de départ) ; null s'il n'y a pas de
    /// page de ce côté.
    /// </summary>
    Control? TurnPage(Control? from, int offset);
}
