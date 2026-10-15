using Avalonia.Controls;
using WinSU.Core.Input;

namespace WinSU.App.Input;

/// <summary>
/// File qui glisse (étape 30, <see cref="Controls.TrackPanel"/>) : ses éléments se suivent sur une ligne, droite ou en
/// diagonale ; deux directions y mènent à l'élément précédent et au suivant, quelle que soit leur place à l'écran (la file
/// de l'accueil du Standard 2.0 est en diagonale, mais on la parcourt avec Haut et Bas).
/// </summary>
public interface ITrackArea
{
    /// <summary>La direction mène d'un élément de la file à son voisin (les autres restent spatiales).</summary>
    bool Handles(NavigationDirection direction);

    /// <summary>L'élément voisin dans cette direction ; null au bout de la file (on y reste).</summary>
    Control? Neighbour(Control from, NavigationDirection direction);
}
