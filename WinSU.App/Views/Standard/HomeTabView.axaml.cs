using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WinSU.App.Input;
using WinSU.App.ViewModels;
using WinSU.App.ViewModels.Shell;

namespace WinSU.App.Views.Standard;

public partial class HomeTabView : UserControl, IInitialFocus
{
    private HomeTabViewModel? _viewModel;

    public HomeTabView()
    {
        InitializeComponent();
        ListFocus.KeepOnReset(RecentList); // liste recréée à la fin d'une session relancée d'ici
        FocusRequests.Follow(this, RecentList); // après une action du menu Options (renommer, retirer, déplacer)

        // Standard 2.0 (étape 30) : l'accueil n'a plus que la file des récents (les consoles sont dans Jeux) ; la bannière
        // héro et l'aide « Lancer » suivent le focus dans la file.
        GotFocus += (_, e) =>
        {
            if (DataContext is not HomeTabViewModel home || e.Source is not Visual source)
                return;

            home.IsRecentFocused = source == RecentList || RecentList.IsVisualAncestorOf(source);
            home.IsConsolesFocused = false;
        };
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = DataContext as HomeTabViewModel;
        if (_viewModel is not null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = null;
    }

    /// <summary>
    /// Les récents arrivent après coup (requête à la base des temps de jeu) : à l'ouverture de l'onglet, il n'y avait encore
    /// rien à quoi donner le focus, et la file restait sans tuile choisie (retour de l'utilisateur, 30.5 : ni équerres, ni
    /// balayage sur sa bibliothèque). Dès qu'ils sont là, si le focus n'est nulle part dans l'écran, la tuile choisie le prend.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(HomeTabViewModel.HasRecent) || _viewModel is not { HasRecent: true } home)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            if (TopLevel.GetTopLevel(this) is not { } top || !HasNoFocus(top.FocusManager?.GetFocusedElement()))
                return;

            if (home.SelectedRecent is { } chosen && RecentList.ContainerFromItem(chosen) is { } container)
                FocusNavigator.Focus(container);
        }, DispatcherPriority.Loaded);
    }

    /// <summary>À l'ouverture de l'onglet : la tuile choisie de la file (le point fixe), si les récents sont déjà là.</summary>
    public bool FocusInitial() =>
        _viewModel?.SelectedRecent is { } chosen && RecentList.ContainerFromItem(chosen) is { } container && FocusNavigator.Focus(container);

    /// <summary>Personne n'a le focus : rien, la fenêtre elle-même, ou un élément qui n'est plus affiché.</summary>
    private static bool HasNoFocus(IInputElement? focused) =>
        focused is null or TopLevel || (focused is Visual visual && TopLevel.GetTopLevel(visual) is null);
}
