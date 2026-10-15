using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using WinSU.App.Input;
using WinSU.App.ViewModels.Shell;

namespace WinSU.App.Views.WiiU;

/// <summary>
/// Écran principal du style Wii U (M16) : les onglets en pages, jusqu'en bas ; sans barre d'aide, sauf sur la page des
/// Réglages, reprise du Standard avec sa barre (D-57, D-63), sous laquelle ils passent (étape 32). On passe d'une page à l'autre par les icônes du bas : Retour
/// ne fait rien sur Accueil et Jeux, et ramène à l'accueil depuis les Réglages (D-68). Plus d'Apps (30.13, D-119).
/// </summary>
public partial class HomeShellView : UserControl, IHintBarPlacement
{
    private HomeShellViewModel? _viewModel;

    public HomeShellView()
    {
        InitializeComponent();
        // Nom de l'élément qui prend le focus (tuile, icône) dans la pastille en haut à droite (D-66) ; rien ailleurs
        // (Réglages). Le focus parti dans une fenêtre superposée ne passe pas ici : la pastille garde son nom.
        GotFocus += (_, e) => ShowName(FocusName.Of(e.Source as Visual));

        // Après le changement de page : le focus que sa vue a placé (élément choisi de la grille, premier réglage), sinon son
        // premier élément, sinon l'icône de la page (accueil sans activité récente ; Views/TabFocus)
        TabFocus.Follow(this, TabHost, () => FocusNavigator.FocusFirstIn(TabHost) || FocusNavigator.Focus(PageIcon()));
    }

    public event EventHandler? HintBarPlacementChanged;

    /// <summary>Toujours jusqu'en bas (les Réglages passent sous la barre d'aide, atténués dessous : étape 32, D-127) ; la barre seulement sur les Réglages.</summary>
    public bool HidesHintBar => !IsSettingsShown;

    private bool IsSettingsShown => _viewModel is { } shell && shell.SelectedTab == shell.SettingsTab;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Release();
        _viewModel = DataContext as HomeShellViewModel;
        if (_viewModel is not null)
        {
            _viewModel.TabsAsPages = true;
            SetIgnoresBack(_viewModel, true);
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        HintBarPlacementChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Release();
        _viewModel = null;
    }

    /// <summary>
    /// Les ViewModels sont communs aux styles : l'indication posée ici est retirée quand la vue les lâche (changement de
    /// style à chaud, écran ouvert par-dessus) ; la vue suivante pose les siennes.
    /// </summary>
    private void Release()
    {
        if (_viewModel is null)
            return;

        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        SetIgnoresBack(_viewModel, false);
    }

    /// <summary>Accueil et Jeux (pas les Réglages) : Retour n'y fait rien.</summary>
    private static void SetIgnoresBack(HomeShellViewModel shell, bool value)
    {
        shell.Home.IgnoresBack = value;
        shell.Games.IgnoresBack = value;
    }

    private void ShowName(string? name)
    {
        NameText.Text = name;
        NamePill.IsVisible = name is not null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(HomeShellViewModel.SelectedTab))
            return;

        HintBarPlacementChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>L'icône de la page affichée.</summary>
    private Button PageIcon() =>
        _viewModel?.SelectedTab is not { } selected ? HomeIcon
        : selected == _viewModel.GamesTab ? GamesIcon
        : selected == _viewModel.SettingsTab ? SettingsIcon
        : HomeIcon;
}
