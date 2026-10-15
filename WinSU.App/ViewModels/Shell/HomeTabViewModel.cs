using System.Collections.ObjectModel;
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
/// Onglet « Accueil » : l'activité récente (derniers jeux et applications lancés, relançables) et vos consoles en bandeau ;
/// la bannière héro de l'entrée choisie (M9), que la vue place dans sa zone <c>banner_top</c>, et son logo (étape 33,
/// D-140), qu'une vue montre ou non (<see cref="Controls.GameLogo"/>). Options (touche Menu) : le menu des grilles sur une
/// entrée récente, celui de l'onglet Jeux sur une console (D-70).
/// </summary>
public sealed partial class HomeTabViewModel : ViewModelBase, IInputHandler, IHintSource, IFocusRequester
{
    private const int TileDecodeWidth = 200;
    private const int RecentCount = 10;

    /// <summary>Pendant une navigation rapide dans la rangée, seuls la bannière et le logo de la dernière entrée choisie sont chargés.</summary>
    private static readonly TimeSpan ArtDelay = TimeSpan.FromMilliseconds(150);

    private readonly ILibraryService _library;
    private readonly IConsoleCatalog _catalog;
    private readonly IImageLoader _images;
    private readonly INavigationService _navigation;
    private readonly IPlaytimeService _playtime;
    private readonly LaunchCoordinator _launcher;
    private readonly IOverlayService _overlays;
    private readonly ILogger<HomeTabViewModel> _logger;
    private readonly GameArtCache _art;
    private readonly EntryCovers _covers;
    private readonly ArtImages _heroes;
    private readonly ArtImages _logos;
    private readonly EntryMenu _entryMenu;
    private readonly ConsoleMenu _consoleMenu;
    private CancellationTokenSource? _loading;
    private CancellationTokenSource? _artUpdate;

    /// <summary>
    /// Action d'un menu Options en cours (renommer, retirer, déplacer) : la rangée reconstruite demande le focus pour son
    /// élément choisi, l'ancienne tuile n'étant plus là quand le menu rend le focus.
    /// </summary>
    private bool _focusRecentAfterRefresh;
    private bool _focusConsoleAfterRebuild;

    public HomeTabViewModel(
        ILibraryService library,
        IConsoleCatalog catalog,
        IImageLoader images,
        INavigationService navigation,
        IPlaytimeService playtime,
        LaunchCoordinator launcher,
        EntryImageService entryImages,
        IOverlayService overlays,
        ILogger<HomeTabViewModel> logger)
    {
        _library = library;
        _catalog = catalog;
        _images = images;
        _navigation = navigation;
        _playtime = playtime;
        _launcher = launcher;
        _overlays = overlays;
        _logger = logger;
        _art = new GameArtCache(images, () => entryImages.ShowBorders);
        _covers = entryImages.CreateCovers();
        _heroes = entryImages.CreateHeroes();
        _logos = entryImages.CreateLogos();
        _entryMenu = new EntryMenu(library, launcher, overlays, entryImages, _covers)
        {
            BeforeRename = _ => _focusRecentAfterRefresh = true,
            BeforeRemove = _ => _focusRecentAfterRefresh = true,
            ArtChanged = _ => UpdateArt(),
        };
        _consoleMenu = new ConsoleMenu(library, overlays, logger)
        {
            BeforeMove = _ => _focusConsoleAfterRebuild = true,
            BeforeRemove = _ => _focusConsoleAfterRebuild = true,
        };
    }

    public override UiSound? EnterSound => null;

    public ObservableCollection<ConsoleTileViewModel> Consoles { get; } = [];

    public event EventHandler<object>? FocusRequested;

