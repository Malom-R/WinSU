using System.ComponentModel;
using Avalonia.Controls;
using WinSU.App.ViewModels.Shell;

namespace WinSU.App.Views.Standard;

/// <summary>
/// Coquille du style Standard 2.0 (étape 30) : la page passe sous la barre d'aide (posée en bas à gauche), les onglets à
/// gauche ; sans onglet Apps. Focus de l'onglet choisi : Views/TabFocus (la vue de l'onglet choisit, files qui glissent).
/// </summary>
public partial class HomeShellView : UserControl, IHintBarPlacement
{
    private HomeShellViewModel? _viewModel;

    public HomeShellView()
    {
        InitializeComponent();
        TabFocus.Follow(this, TabHost);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = DataContext as HomeShellViewModel;
        if (_viewModel is not null)
        {
            _viewModel.TabsAsPages = false; // de vrais onglets (le style Switch en fait des pages)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            PlaceTab();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HomeShellViewModel.SelectedTab))
            PlaceTab();
    }

    /// <summary>
    /// L'accueil (bannière héro, file diagonale) et les Jeux (colonnes à droite) occupent toute la page ; les Réglages sont
    /// dans le grand panneau de verre à droite des onglets (Themes/Standard.axaml).
    /// </summary>
    private void PlaceTab() =>
        PagePanel.Classes.Set("fullPage", _viewModel?.SelectedTab.Content is HomeTabViewModel or GamesTabViewModel);
}
