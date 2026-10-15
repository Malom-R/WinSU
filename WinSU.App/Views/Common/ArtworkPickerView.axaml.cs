using Avalonia.Controls;
using Avalonia.Interactivity;
using WinSU.App.Input;
using WinSU.App.ViewModels.Shell;

namespace WinSU.App.Views.Common;

/// <summary>
/// Écran de choix SteamGridDB (étape 31) : à chaque étape, le focus va au premier choix (jeu, vignette choisie), sinon à
/// « Réessayer » ou « Modifier la recherche ». Commun aux trois styles ; passe sous la barre d'aide du Standard et du Wii U
/// (étape 32).
/// </summary>
public partial class ArtworkPickerView : UserControl, IHintBarPlacement
{
    public ArtworkPickerView()
    {
        InitializeComponent();
        FocusRequests.Follow(this, _ => StepFocus());
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (StepFocus() is { } target)
            FocusNavigator.Focus(target);
    }

    /// <summary>
    /// Le premier choix de l'étape : la vignette choisie, ou le jeu ouvert en dernier (Retour depuis ses images) ; sinon
    /// « Réessayer », sinon « Modifier la recherche ».
    /// </summary>
    private Control? StepFocus()
    {
        if (DataContext is not ArtworkPickerViewModel step)
            return null;

        var choice = step.ShowsImages ? ListFocus.SelectedContainer(ImageList)
            : step.ShowsGames ? ListFocus.SelectedContainer(GameList)
            : null;
        return choice ?? (step.CanRetry ? RetryButton : step.CanEditSearch ? EditButton : null);
    }
}
