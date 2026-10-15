namespace WinSU.App.ViewModels;

/// <summary>
/// ViewModel qui demande à sa vue de donner le focus à un élément : liste reconstruite après une action (ajout, déplacement,
/// renommage, retrait : l'élément concerné ou sa voisine, l'ancien n'étant plus à l'écran), changement d'étape, section
/// refermée. La vue le suit par Views/FocusRequests (étape 36 : tous les écrans qui demandent le focus passent par ici).
/// </summary>
public interface IFocusRequester
{
    event EventHandler<object>? FocusRequested;
}
