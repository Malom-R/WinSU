using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinSU.App.Input;
using WinSU.App.Localization;
using WinSU.App.Navigation;
using WinSU.App.Overlays;
using WinSU.App.Services;
using WinSU.Core.Audio;
using WinSU.Core.Catalog;
using WinSU.Core.Input;
using WinSU.Core.Library;
using WinSU.Core.Playtime;
using Microsoft.Extensions.Logging;

namespace WinSU.App.ViewModels.Shell;

/// <summary>
/// Jeux d'une console (D-7 : un raccourci ou un programme par jeu) : tuiles triées par nom (avec leur image, M6), puis la
/// tuile d'ajout ; en en-tête la tuile de la console, son nom, le nombre de jeux et le temps de jeu total. Confirmer lance ;
/// menu contextuel : celui de <see cref="EntryMenu"/>. Après une reconstruction, la sélection suit le jeu (renommé, joué)
/// ou passe à son voisin s'il a été retiré.
/// </summary>
public sealed partial class ConsoleGamesViewModel : ViewModelBase, IInputHandler, IHintSource, IFocusRequester
{
    private readonly ILibraryService _library;
    private readonly IPlaytimeService _playtime;
    private readonly LaunchCoordinator _launcher;
    private readonly ILaunchFilePicker _picker;
    private readonly IOverlayService _overlays;
    private readonly IImageLoader _images;
    private readonly INavigationService _navigation;
    private readonly EntryCovers _covers;
    private readonly EntryMenu _menu;
    private readonly GameArtCache _art;
    private readonly AddGamesTileViewModel _addTile;
    private readonly ILogger _logger;
    private IReadOnlyDictionary<Guid, PlaytimeSummary> _summaries = new Dictionary<Guid, PlaytimeSummary>();
    private Guid? _focusAfterRebuild;
    private bool _imagesReleased;

    public ConsoleGamesViewModel(
        ILibraryService library,
        IPlaytimeService playtime,
        LaunchCoordinator launcher,
        ILaunchFilePicker picker,
        IOverlayService overlays,
        IImageLoader images,
        EntryImageService entryImages,
        INavigationService navigation,
        ILogger<ConsoleGamesViewModel> logger)
    {
        _library = library;
        _playtime = playtime;
        _launcher = launcher;
        _picker = picker;
        _overlays = overlays;
        _images = images;
        _navigation = navigation;
        _covers = entryImages.CreateCovers();
        _menu = new EntryMenu(library, launcher, overlays, entryImages, _covers) { BeforeRename = id => _focusAfterRebuild = id };
        _art = new GameArtCache(images, () => entryImages.ShowBorders);
        _addTile = new AddGamesTileViewModel(AddFromPicker);
        _logger = logger;
    }

    public override UiSound? EnterSound => UiSound.EnterConsole;

    public override UiSound? ExitSound => UiSound.ExitConsole;

    public ConsoleDefinition? Console { get; private set; }

    public string Title => Console?.Name ?? string.Empty;

    public override string? PageTitle => Console?.Name;

    [ObservableProperty]
    public partial string Subtitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Bitmap? Image { get; set; }

