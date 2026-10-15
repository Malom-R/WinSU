using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.Threading;
using WinSU.App.Controls;
using WinSU.App.Input;
using WinSU.App.Overlays;
using WinSU.App.ViewModels;
using WinSU.Core.Input;
using WinSU.Core.Settings;
using WinSU.Infrastructure.Win32;
using Microsoft.Extensions.Logging;

namespace WinSU.App.Views;

public partial class MainWindow : Window
{
    private readonly InputRouter? _router;
    private readonly KeyboardInput? _keyboard;
    private readonly IOverlayService? _overlays;
    private readonly WindowState _preferredState = WindowState.Maximized;
    private WindowState _stateBeforeFullScreen = WindowState.Maximized;
    private readonly ILogger<MainWindow>? _logger;
    private Control? _focusBeforeOverlay;
    private IHintBarPlacement? _placement;
    private double _scale = double.NaN;

    /// <summary>Constructeur du prévisualiseur XAML.</summary>
    public MainWindow() => InitializeComponent();

    public MainWindow(
        MainWindowViewModel viewModel,
        InputRouter router,
        KeyboardInput keyboard,
        FocusNavigator focus,
        IOverlayService overlays,
        ISettingsStore settings,
        ILogger<MainWindow> logger)
        : this()
    {
        _logger = logger;
        DataContext = viewModel;
        // Style du menu (M15) : la classe de la fenêtre place la barre d'aide (Themes/Controls.axaml, Standard.axaml).
        SetStyleClasses(viewModel.MenuStyle);
        // Fenêtre « Jeu en cours » (D-182) : l'écran s'assombrit, puis elle apparaît ; l'inverse au retour
        var gameHold = new VeilFade(GameHold, GameHoldCard);
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.MenuStyle))
                SetStyleClasses(viewModel.MenuStyle);
            else if (e.PropertyName == nameof(MainWindowViewModel.IsGameRunning) && viewModel.IsGameRunning)
                gameHold.Show();
            else if (e.PropertyName == nameof(MainWindowViewModel.IsGameRunning))
                gameHold.Hide();
        };
        // Place de la page et de la barre d'aide réglée dès que la vue est créée, avant son premier affichage (D-74) :
        // réglée après, une image montrait la nouvelle page dans la disposition de l'ancienne.
        PageFade.WatchView(PageHost, (_, _) => PlacePage());
        UiRoot.SizeChanged += (_, e) => ApplyScale(e.NewSize);
        _router = router;
        _keyboard = keyboard;
        _overlays = overlays;
        focus.Attach(this, PageHost, OverlayHost);
        _preferredState = settings.Current.General.StartFullscreen ? WindowState.FullScreen : WindowState.Maximized;
        WindowState = _preferredState;

        // En tunnel : le clavier passe par le même aiguillage que la manette, avant les comportements natifs.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, (_, e) =>
        {
            keyboard.OnPhysicalKey(e.PhysicalKey, down: false);
            keyboard.OnKeyUp(e.Key);
        }, RoutingStrategies.Tunnel);
        Deactivated += (_, _) => keyboard.ReleaseAll();
        OverlayHost.PointerPressed += OnOverlayBackdropPressed;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Activated += OnActivated;
    }

    private void SetStyleClasses(UiStyle style)
    {
        Classes.Set("switch", style == UiStyle.Switch);
        Classes.Set("standard", style == UiStyle.Standard);
    }

    /// <summary>
    /// Échelle de l'interface d'après la place de la fenêtre (<see cref="UiScale"/>, D-73), et classe « portrait » de la
    /// fenêtre quand elle est plus haute que large (les styles réorganisent alors certains écrans).
    /// </summary>
    private void ApplyScale(Size size)
    {
        var scale = UiScale.For(size);
        Classes.Set("portrait", UiScale.IsPortrait(size));
        if (Math.Abs(scale - _scale) < 0.001)
            return;

        _scale = scale;
        Scaler.LayoutTransform = Math.Abs(scale - 1) < 0.001 ? null : new ScaleTransform(scale, scale);
        _logger?.LogInformation("Échelle de l'interface : {Scale:F3} (fenêtre de {Width:F0} × {Height:F0})", scale, size.Width, size.Height);
    }

    /// <summary>
    /// Une page qui passe sous la barre d'aide (<see cref="IHintBarPlacement"/>) occupe aussi sa ligne ; une page du style
    /// Wii U en décide selon ce qu'elle montre, et peut masquer la barre (M16).
    /// </summary>
    private void PlacePage()
    {
        if (_placement is not null)
            _placement.HintBarPlacementChanged -= OnHintBarPlacementChanged;
        _placement = PageHost.Presenter?.Child as IHintBarPlacement;
        if (_placement is not null)
            _placement.HintBarPlacementChanged += OnHintBarPlacementChanged;
        ApplyHintBarPlacement();
    }

    private void OnHintBarPlacementChanged(object? sender, EventArgs e) => ApplyHintBarPlacement();

    private void ApplyHintBarPlacement()
    {
        var extends = _placement?.ExtendsUnderHintBar == true;
        Grid.SetRowSpan(PageHost, extends ? 2 : 1);
        PageHost.Classes.Set("aboveHintBar", !extends); // écart au-dessus de la barre, selon le style (Themes/Switch.axaml)
        HintBar.IsVisible = _placement?.HidesHintBar != true;
    }

    /// <summary>
    /// Restaure la fenêtre et demande le premier plan (seconde instance ; après un jeu, voir MainWindowControl).
    /// Renvoie false si Windows a refusé le premier plan.
    /// </summary>
    public bool BringToFront()
    {
        // Réduite (par WinSU pendant un jeu, ou par l'utilisateur) : Windows la restaure dans son état d'avant, plein écran
        // compris. Changer WindowState pendant qu'elle est réduite la laisse réduite, ou plein écran mais figée.
        if (TryGetPlatformHandle()?.Handle is { } handle && handle != IntPtr.Zero)
            return ForegroundWindow.BringToFront(handle);

        if (WindowState == WindowState.Minimized)
            WindowState = _preferredState;

        Activate();
        return true;
    }

    public void ToggleFullScreen()
    {
        if (WindowState == WindowState.FullScreen)
        {
            WindowState = _stateBeforeFullScreen == WindowState.FullScreen ? WindowState.Maximized : _stateBeforeFullScreen;
        }
        else
        {
            _stateBeforeFullScreen = WindowState;
            WindowState = WindowState.FullScreen;
        }
    }

    /// <summary>
    /// F11 (plein écran, fixe), puis la capture d'une touche (écran « Boutons »), puis les touches des réglages (M11),
    /// qui passent par le même aiguillage que la manette.
    /// </summary>
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        // Pendant un jeu (D-182) : aucune touche, ni F11 ni les comportements natifs (Tab, Espace sur un bouton…)
        if (_router?.IsHeld == true)
        {
            e.Handled = true;
            return;
        }

        _keyboard?.OnPhysicalKey(e.PhysicalKey, down: true); // le clavier dessiné de l'écran « Boutons » (étape 32)
        if (e.Key == Key.F11)
        {
            ToggleFullScreen();
            e.Handled = true;
            return;
        }

        if (_keyboard is null || _router is null)
            return;

        if (_keyboard.OnKeyDown(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers != KeyModifiers.None || FocusManager?.GetFocusedElement() is TextBox)
            return;

        foreach (var action in _keyboard.Map(e.Key))
            e.Handled |= _router.Route(new InputActionEvent(action, IsRepeat: false, InputDevice.Keyboard));
    }

    /// <summary>
    /// À l'ouverture d'une fenêtre superposée, le focus y entre (premier élément dans l'ordre de lecture) ;
    /// à sa fermeture, il revient là où il était.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.CurrentOverlay))
            return;

        if (((MainWindowViewModel)DataContext!).HasOverlay)
        {
            _focusBeforeOverlay ??= FocusManager?.GetFocusedElement() as Control;
            Dispatcher.UIThread.Post(() => FocusNavigator.FocusFirstIn(OverlayHost), DispatcherPriority.Loaded);
            return;
        }

        var previous = _focusBeforeOverlay;
        _focusBeforeOverlay = null;
        Dispatcher.UIThread.Post(() => RestoreFocus(previous), DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Rend le focus à l'élément qui l'avait avant la fenêtre superposée. S'il n'est plus là (caché par ce qu'on vient de
    /// faire, ex. « Retirer la clé » une fois la clé retirée, 31.5), le premier élément de son groupe le plus proche
    /// le prend, plutôt que le haut de la page.
    /// </summary>
    private void RestoreFocus(Control? previous)
    {
        if (previous is not null && GetTopLevel(previous) is not null) // encore dans la fenêtre
        {
            if (previous.IsEffectivelyVisible && FocusNavigator.Focus(previous))
                return;

            for (var group = previous.Parent as Control; group is not null && group != PageHost; group = group.Parent as Control)
            {
                if (group.IsEffectivelyVisible && FocusNavigator.FocusFirstIn(group))
                    return;
            }
        }

        FocusNavigator.FocusFirstIn(PageHost);
    }

    /// <summary>
    /// Au retour dans WinSU (fin d'un jeu, boîte Windows fermée, Alt+Tab), Avalonia rend le focus sans le montrer :
    /// l'élément garde son fond, mais plus le liseré du focus clavier ou manette. On le lui redonne en « directionnel ».
    /// Redonner le focus à l'élément qui l'a déjà ne change rien, et Avalonia 12 n'a pas de quoi l'effacer : il passe
    /// un instant par un relais invisible (hors de toute liste, donc sans changer de sélection).
    /// </summary>
    private void OnActivated(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsActive || FocusManager?.GetFocusedElement() is not Control focused || focused is TextBox || focused == FocusSink)
                return;

            if (FocusSink.Focus())
                focused.Focus(NavigationMethod.Directional);
        }, DispatcherPriority.Input);

    private void OnOverlayBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source == OverlayHost)
            _overlays?.Current?.Cancel();
    }
}
