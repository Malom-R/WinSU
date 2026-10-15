using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using WinSU.App.Controls;
using WinSU.App.Input;
using WinSU.App.ViewModels.Shell;

namespace WinSU.App.Views.Standard;

/// <summary>
/// Onglet « Jeux » du Standard 2.0 (étape 30, D-104) : la vue demande au ViewModel les jeux de la console choisie en direct
/// (<see cref="GamesTabViewModel.ShowsConsoleGames"/>, rendu à faux quand elle le lâche : les autres styles ne les montrent
/// pas), lui dit quand le focus est dans ces jeux, et donne le focus aux éléments qu'il demande.
/// </summary>
public partial class GamesTabView : UserControl, IInitialFocus
{
    private GamesTabViewModel? _viewModel;

    public GamesTabView()
    {
        InitializeComponent();
        ListFocus.KeepOnReset(ConsoleList); // colonne refaite quand la bibliothèque change
        ListFocus.KeepOnReset(GamesList); // jeux refaits (ajout, retrait, nom)
        // Le focus va à l'élément demandé, dans la colonne des consoles ou dans celle des jeux (jeu ajouté, renommé, voisin
        // d'un jeu retiré : l'onglet fait suivre les demandes de ses jeux)
        FocusRequests.Follow(this, item => ConsoleList.ContainerFromItem(item) ?? GamesList.ContainerFromItem(item));
        GotFocus += (_, e) =>
        {
            if (_viewModel is not null && e.Source is Visual source)
                _viewModel.IsGamesFocused = source == GamesList || GamesList.IsVisualAncestorOf(source);
        };
    }

    /// <summary>
    /// À l'ouverture de l'onglet : la console choisie (le point fixe) ; au retour d'un écran ouvert depuis un jeu (image
    /// SteamGridDB…), ce jeu (DF-9).
    /// </summary>
    public bool FocusInitial()
    {
        if (_viewModel?.TakeGameToResume() is { } game && GamesList.ContainerFromItem(game) is { } gameContainer)
            return FocusNavigator.Focus(gameContainer);

        return _viewModel?.SelectedItem is { } chosen && ConsoleList.ContainerFromItem(chosen) is { } container && FocusNavigator.Focus(container);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Release();
        _viewModel = DataContext as GamesTabViewModel;
        if (_viewModel is null)
            return;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.ShowsConsoleGames = true;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Release();
        _viewModel = null;
    }

    private void Release()
    {
        if (_viewModel is null)
            return;

        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.IsGamesFocused = false;
        _viewModel.ShowsConsoleGames = false;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(GamesTabViewModel.ConsoleGames))
            return;

        // Les jeux de la nouvelle console arrivent depuis la droite, de derrière sa colonne (30.14, comme les menus)
        if (_viewModel?.ConsoleGames is not null)
            SlideIn.Play(GamesList, StandardMetrics.GamesSlideDistance, fade: true);
    }
}
