namespace WinSU.Core.Input;

/// <summary>
/// Style des glyphes de boutons de manette (M12) : lettres ou symboles d'une marque. Les liaisons étant enregistrées par
/// position (M11), chaque style donne le glyphe de chaque position (Switch : bouton du bas = B, D-28). Xbox par défaut.
/// </summary>
public enum ButtonStyle
{
    /// <summary>
    /// Ancien choix « Automatique » (M12, retiré par D-42) : gardé pour relire les anciens <c>settings.json</c> (une valeur
    /// inconnue rendrait tout le fichier illisible) ; vaut Xbox.
    /// </summary>
    Automatic,
    Xbox,
    PlayStation,
    Switch,
}
