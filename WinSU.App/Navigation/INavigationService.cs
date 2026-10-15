using WinSU.App.ViewModels;

namespace WinSU.App.Navigation;

/// <summary>Pile d'écrans pilotée par les ViewModels ; la vue est choisie par <see cref="Views.ViewLocator"/>.</summary>
public interface INavigationService
{
    ViewModelBase? Current { get; }

    bool CanGoBack { get; }

    event EventHandler? CurrentChanged;

    /// <summary>
    /// Ouvre un nouvel écran par-dessus l'écran actuel. <paramref name="configure"/> permet de lui passer
    /// ses paramètres (ex. la console à afficher) avant qu'il ne s'affiche.
    /// </summary>
    void NavigateTo<TViewModel>(Action<TViewModel>? configure = null) where TViewModel : ViewModelBase;

    /// <summary>Vide l'historique et ouvre l'écran demandé (ex. à la fin de l'assistant).</summary>
    void ResetTo<TViewModel>() where TViewModel : ViewModelBase;

    /// <summary>Revient à l'écran précédent. Renvoie false s'il n'y en a pas.</summary>
    bool GoBack();
}
