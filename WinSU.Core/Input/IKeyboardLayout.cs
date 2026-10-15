namespace WinSU.Core.Input;

/// <summary>
/// La disposition du clavier de l'utilisateur (AZERTY, QWERTY…), pour écrire les bonnes lettres sur le clavier dessiné de
/// l'écran « Boutons » (étape 32, D-130).
/// </summary>
public interface IKeyboardLayout
{
    /// <summary>
    /// Le caractère qu'écrit la touche de ce code de balayage (position physique, jeu 1 : 0x10 = la touche à droite de
    /// Tab), en majuscule ; null si elle n'écrit rien (Maj, Entrée…).
    /// </summary>
    string? CharacterOf(int scanCode);
}
