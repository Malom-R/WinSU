using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinSU.App.Input;
using WinSU.App.Localization;
using WinSU.App.Services;
using WinSU.Core.Audio;
using WinSU.Core.Input;

namespace WinSU.App.ViewModels.Shell;

public sealed partial class ShellTab(string title, ViewModelBase content) : ObservableObject
{
    public string Title { get; } = title;

    public ViewModelBase Content { get; } = content;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>
/// Écran principal : Accueil, Jeux, Réglages (plus d'Apps depuis l'étape 35, D-175). Style Standard : des onglets, changés avec LB/RB (Page préc./suiv.
/// au clavier) ; heure et date en haut à droite. Un style peut les présenter en pages (<see cref="TabsAsPages"/>, M15) :
/// l'accueil ouvre les autres, Retour y ramène.
/// </summary>
public sealed partial class HomeShellViewModel : ViewModelBase, IInputHandler, IHintSource
{
    private readonly IAudioService _audio;
    private readonly PowerMenu _power;
    private bool _active;

    public HomeShellViewModel(
        HomeTabViewModel home,
        GamesTabViewModel games,
        SettingsViewModel settings,
        IAudioService audio,
        PowerMenu power)
    {
        _audio = audio;
        _power = power;
        Home = home;
        Games = games;
        Settings = settings;
        HomeTab = new(Tr.Get("win_tab_home"), home);
        GamesTab = new(Tr.Get("win_tab_games"), games);
        SettingsTab = new(Tr.Get("win_tab_settings"), settings);
        Tabs = [HomeTab, GamesTab, SettingsTab];
        SelectedTab = HomeTab;
        SelectedTab.IsSelected = true;
    }

    public override UiSound? EnterSound => null;

    public override UiSound? ExitSound => null;

    /// <summary>Les onglets, dans l'ordre parcouru par LB/RB.</summary>
    public IReadOnlyList<ShellTab> Tabs { get; }

    public ShellTab HomeTab { get; }

    public ShellTab GamesTab { get; }

    public ShellTab SettingsTab { get; }

    public HomeTabViewModel Home { get; }

    public GamesTabViewModel Games { get; }

    public SettingsViewModel Settings { get; }

    /// <summary>
    /// Onglets présentés en pages (option posée par la vue du style, M15) : LB/RB ne changent plus d'onglet, Retour
    /// ramène à l'accueil, la barre d'aide ne propose plus les onglets.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Hints))]
    public partial bool TabsAsPages { get; set; }

    public bool IsHomeSelected => SelectedTab == HomeTab;

    public override string? PageTitle => IsHomeSelected ? null : SelectedTab.Title;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Hints))]
    public partial ShellTab SelectedTab { get; set; }

    public IReadOnlyList<Hint> Hints
    {
        get
        {
            IReadOnlyList<Hint> hints = SelectedTab.Content is IHintSource source
                ? source.Hints
                : [Hint.Navigate, new(InputAction.Confirm, Tr.Get("win_hint_confirm")), Hint.Tabs];
            if (!TabsAsPages)
                return hints;

            // En pages : pas d'onglets ; Retour ramène à l'accueil (sauf depuis une page qui l'ignore, D-68).
            var pages = hints.Where(h => h != Hint.Tabs).ToList();
            if (!IsHomeSelected && !SelectedTab.Content.IgnoresBack && pages.All(h => h.Action != InputAction.Back))
                pages.Add(new(InputAction.Back, Tr.Get("win_hint_back")));
            return pages;
        }
    }

    public override void OnNavigatedTo()
    {
        _active = true;
        Activate(SelectedTab);
    }

    public override void OnNavigatedFrom()
    {
        _active = false;
        Deactivate(SelectedTab);
    }

    public bool HandleAction(InputActionEvent e)
    {
        if (e.Action is InputAction.PagePrevious or InputAction.PageNext && !TabsAsPages)
        {
            if (!e.IsRepeat)
                SwitchTab(e.Action == InputAction.PageNext ? 1 : -1);
            return true;
        }

        if (SelectedTab.Content is IInputHandler handler && handler.HandleAction(e))
            return true;

        if (TabsAsPages && e.Action == InputAction.Back && !e.IsRepeat && !IsHomeSelected)
        {
            if (SelectedTab.Content.IgnoresBack)
                return true; // on change de page par les icônes (D-68)

            _audio.Play(UiSound.Close);
            SelectedTab = HomeTab;
            return true;
        }

        return false;
    }

    /// <summary>Ouvre un onglet présenté en page (tuile « Jeux », boutons ronds du style Switch).</summary>
    [RelayCommand]
    private void OpenPage(ShellTab tab)
    {
        if (tab == SelectedTab)
            return;

        _audio.Play(UiSound.Open);
        SelectedTab = tab;
    }

    /// <summary>Ouvre les Réglages à une section (bouton rond « Thèmes » : la section Thème).</summary>
    [RelayCommand]
    private void OpenSettingsSection(SettingsSection section)
    {
        Settings.ShowSection(section);
        OpenPage(SettingsTab);
    }

    /// <summary>Le menu d'alimentation du bouton Start (D-18), depuis un bouton.</summary>
    [RelayCommand]
    private Task OpenPowerMenuAsync() => _power.ShowAsync();

    [RelayCommand]
    private void SelectTab(ShellTab tab)
    {
        if (tab == SelectedTab)
            return;

        _audio.Play(UiSound.Navigate);
        SelectedTab = tab;
    }

    private void SwitchTab(int offset)
    {
        var index = (Tabs.IndexOf(SelectedTab) + offset + Tabs.Count) % Tabs.Count;
        SelectTab(Tabs[index]);
    }

    partial void OnSelectedTabChanged(ShellTab oldValue, ShellTab newValue)
    {
        if (oldValue is not null) // null lors de la première affectation, dans le constructeur
        {
            oldValue.IsSelected = false;
            if (_active)
                Deactivate(oldValue);
        }

        newValue.IsSelected = true;
        if (_active)
            Activate(newValue);
        OnPropertyChanged(nameof(IsHomeSelected));
        OnPropertyChanged(nameof(PageTitle));
    }

    private void Activate(ShellTab tab)
    {
        tab.Content.OnNavigatedTo();
        tab.Content.PropertyChanged += OnTabContentPropertyChanged;
    }

    private void Deactivate(ShellTab tab)
    {
        tab.Content.PropertyChanged -= OnTabContentPropertyChanged;
        tab.Content.OnNavigatedFrom();
    }

    private void OnTabContentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IHintSource.Hints))
            OnPropertyChanged(nameof(Hints));
    }
}

file static class ListExtensions
{
    public static int IndexOf<T>(this IReadOnlyList<T> list, T item)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (EqualityComparer<T>.Default.Equals(list[i], item))
                return i;
        }

        return -1;
    }
}
