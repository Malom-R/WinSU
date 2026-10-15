using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WinSU.App.Input;
using WinSU.App.Localization;
using WinSU.App.Navigation;
using WinSU.App.Overlays;
using WinSU.App.Services;
using WinSU.Core.Audio;
using WinSU.Core.Catalog;
using WinSU.Core.Input;
using WinSU.Core.Library;
using Microsoft.Extensions.Logging;

namespace WinSU.App.ViewModels.Shell;

/// <summary>
/// Onglet « Jeux » : grille des consoles ajoutées (D-10), dans l'ordre choisi, suivie de la tuile « Ajouter une console ».
/// Menu contextuel sur une console : ouvrir, déplacer, retirer (avec confirmation).
/// <para>
/// Style Standard 2.0 (étape 30, D-104 ; option <see cref="ShowsConsoleGames"/> posée par la vue) : les jeux de la console
/// choisie sont là en direct (<see cref="ConsoleGames"/>, refait quand la console change) ; Confirmer sur la console y
/// entre (au lieu d'ouvrir son écran), Retour en revient, le menu Options d'un jeu passe par lui.
/// </para>
/// </summary>
public sealed partial class GamesTabViewModel : ViewModelBase, IInputHandler, IHintSource, IFocusRequester
{
    private const int TileDecodeWidth = 240;

    private readonly ILibraryService _library;
    private readonly IConsoleCatalog _catalog;
    private readonly IImageLoader _images;
    private readonly INavigationService _navigation;
    private readonly ILogger<GamesTabViewModel> _logger;
    private readonly AddConsoleTileViewModel _addTile;
    private readonly ConsoleMenu _menu;
    private readonly Func<ConsoleGamesViewModel>? _consoleGamesFactory;
    private CancellationTokenSource? _loading;
    private bool _active;
    private string? _focusAfterRebuild;
    private int? _focusIndexAfterRebuild;
    private Guid? _gameToResume;
    private bool _rebuilding;

    public GamesTabViewModel(
        ILibraryService library,
        IConsoleCatalog catalog,
        IImageLoader images,
        INavigationService navigation,
        IOverlayService overlays,
        ILogger<GamesTabViewModel> logger,
        Func<ConsoleGamesViewModel>? consoleGames = null)
    {
        _consoleGamesFactory = consoleGames;
        _library = library;
        _catalog = catalog;
        _images = images;
        _navigation = navigation;
        _logger = logger;
        _addTile = new AddConsoleTileViewModel(() => navigation.NavigateTo<ConsolePickerViewModel>());
        _menu = new ConsoleMenu(library, overlays, logger)
        {
            BeforeMove = id => _focusAfterRebuild = id,
            BeforeRemove = index => _focusIndexAfterRebuild = index, // la voisine prend sa place
        };
    }

    public override UiSound? EnterSound => null;