    public ObservableCollection<object> Items { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Hints))]
    public partial object? SelectedItem { get; set; }

    /// <summary>Seule la tuile d'ajout est affichée.</summary>
    public bool IsEmpty => Items.Count <= 1;

    /// <summary>La vue donne le focus à cet élément (jeu ajouté, renommé, voisin d'un jeu retiré).</summary>
    public event EventHandler<object>? FocusRequested;

    public IReadOnlyList<Hint> Hints => SelectedItem is GameTileViewModel
        ? [Hint.Navigate, new(InputAction.Confirm, Tr.Get("win_hint_launch")), new(InputAction.ContextMenu, Tr.Get("win_hint_options")), BackHint]
        : [Hint.Navigate, new(InputAction.Confirm, Tr.Get("win_games_add")), BackHint];

    private static Hint BackHint => new(InputAction.Back, Tr.Get("win_hint_back"));

    public void Show(ConsoleDefinition console) => Console = console;

    /// <summary>Revenir aux consoles : le bouton de l'écran quand Retour est ignoré (<see cref="ViewModelBase.IgnoresBack"/>).</summary>
    [RelayCommand]
    private void Leave() => _navigation.GoBack();

    public override void OnNavigatedTo()
    {
        _library.Changed += OnLibraryChanged;
        _launcher.SessionEnded += OnSessionEnded;
        Rebuild();
        _ = RefreshPlaytimeAsync();
        _ = LoadHeaderImageAsync();
    }

    public override void OnNavigatedFrom()
    {
        _library.Changed -= OnLibraryChanged;
        _launcher.SessionEnded -= OnSessionEnded;
        foreach (var tile in Items.OfType<GameTileViewModel>())
        {
            tile.Cover = null;
            tile.Background = null;
            tile.Border = null;
        }

        _covers.Clear();
        _art.Clear();
        _imagesReleased = true;
        var image = Image;
        Image = null;
        image?.Dispose();
    }

    public bool HandleAction(InputActionEvent e)
    {
        if (e.Action != InputAction.ContextMenu || e.IsRepeat || SelectedItem is not GameTileViewModel tile)
            return false;

        _ = _menu.ShowAsync(tile);
        return true;
    }

    private void Launch(GameTileViewModel tile) => _ = _launcher.LaunchAsync(tile.Game);

    /// <summary>Action de la tuile d'ajout : boîte « Ouvrir » de Windows, sélection multiple.</summary>
    private void AddFromPicker() => _ = AddFromPickerAsync();

    private async Task AddFromPickerAsync()
    {
        if (Console is not { } console)
            return;

        try
        {
            var paths = await _picker.PickAsync(Tr.Get("win_games_pick_title"));
            if (paths.Count == 0)
                return;

            var added = await _library.AddGamesAsync(console.Id, paths);
            _logger.LogInformation("{Count} entrée(s) ajoutée(s)", added.Count);
            if (added.Count == 0)
            {
                await _overlays.AlertAsync(Tr.Get("win_games_already_title"), Tr.Get("win_games_already_message"), Tr.Get("win_ok"));
                return;
            }

            // La notification de la bibliothèque arrive de façon asynchrone : on reconstruit tout de suite
            // pour pouvoir donner le focus au premier jeu ajouté.
            _focusAfterRebuild = added[0].Id;
            Rebuild();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Impossible d'ajouter les fichiers choisis");
        }
    }

    private void OnLibraryChanged(object? sender, EventArgs e) => UiThread.Run(Rebuild);

    private void OnSessionEnded(object? sender, PlaySession session) => _ = RefreshPlaytimeAsync();

    private void Rebuild()
    {
        if (Console is null)
            return;

        var selected = SelectedItem as GameTileViewModel;
        var selectedIndex = selected is null ? -1 : Items.IndexOf(selected);
        var comparer = StringComparer.Create(CultureInfo.CurrentCulture, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace);
        var games = _library.GamesOf(Console.Id).OrderBy(g => g.Name, comparer).ToList();

        // Mêmes jeux, inchangés (la notification de la bibliothèque après un ajout déjà affiché, DF-14) : les tuiles
        // restent, celle qui a le focus aussi ; refaire les tuiles détruirait le focus sans le redemander.
        var unchanged = Items.Count == games.Count + 1
            && Items.OfType<GameTileViewModel>().Select(t => t.Game).SequenceEqual(games);
        if (!unchanged)
        {
            Items.Clear();
            foreach (var game in games)
                Items.Add(new GameTileViewModel(game, Launch));
            Items.Add(_addTile);

            ApplyPlaytime();
            OnPropertyChanged(nameof(IsEmpty));
        }

        // Tuiles neuves, ou gardées alors que leurs images ont été lâchées en quittant l'écran (retour de SteamGridDB, DF-22)
        if (!unchanged || _imagesReleased)
        {
            _imagesReleased = false;
            _ = ApplyArtAsync();
            _ = _covers.ApplyAsync(Items.OfType<GameTileViewModel>());
        }

        var focusId = _focusAfterRebuild ?? selected?.Game.Id;
        var kept = Items.OfType<GameTileViewModel>().FirstOrDefault(t => t.Game.Id == focusId);

        // Jeu sélectionné retiré (menu, ou fenêtre « introuvable » au lancement) : son voisin prend sa place.
        var vanished = kept is null && selectedIndex >= 0;
        var target = kept ?? (vanished ? Items[Math.Min(selectedIndex, Items.Count - 1)] : Items[0]);
        var requested = _focusAfterRebuild is not null || vanished;
        _focusAfterRebuild = null;

        SelectedItem = target;
        if (requested)
            FocusRequested?.Invoke(this, target);
    }

    private async Task RefreshPlaytimeAsync()
    {
        try
        {
            _summaries = await _playtime.GetSummariesAsync();
            ApplyPlaytime();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Temps de jeu indisponible");
        }
    }

    /// <summary>Le temps de jeu de chaque tuile, et l'en-tête : « Aucun jeu », « 1 jeu », « 12 jeux · 3 h 05 ».</summary>
    private void ApplyPlaytime()
    {
        var total = TimeSpan.Zero;
        var tiles = Items.OfType<GameTileViewModel>().ToList();
        foreach (var tile in tiles)
        {
            var summary = _summaries.GetValueOrDefault(tile.Game.Id);
            tile.Subtitle = PlaytimeTexts.Format(summary);
            total += summary?.Total ?? TimeSpan.Zero;
        }

        var games = tiles.Count switch
        {
            0 => Tr.Get("win_games_count_zero"),
            1 => Tr.Format("win_games_count_one", tiles.Count),
            _ => Tr.Format("win_games_count", tiles.Count),
        };
        Subtitle = total > TimeSpan.Zero ? $"{games} · {PlaytimeTexts.Format(total)}" : games;
    }

    private async Task ApplyArtAsync()
    {
        if (Console is null)
            return;

        var (background, border) = await _art.GetAsync(Console);
        foreach (var tile in Items.OfType<GameTileViewModel>())
        {
            tile.Background = background;
            tile.Border = border;
        }
    }

    private async Task LoadHeaderImageAsync()
    {
        if (Image is null && Console is not null)
            Image = await _images.LoadConsoleArtAsync(Console, ConsoleArt.Tile, 360, ConsoleTileImages.IsDarkTheme);
    }
}
