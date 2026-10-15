using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WinSU.App.Controls;
using WinSU.App.Input;
using WinSU.App.ViewModels;

namespace WinSU.App.Views.Common;

/// <summary>
/// Réglages en tableau déroulant (étape 32, D-128), la même vue pour les trois styles. À l'ouverture de la page : la
/// section restée ouverte garde le focus, sur le réglage d'où l'on était parti (retour d'un écran ouvert depuis elle,
/// « Boutons et touches », l'éditeur de thèmes : DF-3), sinon le titre de la première section ; après un changement de style,
/// les cartes du style.
/// </summary>
public partial class SettingsView : UserControl
{
    private readonly Dictionary<SettingsSection, FoldSection> _sections;
    private SettingsViewModel? _viewModel;

    public SettingsView()
    {
        InitializeComponent();
        _sections = Sections.Children.OfType<FoldSection>().ToDictionary(section => (SettingsSection)section.Key!);

        // Retour a refermé une section : le focus va sur son titre
        FocusRequests.Follow(this, context => (context as SettingsViewModel)?.Fold, key => FoldSection.HeaderIn(Sections, key));

        // Menu sur un thème enregistré ouvre Modifier / Supprimer (M14). On choisit par Confirmer (D-78) : la carte qui a
        // le focus n'est pas forcément la carte choisie, la vue la donne.
        WatchFocusedCard(ThemeList, option => { if (_viewModel is not null) _viewModel.FocusedTheme = option as ThemeOption; });

        // Le réglage qui a le focus dans la section ouverte est retenu, pour le retour d'un écran ouvert depuis lui (DF-3)
        Sections.GotFocus += (_, _) => RememberContentFocus();

        // « Retirer la clé » disparaît une fois la clé retirée : le focus, qu'il gardait sans se voir, passe à « Saisir la
        // clé » (31.5, vu en essai réel)
        SteamGridDbRemoveKey.PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty && e.NewValue is false)
                Dispatcher.UIThread.Post(() =>
                {
                    if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() == SteamGridDbRemoveKey)
                        FocusNavigator.Focus(SteamGridDbEnterKey);
                }, DispatcherPriority.Loaded);
        };
    }

    /// <summary>Signale la carte de la rangée qui a le focus (son option), ou null quand le focus quitte la rangée.</summary>
    private static void WatchFocusedCard(ListBox list, Action<object?> report)
    {
        void Update() => report(list.IsKeyboardFocusWithin
            && TopLevel.GetTopLevel(list)?.FocusManager?.GetFocusedElement() is Control focused
            && (focused as ListBoxItem ?? focused.FindAncestorOfType<ListBoxItem>()) is { } item
                ? item.DataContext
                : null);

        list.GotFocus += (_, _) => Update();
        list.PropertyChanged += (_, e) =>
        {
            if (e.Property == IsKeyboardFocusWithinProperty)
                Update();
        };
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (_viewModel is null)
            return;

        // Après un changement de style, la page vient d'être reconstruite avec les vues du nouveau style : retour aux cartes
        // du style, section ouverte.
        if (_viewModel.TakeMenuStyleFocus())
        {
            _viewModel.ShowSection(SettingsSection.MenuStyle);
            Dispatcher.UIThread.Post(FocusMenuStyleCard, DispatcherPriority.Loaded);
            return;
        }

        if (_viewModel.OpenSection is { } open)
        {
            var remembered = _viewModel.ContentFocus is { } focus && focus.Section == open ? focus : null;
            Dispatcher.UIThread.Post(() => FocusOpenSection(open, remembered), DispatcherPriority.Loaded);
            return;
        }

        _sections[SettingsSection.MenuStyle].FocusHeader();
        Dispatcher.UIThread.Post(() => Scroller.ScrollToHome(), DispatcherPriority.Background);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            // Indications posées par cette vue : remises à zéro quand elle lâche le ViewModel
            _viewModel.FocusedTheme = null;
        }

        _viewModel = DataContext as SettingsViewModel;
    }

    /// <summary>Retient le réglage qui a le focus dans la section ouverte (DF-3).</summary>
    private void RememberContentFocus()
    {
        if (_viewModel?.OpenSection is not { } open || TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not Control focused)
            return;

        if (_sections[open].ContentIndexOf(focused) is { } index)
            _viewModel.ContentFocus = new SettingsFocus(open, string.IsNullOrEmpty(focused.Name) ? null : focused.Name, index);
    }

    /// <summary>La section ouverte reprend le focus : sur le réglage retenu (par son nom, sinon sa place), sinon le premier.</summary>
    private void FocusOpenSection(SettingsSection open, SettingsFocus? remembered)
    {
        if (remembered?.Name is { } name && this.FindControl<Control>(name) is { IsEffectivelyVisible: true } named && FocusNavigator.Focus(named))
            return;

        if (remembered is not null)
            _sections[open].FocusContent(remembered.Index);
        else
            _sections[open].FocusContent();
    }

    private void FocusMenuStyleCard()
    {
        var selected = MenuStyleList.SelectedIndex >= 0 ? MenuStyleList.ContainerFromIndex(MenuStyleList.SelectedIndex) : null;
        if (selected is null || !FocusNavigator.Focus(selected))
            _sections[SettingsSection.MenuStyle].FocusHeader();
    }
}
