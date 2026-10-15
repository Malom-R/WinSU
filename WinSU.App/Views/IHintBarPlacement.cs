namespace WinSU.App.Views;

/// <summary>
/// Vue de page qui passe sous la barre d'aide au lieu de s'arrêter au-dessus (une seule façon de le dire depuis l'étape 36,
/// qui remplace IExtendsUnderHintBar et IPassesUnderHintBar) : les coquilles des trois styles (elles gardent elles-mêmes la
/// place de la barre ; style Switch, M15 : la bannière héro passe derrière, D-51), les écrans communs dont les listes
/// atténuent ce qui passe dessous (Ajouter une console, recherche SteamGridDB : étape 32, D-127 ; aussi sous la barre du
/// Switch depuis l'étape 33). Une vue qui en décide selon ce qu'elle montre (style Wii U, M16 : D-60, D-63) redéfinit ses
/// valeurs ; la fenêtre les relit à chaque <see cref="HintBarPlacementChanged"/>.
/// </summary>
public interface IHintBarPlacement
{
    bool ExtendsUnderHintBar => true;

    bool HidesHintBar => false;

    event EventHandler? HintBarPlacementChanged
    {
        add { }
        remove { }
    }
}
