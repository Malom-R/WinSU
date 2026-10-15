using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WinSU.App.Input;

namespace WinSU.App.Controls;

/// <summary>
/// Une section d'un « tableau déroulant » (étape 32, maquette Sprite-0005, D-128) : une capsule à son titre ; ouverte, la
/// capsule s'agrandit et montre son contenu dessous. Une seule section ouverte à la fois : c'est l'écran qui le sait
/// (<c>ViewModels/FoldState</c>) et qui le dit à ses sections par deux propriétés héritées, posées une fois sur la liste :
/// <see cref="OpenKeyProperty"/> (la clé de la section ouverte) et <see cref="ToggleCommandProperty"/> (Confirmer ou un clic
/// sur un titre l'exécute avec la clé de sa section). Sert aux Réglages et à l'éditeur de thèmes, dans les trois styles.
/// Gabarit : <c>Themes/ControlThemes.axaml</c> ; titre au style des commandes (<c>Button.menu.foldHeader</c>).
/// </summary>
public sealed class FoldSection : HeaderedContentControl
{
    public static readonly StyledProperty<object?> KeyProperty =
        AvaloniaProperty.Register<FoldSection, object?>(nameof(Key));

    /// <summary>Clé de la section ouverte (null : toutes fermées) ; héritée de la liste.</summary>
    public static readonly AttachedProperty<object?> OpenKeyProperty =
        AvaloniaProperty.RegisterAttached<FoldSection, Control, object?>("OpenKey", inherits: true);

    /// <summary>Commande qui ouvre ou ferme une section (paramètre : sa clé) ; héritée de la liste.</summary>
    public static readonly AttachedProperty<ICommand?> ToggleCommandProperty =
        AvaloniaProperty.RegisterAttached<FoldSection, Control, ICommand?>("ToggleCommand", inherits: true);

    public static readonly DirectProperty<FoldSection, bool> IsOpenProperty =
        AvaloniaProperty.RegisterDirect<FoldSection, bool>(nameof(IsOpen), section => section.IsOpen);

    private Button? _header;
    private Control? _body;
    private bool _isOpen;

    public object? Key
    {
        get => GetValue(KeyProperty);
        set => SetValue(KeyProperty, value);
    }

    public bool IsOpen
    {
        get => _isOpen;
        private set => SetAndRaise(IsOpenProperty, ref _isOpen, value);
    }

    public static object? GetOpenKey(Control control) => control.GetValue(OpenKeyProperty);

    public static void SetOpenKey(Control control, object? value) => control.SetValue(OpenKeyProperty, value);

    public static ICommand? GetToggleCommand(Control control) => control.GetValue(ToggleCommandProperty);

    public static void SetToggleCommand(Control control, ICommand? value) => control.SetValue(ToggleCommandProperty, value);

    /// <summary>Donne le focus au titre de la section (Retour vient de la fermer, par exemple).</summary>
    public bool FocusHeader() => _header is not null && FocusNavigator.Focus(_header);

    /// <summary>
    /// Le titre de la section de clé <paramref name="key"/> dans la liste <paramref name="sections"/> : il reprend le focus
    /// quand Retour la referme (<c>ViewModels/FoldState</c>, Views/FocusRequests).
    /// </summary>
    public static Control? HeaderIn(Panel sections, object key) =>
        sections.Children.OfType<FoldSection>().FirstOrDefault(section => Equals(section.Key, key))?._header;

    /// <summary>
    /// Donne le focus au premier réglage de la section ouverte (retour d'un écran ouvert depuis elle), sinon à son titre.
    /// </summary>
    public bool FocusContent() => (IsOpen && _body is not null && FocusNavigator.FocusFirstIn(_body)) || FocusHeader();

    /// <summary>La place, dans l'ordre de lecture, du réglage de la section ouverte qui contient <paramref name="focused"/> (sinon null).</summary>
    public int? ContentIndexOf(Control focused)
    {
        if (!IsOpen || _body is null || !_body.IsVisualAncestorOf(focused))
            return null;

        var index = FocusNavigator.InReadingOrder(_body).ToList().FindIndex(c => c == focused || c.IsVisualAncestorOf(focused));
        return index >= 0 ? index : null;
    }

    /// <summary>Le focus au réglage de cette place (retour d'un écran ouvert depuis lui, DF-3), sinon au premier.</summary>
    public bool FocusContent(int index) =>
        IsOpen && _body is not null && FocusNavigator.InReadingOrder(_body) is { } settings && index < settings.Count
            ? FocusNavigator.Focus(settings[index])
            : FocusContent();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_header is not null)
            _header.Click -= OnHeaderClick;

        _header = e.NameScope.Find<Button>("PART_Header");
        _body = e.NameScope.Find<Control>("PART_ContentPresenter");
        if (_header is not null)
            _header.Click += OnHeaderClick;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != KeyProperty && change.Property != OpenKeyProperty)
            return;

        var open = Key is not null && Equals(Key, GetValue(OpenKeyProperty));
        if (open == IsOpen)
            return;

        IsOpen = open;
        PseudoClasses.Set(":open", open);
        // Ouverte : la section est ramenée entière dans la zone qui défile, avec la place gardée sous l'en-tête et au-dessus
        // de la barre d'aide (Covers.Room : sa dernière ligne n'y reste pas cachée, étape 34) ; une section plus haute que la
        // zone n'y entre qu'en partie, son titre en haut (sinon c'est sa fin qui serait montrée, le titre sorti par le haut)
        if (open)
            Dispatcher.UIThread.Post(BringTitleIntoView, DispatcherPriority.Loaded);
    }

    private void BringTitleIntoView()
    {
        var viewport = this.FindAncestorOfType<ScrollViewer>()?.Viewport.Height ?? Bounds.Height;
        var (above, below) = Covers.Room(this);
        var shown = Math.Min(Bounds.Height + below, viewport - above - TileMetrics.ScrollRoom);
        this.BringIntoView(new Rect(0, -above, Bounds.Width, above + Math.Max(0, shown)));
    }

    private void OnHeaderClick(object? sender, RoutedEventArgs e)
    {
        if (GetValue(ToggleCommandProperty) is { } command && command.CanExecute(Key))
            command.Execute(Key);
    }
}
