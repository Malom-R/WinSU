using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using WinSU.App.Input;
using WinSU.App.Localization;
using WinSU.App.Overlays;
using WinSU.App.Services;
using WinSU.Core.Catalog;
using WinSU.Core.Input;
using WinSU.Core.Library;
using Microsoft.Extensions.Logging;

namespace WinSU.App.ViewModels.Shell;

/// <summary>
/// « Ajouter une console » : les consoles du catalogue (étape 34 : celui de WinSU), rangées par fabricant (Nintendo, Sega,
/// Sony, Microsoft), puis par année de sortie. Confirmer ajoute la console (on peut en ajouter plusieurs d'affilée) ; sur une
/// console déjà ajoutée, Confirmer la retire, après la confirmation du menu Options (ses jeux partent avec elle). LB/RB
/// sautent d'un fabricant à l'autre.
/// </summary>
public sealed partial class ConsolePickerViewModel(
    IConsoleCatalog catalog,
    ILibraryService library,
    IImageLoader images,
    IOverlayService overlays,
    ILogger<ConsolePickerViewModel> logger) : ViewModelBase, IInputHandler, IHintSource, IFocusRequester
{
    private const int TileDecodeWidth = 192;

    /// <summary>Ordre des fabricants (étape 34, demande de l'utilisateur) ; un autre passerait à la fin.</summary>
    public static readonly IReadOnlyList<string> MakerOrder = ["Nintendo", "Sega", "Sony", "Microsoft"];

    private readonly ConsoleMenu _menu = new(library, overlays, logger);

    private CancellationTokenSource? _loading;

    public ObservableCollection<ConsoleTileViewModel> Tiles { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Hints))]
    public partial ConsoleTileViewModel? SelectedTile { get; set; }

    [ObservableProperty]
    public partial string Summary { get; set; } = string.Empty;

    public bool IsEmpty => catalog.All.Count == 0;

    /// <summary>La vue donne le focus à cette tuile (saut d'initiale).</summary>
    public event EventHandler<object>? FocusRequested;

    /// <summary>Confirmer ajoute, ou retire une console déjà ajoutée ; LB/RB : fabricant précédent, suivant.</summary>
    public IReadOnlyList<Hint> Hints =>
    [
        Hint.Navigate,
        new(InputAction.Confirm, Tr.Get(SelectedTile?.IsAdded == true ? "win_hint_remove" : "win_hint_add")),
        new(InputAction.Back, Tr.Get("win_hint_back")),
        new(InputAction.PagePrevious, Tr.Get("win_hint_makers")),
    ];

    /// <summary>Rang du fabricant dans <see cref="MakerOrder"/> (un inconnu après les autres).</summary>
    public static int MakerRank(string? manufacturer)
    {
        var rank = MakerOrder.ToList().FindIndex(m => string.Equals(m, manufacturer, StringComparison.OrdinalIgnoreCase));
        return rank < 0 ? MakerOrder.Count : rank;
    }

    public override void OnNavigatedTo()
    {
        if (Tiles.Count == 0)
        {
            var comparer = StringComparer.Create(CultureInfo.CurrentCulture, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace);
            var sorted = catalog.All
                .OrderBy(c => MakerRank(c.Manufacturer))
                .ThenBy(c => c.ReleaseYear ?? int.MaxValue)
                .ThenBy(c => c.Name, comparer);
            foreach (var console in sorted)
                Tiles.Add(new ConsoleTileViewModel(console, Activate) { IsAdded = LibraryIdOf(console) is not null });
            SelectedTile = Tiles.FirstOrDefault();
        }

        UpdateSummary();
        _loading = new CancellationTokenSource();
        _ = ConsoleTileImages.LoadAsync(Tiles, images, TileDecodeWidth, _loading.Token);
    }

    public override void OnNavigatedFrom()
    {
        _loading?.Cancel();
        foreach (var tile in Tiles)
            tile.ReleaseImage();
    }

    public bool HandleAction(InputActionEvent e)
    {
        if (e.Action is not (InputAction.PagePrevious or InputAction.PageNext) || Tiles.Count == 0)
            return false;

        var current = SelectedTile is null ? 0 : Tiles.IndexOf(SelectedTile);
        var makers = Tiles.Select(t => MakerRank(t.Console.Manufacturer).ToString(CultureInfo.InvariantCulture)).ToList();
        var target = LetterJump.FindGroup(makers, current, e.Action == InputAction.PageNext ? 1 : -1);
        if (target >= 0 && target != current)
            FocusRequested?.Invoke(this, Tiles[target]);
        return true;
    }

    /// <summary>Confirmer : ajoute la console, ou la retire si elle est déjà dans la bibliothèque.</summary>
    private async void Activate(ConsoleTileViewModel tile)
    {
        if (LibraryIdOf(tile.Console) is { } id)
        {
            if (await _menu.RemoveAsync(id, tile.Name))
            {
                logger.LogInformation("Console retirée depuis le catalogue : {Id}", id);
                tile.IsAdded = false;
                UpdateSummary();
            }

            return;
        }

        try
        {
            if (await library.AddConsoleAsync(tile.Console.Id))
                logger.LogInformation("Console ajoutée : {Id}", tile.Console.Id);
            tile.IsAdded = true;
            UpdateSummary();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Impossible d'ajouter la console {Id}", tile.Console.Id);
        }
    }

    /// <summary>
    /// L'identifiant sous lequel la console est dans la bibliothèque : le sien ou un de ses alias (étape 34 : doublons
    /// régionaux fusionnés ; une bibliothèque qui a « genesis » a déjà la Mega Drive) ; null si elle n'y est pas.
    /// </summary>
    private string? LibraryIdOf(ConsoleDefinition console) =>
        console.Aliases.Prepend(console.Id).FirstOrDefault(library.ContainsConsole);

    /// <summary>Les consoles de la bibliothèque encore au catalogue (une console retirée du catalogue n'est plus montrée).</summary>
    private void UpdateSummary()
    {
        Summary = Tr.Format("win_picker_summary", catalog.All.Count, library.Consoles.Count(c => catalog.Find(c.ConsoleId) is not null));
        OnPropertyChanged(nameof(Hints)); // la tuile choisie vient d'être ajoutée ou retirée
    }
}
