using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinSU.App.Controls;
using WinSU.App.Input;
using WinSU.App.Localization;
using WinSU.App.Navigation;
using WinSU.App.Overlays;
using WinSU.App.Services;
using WinSU.Core.Input;
using WinSU.Core.Library;
using WinSU.Core.Media;
using Microsoft.Extensions.Logging;

namespace WinSU.App.ViewModels.Shell;

/// <summary>Où en est l'écran de choix SteamGridDB.</summary>
public enum ArtworkPickerStep
{
    /// <summary>Recherche en cours.</summary>
    Busy,

    /// <summary>Plusieurs jeux portent ce nom : l'utilisateur choisit le sien.</summary>
    Games,

    /// <summary>Les vignettes des images du jeu.</summary>
    Images,

    /// <summary>Rien trouvé, ou une erreur : un message, avec Réessayer et Modifier la recherche.</summary>
    Message,
}

/// <summary>
/// « Chercher sur SteamGridDB » (étape 31, D-116) : le nom de l'entrée (sans « (USA) », « [!] »…) est cherché sur le site ;
/// si plusieurs jeux portent ce nom, l'utilisateur choisit le sien ; puis une grille de vignettes (les mieux notées d'abord,
/// carrées d'abord pour une jaquette) ; Confirmer télécharge l'image entière, l'enregistre comme celle de l'entrée et revient
/// à l'écran d'avant. Retour depuis les vignettes revient aux jeux (s'il y en avait plusieurs), sinon à l'écran d'avant.
/// </summary>
public sealed partial class ArtworkPickerViewModel(
    IArtworkSearch search,
    EntryImageService entryImages,
    INavigationService navigation,
    IOverlayService overlays,
    IImageLoader images,
    ILogger<ArtworkPickerViewModel> logger) : ViewModelBase, IInputHandler, IHintSource, IFocusRequester
{
    /// <summary>Vignettes montrées au plus (une page du site en compte jusqu'à 50).</summary>
    public const int MaxImages = 40;

    private const int ThumbnailDecodeWidth = 320;
    private const int ParallelThumbnails = 4;

    private LibraryGame? _entry;
    private Func<string, Task>? _applied;
    private Func<Task>? _retry;
    private CancellationTokenSource? _work;
    private bool _started;

    public ArtKind Kind { get; private set; }

    /// <summary>« Recherche Image » ou « Recherche Bannière héro » (étape 32, demande de l'utilisateur) : le nom du jeu est dans l'état.</summary>
    public string Title => _entry is null ? string.Empty : Tr.Get(Kind switch
    {
        ArtKind.Hero => "win_sgdb_title_hero",
        ArtKind.Logo => "win_sgdb_title_logo",
        _ => "win_sgdb_title_image",
    });

    /// <summary>Le nom cherché ; « Modifier la recherche » le change.</summary>
    [ObservableProperty]
    public partial string Query { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy), nameof(ShowsGames), nameof(ShowsImages), nameof(ShowsList), nameof(CanChangeGame), nameof(CanEditSearch), nameof(DetailText))]
    public partial ArtworkPickerStep Step { get; private set; } = ArtworkPickerStep.Busy;

    [ObservableProperty]
    public partial string StatusText { get; private set; } = string.Empty;

    /// <summary>Après une erreur passagère (hors ligne, trop de demandes) : « Réessayer ».</summary>
    [ObservableProperty]
    public partial bool CanRetry { get; private set; }

    /// <summary>L'image choisie se télécharge : Confirmer et Retour attendent.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditSearch))]
    public partial bool IsDownloading { get; private set; }

    public ObservableCollection<ArtworkGameOptionViewModel> Games { get; } = [];

    public ObservableCollection<ArtworkTileViewModel> Images { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailText))]
    public partial ArtworkTileViewModel? SelectedImage { get; set; }

    /// <summary>L'auteur et la taille de la vignette choisie.</summary>
    public string DetailText => ShowsImages ? SelectedImage?.Detail ?? string.Empty : string.Empty;

    public bool IsBusy => Step == ArtworkPickerStep.Busy;

    public bool ShowsGames => Step == ArtworkPickerStep.Games;

    public bool ShowsImages => Step == ArtworkPickerStep.Images;

    public bool ShowsList => ShowsGames || ShowsImages;

    public bool CanChangeGame => ShowsImages && Games.Count > 1;

    public bool CanEditSearch => !IsBusy && !IsDownloading;

    /// <summary>La vue donne le focus au premier choix de l'étape (jeu, vignette, ou bouton) ; l'élément : l'écran lui-même.</summary>
    public event EventHandler<object>? FocusRequested;

    public IReadOnlyList<Hint> Hints { get; } =
    [
        Hint.Navigate,
        new(InputAction.Confirm, Tr.Get("win_hint_choose")),
        new(InputAction.Back, Tr.Get("win_hint_back")),
    ];

    /// <summary>L'entrée et la sorte d'image ; <paramref name="applied"/> reçoit le chemin de la copie enregistrée.</summary>
    public void Show(LibraryGame entry, ArtKind kind, Func<string, Task> applied)
    {
        _entry = entry;
        Kind = kind;
        _applied = applied;
        Query = SearchName(entry.Name);
        OnPropertyChanged(nameof(Title));
    }

    public override void OnNavigatedTo()
    {
        if (_started || _entry is null)
            return;

        _started = true;
        _ = SearchGamesAsync();
    }

    public override void OnNavigatedFrom()
    {
        _work?.Cancel();
        ReleaseImages();
    }

    public bool HandleAction(InputActionEvent e)
    {
        if (e.Action != InputAction.Back || e.IsRepeat)
            return false;

        if (IsDownloading)
            return true;

        if (CanChangeGame)
        {
            ChangeGame();
            return true;
        }

        return false; // l'écran d'avant
    }

    /// <summary>Le nom à chercher : sans les précisions entre parenthèses ou crochets (« (USA) », « [!] »).</summary>
    public static string SearchName(string name)
    {
        var cleaned = Regex.Replace(Regex.Replace(name, @"\s*[\(\[][^\)\]]*[\)\]]", " "), @"\s+", " ").Trim();
        return cleaned.Length > 0 ? cleaned : name.Trim();
    }

    /// <summary>Même nom, à la casse, aux accents et à la ponctuation près.</summary>
    public static bool SameName(string a, string b) => Key(a) == Key(b);

    private static string Key(string name) =>
        string.Concat(name.Normalize(System.Text.NormalizationForm.FormD).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant));

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task EditSearchAsync()
    {
        if (!CanEditSearch)
            return;

        var text = await overlays.PromptTextAsync(Tr.Get("win_sgdb_edit_search"), Query, Tr.Get("win_ok"), Tr.Get("win_cancel"));
        if (text?.Trim() is not { Length: > 0 } query)
            return;

        Query = query;
        await SearchGamesAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task RetryAsync() => _retry?.Invoke() ?? Task.CompletedTask;

    [RelayCommand]
    private void ChangeGame()
    {
        if (Games.Count <= 1)
            return;

        _work?.Cancel();
        ReleaseImages();
        Step = ArtworkPickerStep.Games;
        StatusText = Tr.Format("win_sgdb_choose_game", Query);
        FocusRequested?.Invoke(this, this);
    }

    private async Task SearchGamesAsync()
    {
        var token = Restart();
        Games.Clear();
        ReleaseImages();
        SetBusy(Tr.Format("win_sgdb_searching", Query));
        try
        {
            var result = await search.FindGamesAsync(Query, token);
            if (token.IsCancellationRequested)
                return;

            if (!result.Succeeded)
            {
                ShowError(result.Error, SearchGamesAsync);
                return;
            }

            var games = result.Value!;
            if (games.Count == 0)
            {
                ShowMessage(Tr.Format("win_sgdb_no_game", Query));
                return;
            }

            foreach (var game in games)
                Games.Add(new ArtworkGameOptionViewModel(game, option => _ = LoadImagesAsync(option.Game)));

            // Un seul jeu, ou un seul qui porte exactement ce nom : ses images tout de suite (D-116)
            var exact = games.Where(g => SameName(g.Name, Query)).ToList();
            if (games.Count == 1 || exact.Count == 1)
            {
                await LoadImagesAsync(games.Count == 1 ? games[0] : exact[0]);
                return;
            }

            Step = ArtworkPickerStep.Games;
            StatusText = Tr.Format("win_sgdb_choose_game", Query);
            FocusRequested?.Invoke(this, this);
        }
        catch (OperationCanceledException)
        {
            // Écran quitté ou nouvelle recherche
        }
    }

    private async Task LoadImagesAsync(ArtworkGame game)
    {
        var token = Restart();
        ReleaseImages();
        SetBusy(Tr.Format("win_sgdb_loading_images", game.Name));
        try
        {
            var result = await search.FindImagesAsync(game.Id, Kind, token);
            if (token.IsCancellationRequested)
                return;

            if (!result.Succeeded)
            {
                ShowError(result.Error, () => LoadImagesAsync(game));
                return;
            }

            if (result.Value!.Count == 0)
            {
                ShowMessage(Tr.Format(Kind switch
                {
                    ArtKind.Hero => "win_sgdb_no_heroes",
                    ArtKind.Logo => "win_sgdb_no_logos",
                    _ => "win_sgdb_no_images",
                }, game.Name));
                return;
            }

            foreach (var image in result.Value.Take(MaxImages))
                Images.Add(new ArtworkTileViewModel(image, Kind, tile => _ = ChooseAsync(tile)));
            SelectedImage = Images[0];
            Step = ArtworkPickerStep.Images;
            StatusText = Tr.Format("win_sgdb_images", Images.Count, game.Name);
            FocusRequested?.Invoke(this, this);
            await LoadThumbnailsAsync([.. Images], token);
        }
        catch (OperationCanceledException)
        {
            // Écran quitté, autre jeu ou nouvelle recherche
        }
    }

    /// <summary>Les vignettes, quatre à la fois ; chacune remplit sa tuile dès qu'elle arrive.</summary>
    private async Task LoadThumbnailsAsync(IReadOnlyList<ArtworkTileViewModel> tiles, CancellationToken token)
    {
        using var gate = new SemaphoreSlim(ParallelThumbnails);
        await Task.WhenAll(tiles.Select(async tile =>
        {
            try
            {
                await gate.WaitAsync(token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                var file = await search.DownloadAsync(tile.Image, thumbnail: true, token);
                if (!file.Succeeded)
                    return;

                try
                {
                    var bitmap = await images.LoadAsync(file.Value, ThumbnailDecodeWidth, token);
                    if (token.IsCancellationRequested)
                        bitmap?.Dispose();
                    else
                        tile.Thumbnail = bitmap;
                }
                finally
                {
                    TryDelete(file.Value!);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                gate.Release();
            }
        }));
    }

    private async Task ChooseAsync(ArtworkTileViewModel tile)
    {
        if (IsDownloading || !ShowsImages || _entry is not { } entry)
            return;

        IsDownloading = true;
        var previousStatus = StatusText;
        StatusText = Tr.Get("win_sgdb_downloading");
        try
        {
            var file = await search.DownloadAsync(tile.Image, thumbnail: false, _work?.Token ?? default);
            if (!file.Succeeded)
            {
                StatusText = previousStatus;
                await overlays.AlertAsync(Tr.Get("win_sgdb_download_failed"), ErrorText(file.Error), Tr.Get("win_ok"));
                return;
            }

            string? saved;
            try
            {
                saved = await entryImages.SaveDownloadedAsync(entry, file.Value!, Kind);
            }
            finally
            {
                TryDelete(file.Value!);
            }

            if (saved is null)
            {
                StatusText = previousStatus;
                return;
            }

            logger.LogInformation("Image SteamGridDB {Id} ({Kind}) appliquée à {Name}", tile.Image.Id, Kind, entry.Name);
            if (_applied is not null)
                await _applied(saved);
            navigation.GoBack();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            IsDownloading = false;
        }
    }

    private void SetBusy(string status)
    {
        CanRetry = false;
        _retry = null;
        Step = ArtworkPickerStep.Busy;
        StatusText = status;
    }

    private void ShowMessage(string message)
    {
        Step = ArtworkPickerStep.Message;
        StatusText = message;
        FocusRequested?.Invoke(this, this);
    }

    private void ShowError(ArtworkError error, Func<Task> retry)
    {
        _retry = error is ArtworkError.Offline or ArtworkError.TooManyRequests or ArtworkError.Failed ? retry : null;
        CanRetry = _retry is not null;
        ShowMessage(ErrorText(error));
    }

    public static string ErrorText(ArtworkError error) => Tr.Get(error switch
    {
        ArtworkError.NoKey or ArtworkError.KeyRefused => "win_sgdb_error_key",
        ArtworkError.NotFound => "win_sgdb_error_not_found",
        ArtworkError.TooManyRequests => "win_sgdb_error_busy",
        ArtworkError.Offline => "win_sgdb_error_offline",
        _ => "win_sgdb_error_failed",
    });

    private CancellationToken Restart()
    {
        _work?.Cancel();
        _work = new CancellationTokenSource();
        return _work.Token;
    }

    private void ReleaseImages()
    {
        foreach (var tile in Images)
            tile.ReleaseImage();
        Images.Clear();
        SelectedImage = null;
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>Un jeu trouvé sur SteamGridDB : son nom, son année et « vérifié ».</summary>
public sealed class ArtworkGameOptionViewModel(ArtworkGame game, Action<ArtworkGameOptionViewModel> choose) : IActivatable
{
    public ArtworkGame Game => game;

    public string Name => game.Name;

    public string Detail => string.Join(" · ", new[]
    {
        game.Year?.ToString(CultureInfo.InvariantCulture),
        game.Verified ? Tr.Get("win_sgdb_verified") : null,
    }.Where(part => part is not null));

    public void Activate() => choose(this);
}

/// <summary>
/// Une vignette : carrée pour une jaquette (comme la tuile), large pour une bannière héro ; pour un logo (étape 33), une
/// case large où le logo se montre entier, sur le verre (fond transparent).
/// </summary>
public sealed partial class ArtworkTileViewModel(ArtworkImage image, ArtKind kind, Action<ArtworkTileViewModel> choose)
    : ObservableObject, IActivatable
{
    /// <summary>Largeur d'une vignette de bannière (format 1920 × 620) ou de logo.</summary>
    public const double HeroTileWidth = 330;

    /// <summary>Hauteur d'une vignette de logo : la place du logo sur l'accueil (320 × 190), à la largeur de la vignette.</summary>
    public const double LogoTileHeight = 196;

    public ArtworkImage Image => image;

    /// <summary>Le logo se montre entier dans sa case (une jaquette ou une bannière la remplit).</summary>
    public bool IsLogo => kind == ArtKind.Logo;

    public double TileWidth => kind == ArtKind.Cover ? TileMetrics.GameSize : HeroTileWidth;

    public double TileHeight => kind switch
    {
        ArtKind.Hero => Math.Round(HeroTileWidth * 620 / 1920),
        ArtKind.Logo => LogoTileHeight,
        _ => TileMetrics.GameSize,
    };

    [ObservableProperty]
    public partial Bitmap? Thumbnail { get; set; }

    public string Detail => image.Author is { Length: > 0 } author
        ? Tr.Format("win_sgdb_by", author, image.Width, image.Height)
        : Tr.Format("win_sgdb_size", image.Width, image.Height);

    public void Activate() => choose(this);

    public void ReleaseImage()
    {
        var thumbnail = Thumbnail;
        Thumbnail = null;
        thumbnail?.Dispose();
    }
}
