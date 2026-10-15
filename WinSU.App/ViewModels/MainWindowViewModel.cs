using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinSU.App.Input;
using WinSU.App.Navigation;
using WinSU.App.Overlays;
using WinSU.App.Services;
using WinSU.Core.Input;
using WinSU.Core.Settings;

namespace WinSU.App.ViewModels;

/// <summary>Une touche de la barre d'aide : sa représentation (glyphe de manette et sa couleur, ou nom de touche) et son rôle.</summary>
public sealed record HintItem(string KeyText, bool IsGlyph, GlyphTint Tint, string Label);

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly InputRouter _router;
    private readonly IInputService _input;
    private readonly ISettingsStore _settings;
    private readonly LaunchCoordinator _launcher;
    private INotifyPropertyChanged? _watchedHintSource;

    public MainWindowViewModel(
        INavigationService navigation,
        IOverlayService overlays,
        InputRouter router,
        IInputService input,
        ISettingsStore settings,
        BackdropDirector backdrop,
        LaunchCoordinator launcher)
    {
        Backdrop = backdrop;
        _router = router;
        _input = input;
        _settings = settings;
        _launcher = launcher;
        launcher.RunningChanged += (_, _) => UiThread.Run(() => IsGameRunning = launcher.IsRunning);

        navigation.CurrentChanged += (_, _) => CurrentPage = navigation.Current;
        overlays.CurrentChanged += (_, _) => CurrentOverlay = overlays.Current;
        router.LastDeviceChanged += (_, _) => RefreshHints();
        input.ControllersChanged += (_, _) => Dispatcher.UIThread.Post(RefreshHints);
        MenuStyle = settings.Current.Appearance.Style;
        settings.Changed += (_, e) =>
        {
            // Style du menu (M15) : la page courante est reconstruite avec les vues du nouveau style (mêmes ViewModels).
            if (e.Previous.Appearance.Style != e.Current.Appearance.Style)
                Dispatcher.UIThread.Post(() => ApplyMenuStyle(e.Current.Appearance.Style));

            // Liaisons (M11) ou style des glyphes (M12).
            if (e.Previous.Input != e.Current.Input
                || e.Previous.Appearance.ButtonStyle != e.Current.Appearance.ButtonStyle
                || e.Previous.Appearance.ColoredGlyphs != e.Current.Appearance.ColoredGlyphs)
            {
                Dispatcher.UIThread.Post(RefreshHints);
            }
        };

        CurrentPage = navigation.Current;
    }

    /// <summary>Quand le fond animé avance (lampe à lave, M10).</summary>
    public BackdropDirector Backdrop { get; }

    [ObservableProperty]
    public partial ViewModelBase? CurrentPage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOverlay))]
    public partial OverlayViewModel? CurrentOverlay { get; set; }

    public bool HasOverlay => CurrentOverlay is not null;

    public ObservableCollection<HintItem> Hints { get; } = [];

    /// <summary>
    /// Un jeu est suivi (D-182) : WinSU n'accepte aucune entrée ; l'écran s'assombrit et une fenêtre au milieu dit qu'il
    /// attend et propose « Reprendre WinSU », seul élément qu'on peut toucher (à la souris).
    /// </summary>
    [ObservableProperty]
    public partial bool IsGameRunning { get; set; }

    /// <summary>« Reprendre WinSU » : WinSU cesse de suivre le jeu, qui reste ouvert (<see cref="LaunchCoordinator.StopFollowing"/>).</summary>
    [RelayCommand]
    private void Resume() => _launcher.StopFollowing();

    /// <summary>Style du menu en vigueur (M15) : la fenêtre place la barre d'aide selon lui.</summary>
    [ObservableProperty]
    public partial UiStyle MenuStyle { get; set; }

    private void ApplyMenuStyle(UiStyle style)
    {
        MenuStyle = style;
        var page = CurrentPage;
        CurrentPage = null; // vue détruite, puis recréée par le ViewLocator dans le nouveau style
        CurrentPage = page;
    }

    partial void OnCurrentPageChanged(ViewModelBase? value) => RefreshHints();

    partial void OnCurrentOverlayChanged(OverlayViewModel? value) => RefreshHints();

    private void RefreshHints()
    {
        object? source = (object?)CurrentOverlay ?? CurrentPage;
        WatchHintSource(source as INotifyPropertyChanged);

        // Avant toute entrée : icônes de manette si une manette est branchée, sinon touches du clavier.
        var device = _router.HasReceivedInput ? _router.LastDevice
            : _input.Controllers.Count > 0 ? InputDevice.Controller
            : InputDevice.Keyboard;
        var settings = _settings.Current;
        var glyphs = GlyphStyle.From(settings.Appearance);

        Hints.Clear();
        foreach (var hint in source is IHintSource provider ? provider.Hints : Hint.Default)
        {
            var key = HintKeys.For(hint.Action, device, settings.Input, glyphs, ControllerInfo.MappingOf(_input.Controllers));
            Hints.Add(new HintItem(key.Text, key.IsGlyph, key.Tint, hint.Label));
        }
    }

    /// <summary>Les écrans à onglets changent d'aides sans changer d'écran : on écoute leur propriété Hints.</summary>
    private void WatchHintSource(INotifyPropertyChanged? source)
    {
        if (ReferenceEquals(source, _watchedHintSource))
            return;

        if (_watchedHintSource is not null)
            _watchedHintSource.PropertyChanged -= OnHintSourceChanged;
        _watchedHintSource = source;
        if (source is not null)
            source.PropertyChanged += OnHintSourceChanged;
    }

    private void OnHintSourceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IHintSource.Hints))
            RefreshHints();
    }
}
