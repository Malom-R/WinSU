using WinSU.Core.Input;

namespace WinSU.App.Input;

/// <summary>
/// Implémenté par un écran (ViewModel) qui veut traiter lui-même certaines actions.
/// Renvoyer false laisse le comportement par défaut s'appliquer (déplacement du focus, validation, retour).
/// </summary>
public interface IInputHandler
{
    bool HandleAction(InputActionEvent e);
}

/// <summary>Implémenté par l'élément d'une liste (son DataContext) que « Confirmer » doit ouvrir ou lancer.</summary>
public interface IActivatable
{
    void Activate();
}