    /// <summary>Jeux et applications, du dernier lancé au plus ancien.</summary>
    public ObservableCollection<GameTileViewModel> Recent { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Hints))]
    public partial GameTileViewModel? SelectedRecent { get; set; }

    /// <summary>Le focus est dans l'activité récente (fixé par la vue) : Confirmer y lance, ailleurs il ouvre.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Hints))]
    public partial bool IsRecentFocused { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Hints))]
    public partial ConsoleTileViewModel? SelectedConsole { get; set; }

    /// <summary>Le focus est dans le bandeau des consoles (fixé par la vue, style Standard) : Options y ouvre leur menu.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Hints))]
    public partial bool IsConsolesFocused { get; set; }

    /// <summary>
    /// Bannière héro de l'entrée choisie dans l'activité récente, tant que cette rangée a le focus ; null sinon, ou si
    /// l'entrée n'en a pas (D-26). Mise à jour 150 ms après le dernier changement.
    /// </summary>
    [ObservableProperty]
    public partial Bitmap? Hero { get; private set; }

    /// <summary>Fichier de la bannière affichée (ou en cours d'affichage), null s'il n'y en a pas.</summary>
    [ObservableProperty]
    public partial string? HeroPath { get; private set; }

    /// <summary>
    /// Logo de l'entrée choisie (étape 33, D-140), en même temps que sa bannière et dans les mêmes cas ; null sinon, ou si
    /// l'entrée n'en a pas. Le style Switch cache alors le nom au-dessus de la tuile (D-141).
    /// </summary>
    [ObservableProperty]
    public partial Bitmap? Logo { get; private set; }

    public bool HasConsoles => Consoles.Count > 0;

    public bool HasNoConsoles => Consoles.Count == 0;

    public bool HasRecent => Recent.Count > 0;

    public bool HasNoRecent => Recent.Count == 0;

    public IReadOnlyList<Hint> Hints =>
    [
        Hint.Navigate,
        new(InputAction.Confirm, Tr.Get(IsRecentFocused ? "win_hint_launch" : "win_hint_open")),
        .. HasOptions ? [new Hint(InputAction.ContextMenu, Tr.Get("win_hint_options"))] : Array.Empty<Hint>(),
        new(InputAction.Start, Tr.Get("win_hint_power")),
        Hint.Tabs,
    ];

    private bool HasOptions => (IsRecentFocused && SelectedRecent is not null) || (IsConsolesFocused && SelectedConsole is not null);

    public bool HandleAction(InputActionEvent e)
    {
        if (e.Action != InputAction.ContextMenu || e.IsRepeat)
            return false;

        if (IsRecentFocused && SelectedRecent is { } entry)
            _ = _entryMenu.ShowAsync(entry);
        else if (IsConsolesFocused && SelectedConsole is { } console)
            _ = _consoleMenu.ShowAsync(console, Consoles, OpenConsole);
        else
            return false;

        return true;
    }

    public override void OnNavigatedTo()
    {
        _library.Changed += OnLibraryChanged;
        _launcher.SessionEnded += OnSessionEnded;
        Rebuild();
    }

    public override void OnNavigatedFrom()
    {
        _library.Changed -= OnLibraryChanged;
        _launcher.SessionEnded -= OnSessionEnded;
        _loading?.Cancel();
        _artUpdate?.Cancel();
        foreach (var tile in Consoles)
            tile.ReleaseImage();
        Recent.Clear();
        Hero = null;
        HeroPath = null;
        Logo = null;
        _art.Clear();
        _covers.Clear();
        _heroes.Clear();
        _logos.Clear();
    }

    partial void OnSelectedRecentChanged(GameTileViewModel? value) => UpdateArt();

    partial void OnIsRecentFocusedChanged(bool value)
    {
        // Focus parti dans une fenêtre superposée (menu Options) : la bannière reste, le focus reviendra ici.
        if (!value && _overlays.Current is not null)
            return;

        UpdateArt();
    }

    private void UpdateArt()
    {
        _artUpdate?.Cancel();
        _artUpdate = new CancellationTokenSource();
        _ = UpdateArtAsync(IsRecentFocused ? SelectedRecent?.Game : null, _artUpdate.Token);
    }

    private async Task UpdateArtAsync(LibraryGame? entry, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(ArtDelay, cancellationToken);
            var (heroFile, logoFile) = entry is null ? (null, null) : (await _heroes.FindAsync(entry), await _logos.FindAsync(entry));
            var hero = heroFile is null ? null : await _heroes.LoadAsync(heroFile);
            var logo = logoFile is null ? null : await _logos.LoadAsync(logoFile);
            cancellationToken.ThrowIfCancellationRequested();
            HeroPath = heroFile?.Path;
            Hero = hero;
            Logo = logo;
        }
        catch (OperationCanceledException)
        {
            // une autre entrée a été choisie entre-temps
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bannière héro ou logo indisponible");
        }
    }

    [RelayCommand]
    private void AddConsole() => _navigation.NavigateTo<ConsolePickerViewModel>();

    private void OpenConsole(ConsoleTileViewModel tile) =>
        _navigation.NavigateTo<ConsoleGamesViewModel>(games => games.Show(tile.Console));

    private void OnLibraryChanged(object? sender, EventArgs e) => UiThread.Run(Rebuild);

    private void OnSessionEnded(object? sender, PlaySession session) => _ = RefreshRecentAsync();

    /// <summary>
    /// La console choisie reste choisie (déplacée) ; retirée, sa voisine prend sa place.
    /// </summary>
    private void Rebuild()
    {
        _loading?.Cancel();
        var selectedId = SelectedConsole?.Console.Id;
        var selectedIndex = SelectedConsole is { } selected ? Consoles.IndexOf(selected) : -1;
        foreach (var old in Consoles)
            old.ReleaseImage();
        Consoles.Clear();

        foreach (var entry in _library.Consoles)
        {
            if (_catalog.Find(entry.ConsoleId) is { } console)
                Consoles.Add(new ConsoleTileViewModel(console, OpenConsole));
        }

        SelectedConsole = Consoles.FirstOrDefault(t => t.Console.Id == selectedId)
            ?? (selectedIndex >= 0 && Consoles.Count > 0 ? Consoles[Math.Min(selectedIndex, Consoles.Count - 1)] : null);
        if (_focusConsoleAfterRebuild && SelectedConsole is { } chosen)
        {
            _focusConsoleAfterRebuild = false;
            FocusRequested?.Invoke(this, chosen);
        }

        OnPropertyChanged(nameof(HasConsoles));
        OnPropertyChanged(nameof(HasNoConsoles));
        _loading = new CancellationTokenSource();
        _ = ConsoleTileImages.LoadAsync(Consoles, _images, TileDecodeWidth, _loading.Token);
        _ = RefreshRecentAsync();
    }

    /// <summary>
    /// Derniers jeux et applications lancés, du plus récent au plus ancien (les entrées retirées de la bibliothèque sont ignorées).
    /// La sélection suit l'entrée (relancée, elle passe en tête) ; une entrée retirée laisse la place à sa voisine.
    /// </summary>
    private async Task RefreshRecentAsync()
    {
        try
        {
            var recent = await _playtime.GetRecentAsync(RecentCount * 2);
            var selectedId = SelectedRecent?.Game.Id;
            var selectedIndex = SelectedRecent is { } selected ? Recent.IndexOf(selected) : -1;
            Recent.Clear();
            foreach (var summary in recent)
            {
                if (CreateRecentTile(summary) is not { } tile)
                    continue;

                Recent.Add(tile);
                if (Recent.Count == RecentCount)
                    break;
            }

            SelectedRecent = Recent.FirstOrDefault(t => t.Game.Id == selectedId)
                ?? (selectedIndex >= 0 && Recent.Count > 0 ? Recent[Math.Min(selectedIndex, Recent.Count - 1)] : null);
            if (_focusRecentAfterRefresh && SelectedRecent is { } chosenRecent)
            {
                _focusRecentAfterRefresh = false;
                FocusRequested?.Invoke(this, chosenRecent);
            }
            _ = _covers.ApplyAsync(Recent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Activité récente indisponible");
        }

        OnPropertyChanged(nameof(HasRecent));
        OnPropertyChanged(nameof(HasNoRecent));
    }

    /// <summary>
    /// Jeu : fond et cadre de sa console, « console · temps ». L'image du jeu (M6) s'y ajoute ensuite.
    /// </summary>
    private GameTileViewModel? CreateRecentTile(PlaytimeSummary summary)
    {
        if (_library.FindGame(summary.EntryId) is not { } game || _catalog.Find(game.ConsoleId) is not { } console)
            return null;

        var tile = new GameTileViewModel(game, Launch) { Subtitle = $"{console.Name} · {PlaytimeTexts.Format(summary)}" };
        _ = ApplyArtAsync(tile, console);
        return tile;
    }

    private void Launch(GameTileViewModel tile) => _ = _launcher.LaunchAsync(tile.Game);

    private async Task ApplyArtAsync(GameTileViewModel tile, ConsoleDefinition console)
    {
        var (background, border) = await _art.GetAsync(console);
        tile.Background = background;
        tile.Border = border;
    }
}
