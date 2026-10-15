using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using WinSU.App.Input;
using WinSU.App.Localization;
using WinSU.Core.Library;
using WinSU.Core.Playtime;

namespace WinSU.App.ViewModels.Shell;

/// <summary>
/// Tuile d'un jeu : nom, temps de jeu, image (M6) ; Confirmer lance le jeu. Sans image, la tuile de la console atténuée
/// en fond et le nom du jeu ; le cadre de la console par-dessus (dessiné par WinSU, D-150), si le réglage l'affiche.
/// </summary>
public sealed partial class GameTileViewModel(LibraryGame game, Action<GameTileViewModel> activate) : ObservableObject, IActivatable
{
    public LibraryGame Game { get; } = game;

    public string Name => Game.Name;

    /// <summary>Image choisie, déposée dans <c>media</c> (voir <see cref="Services.EntryCovers"/>).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCover), nameof(HasFillingCover), nameof(HasIconCover))]
    public partial Bitmap? Cover { get; set; }

    /// <summary>L'image est une icône (<c>.ico</c>) : entière et centrée, au lieu de remplir la tuile (D-25).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFillingCover), nameof(HasIconCover))]
    public partial bool CoverIsIcon { get; set; }

    public bool HasCover => Cover is not null;

    /// <summary>Jaquette ou image choisie qui remplit la tuile.</summary>
    public bool HasFillingCover => HasCover && !CoverIsIcon;

    /// <summary>Icône choisie, centrée dans sa boîte avec une zone de respiration.</summary>
    public bool HasIconCover => HasCover && CoverIsIcon;

    /// <summary>Affiche l'image (null : tuile par défaut), en précisant d'abord si c'est une icône pour éviter un affichage intermédiaire.</summary>
    public void ShowCover(Bitmap? cover, bool isIcon)
    {
        CoverIsIcon = isIcon;
        Cover = cover;
    }

    /// <summary>Temps de jeu (grille) ou « console · temps » (accueil).</summary>
    [ObservableProperty]
    public partial string Subtitle { get; set; } = Tr.Get("win_playtime_never");

    [ObservableProperty]
    public partial Bitmap? Background { get; set; }

    [ObservableProperty]
    public partial Bitmap? Border { get; set; }

    public void Activate() => activate(this);
}

/// <summary>Dernière tuile de l'écran d'une console : ajouter des raccourcis ou des programmes.</summary>
public sealed class AddGamesTileViewModel(Action activate) : IActivatable
{
    public void Activate() => activate();
}

/// <summary>Textes de temps de jeu (« Jamais lancé », « 45 min », « 2 h 05 »).</summary>
public static class PlaytimeTexts
{
    public static string Format(PlaytimeSummary? summary) => summary is null ? Tr.Get("win_playtime_never") : Format(summary.Total);

    public static string Format(TimeSpan total)
    {
        var (kind, hours, minutes) = PlaytimeFormatter.Split(total);
        return kind switch
        {
            PlaytimeFormatter.Kind.LessThanAMinute => Tr.Get("win_playtime_less_minute"),
            PlaytimeFormatter.Kind.Minutes => Tr.Format("win_playtime_minutes", minutes),
            _ => Tr.Format("win_playtime_hours", hours, minutes),
        };
    }
}
