namespace WinSU.App.Views;

/// <summary>
/// Vue d'un onglet qui choisit elle-même où va le focus à son ouverture (étape 30) : dans les files qui glissent du
/// Standard 2.0, l'ordre de lecture n'a pas de sens (l'élément le plus haut est souvent hors de l'écran) ; l'accueil donne
/// le focus à sa tuile choisie, les Jeux à la console choisie. False : rien à focaliser (la coquille prend le premier
/// élément dans l'ordre de lecture).
/// </summary>
public interface IInitialFocus
{
    bool FocusInitial();
}