    public ObservableCollection<object> Items { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Hints))]
    public partial object? SelectedItem { get; set; }

    /// <summary>La vue donne le focus à cet élément (après un déplacement ou un retrait ; un jeu montré en direct aussi).</summary>
    public event EventHandler<object>? FocusRequested;

    /// <summary>
    /// Les jeux de la console choisie sont montrés en direct à côté d'elle (option posée par la vue : Standard 2.0, D-104).
    /// </summary>
    [ObservableProperty]
    public partial bool ShowsConsoleGames { get; set; }

    /// <summary>Les jeux de la console choisie (avec <see cref="ShowsConsoleGames"/>) ; null sur « Ajouter une console ».</summary>
    [ObservableProperty]
    public partial ConsoleGamesViewModel? ConsoleGames { get; private set; }

    /// <summary>Le focus est dans les jeux de la console (fixé par la vue) : aide, Options et Retour sont les leurs.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Hints))]
    public partial bool IsGamesFocused { get; set; }

    public IReadOnlyList<Hint> Hints => IsGamesFocused && ConsoleGames is { } games
        ? games.Hints
        : SelectedItem is ConsoleTileViewModel
            ? [Hint.Navigate, new(InputAction.Confirm, Tr.Get("win_hint_open")), new(InputAction.ContextMenu, Tr.Get("win_hint_options")), Hint.Tabs]
            : [Hint.Navigate, new(InputAction.Confirm, Tr.Get("win_add_console")), Hint.Tabs];

    public override void OnNavigatedTo()
    {
        _active = true;
        _library.Changed += OnLibraryChanged;
        Rebuild();
    }

    public override void OnNavigatedFrom()
    {
        _active = false;
        _library.Changed -= OnLibraryChanged;
        _loading?.Cancel();
        foreach (var tile in Items.OfType<ConsoleTileViewModel>())
            tile.ReleaseImage();
        ReleaseConsoleGames();
    }

    public bool HandleAction(InputActionEvent e)
    {
        if (IsGamesFocused && ConsoleGames is { } games)
        {
            // Retour : de la colonne des jeux à leur console
            if (e.Action == InputAction.Back && !e.IsRepeat && SelectedItem is { } console)
            {
                FocusRequested?.Invoke(this, console);
                return true;
            }

            return games.HandleAction(e);
        }

        if (e.Action != InputAction.ContextMenu || e.IsRepeat || SelectedItem is not ConsoleTileViewModel tile)
            return false;

        _ = _menu.ShowAsync(tile, [.. Items.OfType<ConsoleTileViewModel>()], OpenConsole);
        return true;
    }

    /// <summary>Ouvrir une console : entrer dans ses jeux montrés en direct (D-104), sinon ouvrir son écran.</summary>
    private void OpenConsole(ConsoleTileViewModel tile)
    {
        if (ConsoleGames is { } games && games.Console?.Id == tile.Console.Id)
        {
            if ((games.SelectedItem ?? games.Items.FirstOrDefault()) is { } entry)
                FocusRequested?.Invoke(this, entry);
            return;
        }

        _navigation.NavigateTo<ConsoleGamesViewModel>(screen => screen.Show(tile.Console));
    }

    // Pendant la reconstruction de la colonne, la console choisie disparaît un instant : les jeux ne sont pas lâchés pour
    // autant (ils le sont à la fin, si la console choisie a changé ; DF-14, étape 35)
    partial void OnSelectedItemChanged(object? value)
    {
        if (!_rebuilding)
            UpdateConsoleGames();
    }

    partial void OnShowsConsoleGamesChanged(bool value) => UpdateConsoleGames();

    /// <summary>Les jeux suivent la console choisie (rien sur « Ajouter une console », ni sans l'option).</summary>
    private void UpdateConsoleGames()
    {
        if (!_active || !ShowsConsoleGames || _consoleGamesFactory is null || SelectedItem is not ConsoleTileViewModel tile)
        {
            ReleaseConsoleGames();
            return;
        }

        if (ConsoleGames?.Console?.Id == tile.Console.Id)
            return;

        ReleaseConsoleGames();
        var games = _consoleGamesFactory();
        games.Show(tile.Console);
        games.OnNavigatedTo();
        games.PropertyChanged += OnConsoleGamesPropertyChanged;
        games.FocusRequested += OnConsoleGamesFocusRequested;
        ConsoleGames = games;
    }

    /// <summary>
    /// Le jeu qui avait le focus quand ses jeux ont été lâchés (écran SteamGridDB ouvert depuis ce jeu, autre onglet), s'il
    /// est encore là : choisi dans les jeux pour que la vue lui rende le focus au retour (DF-9, étape 35). Une seule fois.
    /// </summary>
    public GameTileViewModel? TakeGameToResume()
    {
        var id = _gameToResume;
        _gameToResume = null;
        if (id is null || ConsoleGames is not { } games || games.Items.OfType<GameTileViewModel>().FirstOrDefault(t => t.Game.Id == id) is not { } tile)
            return null;

        games.SelectedItem = tile;
        return tile;
    }

    private void ReleaseConsoleGames()
    {
        if (ConsoleGames is not { } games)
            return;

        _gameToResume = IsGamesFocused ? (games.SelectedItem as GameTileViewModel)?.Game.Id : null;
        games.PropertyChanged -= OnConsoleGamesPropertyChanged;
        games.FocusRequested -= OnConsoleGamesFocusRequested;
        games.OnNavigatedFrom();
        ConsoleGames = null;
        IsGamesFocused = false;
    }

    private void OnConsoleGamesPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IHintSource.Hints) && IsGamesFocused)
            OnPropertyChanged(nameof(Hints));
    }

    /// <summary>Les jeux montrés en direct demandent le focus (jeu ajouté, renommé, voisin d'un jeu retiré) : par l'onglet, une seule source pour sa vue.</summary>
    private void OnConsoleGamesFocusRequested(object? sender, object item) => FocusRequested?.Invoke(this, item);

    private void OnLibraryChanged(object? sender, EventArgs e) => UiThread.Run(Rebuild);

    private void Rebuild()
    {
        var selectedId = (SelectedItem as ConsoleTileViewModel)?.Console.Id;
        var addSelected = SelectedItem == _addTile; // retour d'« Ajouter une console » : le focus revient sur « + »
        _loading?.Cancel();
        foreach (var old in Items.OfType<ConsoleTileViewModel>())
            old.ReleaseImage();
        _rebuilding = true;
        Items.Clear();

        foreach (var entry in _library.Consoles)
        {
            if (_catalog.Find(entry.ConsoleId) is { } console)
                Items.Add(new ConsoleTileViewModel(console, OpenConsole));
            else
                _logger.LogWarning("Console « {Id} » de la bibliothèque absente du catalogue : ignorée", entry.ConsoleId);
        }

        Items.Add(_addTile);

        var focusId = _focusAfterRebuild ?? selectedId;
        var target = _focusIndexAfterRebuild is { } index
            ? Items[Math.Min(index, Items.Count - 1)]
            : Items.OfType<ConsoleTileViewModel>().FirstOrDefault(t => t.Console.Id == focusId) ?? (addSelected ? _addTile : Items[0]);
        var requested = _focusAfterRebuild is not null || _focusIndexAfterRebuild is not null;
        _focusAfterRebuild = null;
        _focusIndexAfterRebuild = null;

        SelectedItem = target;
        _rebuilding = false;
        UpdateConsoleGames(); // les mêmes jeux si la console choisie est la même : ils gardent leur sélection et leurs demandes de focus
        if (requested)
            FocusRequested?.Invoke(this, target);

        _loading = new CancellationTokenSource();
        _ = ConsoleTileImages.LoadAsync(Items.OfType<ConsoleTileViewModel>(), _images, TileDecodeWidth, _loading.Token);
    }
}
