using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace WinSU.App.Controls;

/// <summary>
/// Ce qui passe sous un élément « couvrant » (la barre d'aide, l'en-tête « Vos consoles ») est atténué : 30.5 et 30.6 pour
/// les files de l'accueil Standard, rendu commun à toutes les listes à l'étape 32 (D-127 : tuiles du catalogue, vignettes
/// de SteamGridDB, lignes des Réglages, de l'écran « Boutons »…).
/// <list type="bullet">
/// <item><see cref="IsCoverProperty"/> : posé sur un élément couvrant (MainWindow pose la barre d'aide).</item>
/// <item><see cref="FadesProperty"/> : l'élément s'atténue lui-même quand il passe dessous.</item>
/// <item><see cref="FadeChildrenProperty"/> : posé sur un panneau, chacun de ses enfants s'atténue (les lignes d'une
/// section, les tuiles d'une file).</item>
/// <item><see cref="FadeItemsProperty"/> : posé sur une liste, chacun de ses éléments s'atténue (les tuiles d'une grille,
/// les cartes d'une rangée).</item>
/// <item><see cref="MasksAboveProperty"/> : posé sur un grand contenant (la carte d'une section ouverte des Réglages), seule
/// sa partie au-dessus de la ligne d'un en-tête s'atténue, par un masque en dégradé (étape 34).</item>
/// <item><see cref="TopEdgeCoversProperty"/> : posé sur un panneau qui atténue ses enfants, son bord haut compte comme un
/// élément couvrant : ce qui en dépasse est à 10 % (DF-15 : la place du haut de la colonne des jeux, coupée par l'écran,
/// reste un aperçu atténué ; une place entière ne l'est jamais).</item>
/// </list>
/// Opacité (<see cref="OpacityFor"/>) : 1 à découvert, <see cref="CoveredOpacity"/> dès que le contenu de l'élément touche
/// un élément couvrant (étape 33, demande de l'utilisateur : « 10 % lorsque les éléments touchent » ; avant : dès qu'un
/// quart de sa surface était dessous), avec une courte rampe de <see cref="TouchDepth"/> (pas de saut pendant un
/// glissement). À découvert, la valeur locale est retirée : les styles (élément désactivé…) reprennent la main. Les files
/// qui glissent (<see cref="TrackPanel"/>) suivent la même règle.
/// <para>
/// Haut d'une zone qui défile (étape 34, demande de l'utilisateur : « fondu au bord ») : dans une bande de
/// <see cref="TopFade"/> sous son bord haut, un élément passe progressivement à <see cref="CoveredOpacity"/> avant d'être
/// coupé (<see cref="TopFadeOpacity"/>) ; la bande ne grandit qu'avec le défilement : une liste au repos reste nette.
/// </para>
/// </summary>
public sealed class Covers
{
    public static readonly AttachedProperty<bool> IsCoverProperty =
        AvaloniaProperty.RegisterAttached<Covers, Control, bool>("IsCover");

    public static readonly AttachedProperty<bool> FadesProperty =
        AvaloniaProperty.RegisterAttached<Covers, Control, bool>("Fades");

    public static readonly AttachedProperty<bool> FadeChildrenProperty =
        AvaloniaProperty.RegisterAttached<Covers, Panel, bool>("FadeChildren");

    public static readonly AttachedProperty<bool> FadeItemsProperty =
        AvaloniaProperty.RegisterAttached<Covers, ItemsControl, bool>("FadeItems");

    public static readonly AttachedProperty<bool> MasksAboveProperty =
        AvaloniaProperty.RegisterAttached<Covers, Control, bool>("MasksAbove");

    public static readonly AttachedProperty<bool> TopEdgeCoversProperty =
        AvaloniaProperty.RegisterAttached<Covers, Panel, bool>("TopEdgeCovers");

    /// <summary>Opacité d'un élément sous un élément couvrant (30.6, demande de l'utilisateur : 10 %).</summary>
    public const double CoveredOpacity = 0.1;

    /// <summary>
    /// Profondeur (DIP) du contenu d'un élément sous un élément couvrant à partir de laquelle il est à
    /// <see cref="CoveredOpacity"/> ; en deçà, l'opacité descend avec elle (pas de saut pendant un glissement).
    /// </summary>
    public const double TouchDepth = 12;

    /// <summary>Hauteur (DIP) de la bande du haut d'une zone qui défile où les éléments s'atténuent (étape 34).</summary>
    public const double TopFade = 40;

    /// <summary>Les éléments couvrants affichés (toutes fenêtres confondues ; chacun ne compte que dans la sienne).</summary>
    private static readonly HashSet<Control> Registered = [];

    static Covers()
    {
        IsCoverProperty.Changed.AddClassHandler<Control>((cover, e) => Watch(cover, e.NewValue is true, OnCoverAttached, OnCoverDetached));
        FadesProperty.Changed.AddClassHandler<Control>((element, e) => Watch(element, e.NewValue is true, OnFaderAttached, OnFaderDetached));
        FadeChildrenProperty.Changed.AddClassHandler<Panel>((panel, e) => Watch(panel, e.NewValue is true, OnFaderAttached, OnFaderDetached));
        FadeItemsProperty.Changed.AddClassHandler<ItemsControl>((list, e) => Watch(list, e.NewValue is true, OnFaderAttached, OnFaderDetached));
        MasksAboveProperty.Changed.AddClassHandler<Control>((element, e) => Watch(element, e.NewValue is true, OnFaderAttached, OnFaderDetached));
    }

    private Covers()
    {
    }

    public static bool GetIsCover(Control element) => element.GetValue(IsCoverProperty);

    public static void SetIsCover(Control element, bool value) => element.SetValue(IsCoverProperty, value);

    public static bool GetFades(Control element) => element.GetValue(FadesProperty);

    public static void SetFades(Control element, bool value) => element.SetValue(FadesProperty, value);

    public static bool GetFadeChildren(Panel panel) => panel.GetValue(FadeChildrenProperty);

    public static void SetFadeChildren(Panel panel, bool value) => panel.SetValue(FadeChildrenProperty, value);

    public static bool GetFadeItems(ItemsControl list) => list.GetValue(FadeItemsProperty);

    public static void SetFadeItems(ItemsControl list, bool value) => list.SetValue(FadeItemsProperty, value);

    public static bool GetMasksAbove(Control element) => element.GetValue(MasksAboveProperty);

    public static void SetMasksAbove(Control element, bool value) => element.SetValue(MasksAboveProperty, value);

    public static bool GetTopEdgeCovers(Panel panel) => panel.GetValue(TopEdgeCoversProperty);

    public static void SetTopEdgeCovers(Panel panel, bool value) => panel.SetValue(TopEdgeCoversProperty, value);

    /// <summary>
    /// Masque d'un élément de hauteur <paramref name="height"/> dont la ligne d'un en-tête passe à <paramref name="line"/>
    /// (dans son repère) : <see cref="CoveredOpacity"/> au-dessus, pleine opacité <see cref="TouchDepth"/> plus bas ; les
    /// positions des arrêts du dégradé, de 0 à 1. Null si la ligne ne coupe pas l'élément (au-dessus : rien à masquer).
    /// </summary>
    public static (double FadeEnd, double ClearStart)? MaskStops(double line, double height)
    {
        if (height <= 0 || line <= 0)
            return null;

        var fadeEnd = Math.Min(line / height, 1);
        return (fadeEnd, Math.Min((line + TouchDepth) / height, 1));
    }

    /// <summary>
    /// Opacité d'un élément dont le contenu est à <paramref name="depth"/> (DIP) sous un élément couvrant : 1 à découvert,
    /// <see cref="CoveredOpacity"/> dès <see cref="TouchDepth"/>, entre les deux en proportion.
    /// </summary>
    public static double OpacityFor(double depth) =>
        1 - ((1 - CoveredOpacity) * Math.Clamp(depth / TouchDepth, 0, 1));

    /// <summary>
    /// Opacité d'un élément dont le haut est à <paramref name="top"/> (DIP) du bord haut de sa zone qui défile, après un
    /// défilement de <paramref name="scrolled"/> : 1 sous la bande, <see cref="CoveredOpacity"/> au bord (et au-dessus),
    /// entre les deux en proportion ; la bande vaut <see cref="TopFade"/> au plus, le défilement tant qu'il est plus petit
    /// (rien ne s'atténue tant que la liste n'a pas défilé).
    /// </summary>
    public static double TopFadeOpacity(double top, double scrolled)
    {
        var band = Math.Min(TopFade, Math.Max(0, scrolled));
        return band <= 0 ? 1 : 1 - ((1 - CoveredOpacity) * Math.Clamp((band - top) / band, 0, 1));
    }

    /// <summary>
    /// Profondeur de <paramref name="rect"/> sous les éléments couvrants <paramref name="covers"/> (même repère) : la
    /// hauteur de la plus grande partie commune ; 0 s'ils ne se touchent pas.
    /// </summary>
    public static double DepthUnder(Rect rect, IEnumerable<Rect> covers)
    {
        var depth = 0.0;
        foreach (var cover in covers)
        {
            var overlap = rect.Intersect(cover);
            if (overlap.Width > 0 && overlap.Height > 0)
                depth = Math.Max(depth, overlap.Height);
        }

        return depth;
    }

    /// <summary>
    /// Profondeur au-delà du bord haut d'un panneau (<see cref="TopEdgeCoversProperty"/>) d'un élément dont le haut est à
    /// <paramref name="top"/> dans le panneau : 0 s'il est entier dessous.
    /// </summary>
    public static double DepthAboveEdge(double top) => Math.Max(0, -top);

    /// <summary>
    /// Le rectangle de <paramref name="visual"/> dans le repère de <paramref name="relativeTo"/>, ramené en entier : sa
    /// position et sa taille (DF-17 : l'interface réduite, en portrait, réduisait la position mais pas la taille) ; null
    /// s'ils ne sont pas dans le même arbre.
    /// </summary>
    public static Rect? RectIn(Visual visual, Visual relativeTo) =>
        visual.TransformToVisual(relativeTo) is { } toTarget ? new Rect(visual.Bounds.Size).TransformToAABB(toTarget) : null;

    /// <summary>Les éléments couvrants affichés de la fenêtre de <paramref name="relativeTo"/>, dans son repère.</summary>
    private static IEnumerable<Rect> CoverRects(Visual relativeTo) =>
        CoversOf(relativeTo).Select(c => RectIn(c, relativeTo)).OfType<Rect>();

    /// <summary>
    /// Place à garder au-dessus et au-dessous d'un élément qu'on fait défiler dans sa zone pour qu'il ne finisse pas sous
    /// un élément couvrant : la hauteur, en haut et en bas de la zone qui défile, des éléments couvrants (0 s'il n'y en a
    /// pas) ; dans le repère de la zone, donc de l'élément. <see cref="Input.FocusNavigator"/>
    /// l'ajoute autour de l'élément qui prend le focus. En haut : l'en-tête d'un écran (titre en capsule, état ;
    /// <see cref="HeaderRoom"/>, étape 33), au moins la bande <see cref="TopFade"/> (étape 34) ; en bas : la barre d'aide.
    /// </summary>
    public static (double Top, double Bottom) Room(Control element)
    {
        if (ScrollingViewport(element) is not { } viewport)
            return (0, 0);

        var (above, below) = RoomFor(viewport.Bounds.Height, CoverRects(viewport));
        return (Math.Max(above, TopFade), below);
    }

    /// <summary>
    /// <see cref="Room"/> dans le repère de la zone qui défile (de hauteur <paramref name="viewportHeight"/>) : un élément
    /// couvrant compte en bas s'il commence dans la moitié basse de la zone (la barre d'aide flotte un peu au-dessus du
    /// bord), en haut s'il finit dans la moitié haute (l'en-tête est un peu sous le bord). Qu'il soit en face de l'élément
    /// ou non : toute une rangée d'une grille défile pareil, quelle que soit la colonne (étape 33, retour de l'utilisateur :
    /// les colonnes à droite de la barre d'aide défilaient autrement).
    /// </summary>
    public static (double Top, double Bottom) RoomFor(double viewportHeight, IEnumerable<Rect> covers)
    {
        var (above, below) = (0.0, 0.0);
        foreach (var cover in covers)
        {
            if (cover.Y > viewportHeight / 2 && cover.Y < viewportHeight)
                below = Math.Max(below, viewportHeight - cover.Y);
            else if (cover.Bottom < viewportHeight / 2 && cover.Bottom > 0)
                above = Math.Max(above, cover.Bottom);
        }

        return (above, below);
    }

    /// <summary>
    /// Atténue <paramref name="element"/> quand ce qu'il montre touche un élément couvrant, ou passe dans la bande du haut de
    /// sa zone qui défile (<see cref="TopFadeOpacity"/>). Une tuile d'une liste de tuiles compte sans sa marge intérieure :
    /// elle garde la place de la tuile grossie et de ses équerres, vide au repos (une carte, elle, est dessinée jusqu'à son
    /// bord). Tout se mesure dans le repère de l'élément (DF-17), comme <see cref="TouchDepth"/>.
    /// </summary>
    public static void Apply(Control element)
    {
        if (!element.IsEffectivelyVisible || TopLevel.GetTopLevel(element) is null)
            return;

        var shown = new Rect(element.Bounds.Size);
        if (element is ListBoxItem item && ItemsControl.ItemsControlFromItemContainer(item) is { } list && list.Classes.Contains("tiles"))
            shown = shown.Deflate(item.Padding);
        var depth = DepthUnder(shown, CoverRects(element));
        if (element.GetVisualParent() is Panel panel && GetTopEdgeCovers(panel))
            depth = Math.Max(depth, DepthAboveEdge(element.Bounds.Y + shown.Y));
        var opacity = OpacityFor(depth);
        if (ScrollingViewport(element) is { } viewport && element.TranslatePoint(default, viewport) is { } inViewport)
            opacity = Math.Min(opacity, TopFadeOpacity(inViewport.Y + shown.Y, viewport.Offset.Y));
        if (opacity >= 1)
            element.ClearValue(Visual.OpacityProperty);
        else
            element.Opacity = opacity;
    }

    /// <summary>
    /// La zone qui défile vraiment en hauteur autour de <paramref name="element"/> : une rangée de cartes a sa propre zone,
    /// figée (défilement coupé), qu'on saute pour trouver celle des Réglages (étape 34 : la carte choisie restait sous la
    /// barre d'aide, sa place était calculée dans la rangée).
    /// </summary>
    private static ScrollContentPresenter? ScrollingViewport(Visual element) =>
        element.GetVisualAncestors().OfType<ScrollContentPresenter>().FirstOrDefault(p => p.CanVerticallyScroll);

    private static IEnumerable<Control> CoversOf(Visual visual)
    {
        var top = TopLevel.GetTopLevel(visual);
        return top is null ? [] : Registered.Where(c => c.IsEffectivelyVisible && TopLevel.GetTopLevel(c) == top);
    }

    /// <summary>Suit l'attachement de <paramref name="control"/> à une fenêtre tant que la propriété est vraie.</summary>
    private static void Watch(
        Control control,
        bool enabled,
        EventHandler<VisualTreeAttachmentEventArgs> attached,
        EventHandler<VisualTreeAttachmentEventArgs> detached)
    {
        control.AttachedToVisualTree -= attached;
        control.DetachedFromVisualTree -= detached;
        if (TopLevel.GetTopLevel(control) is not null)
            detached(control, null!);
        if (!enabled)
            return;

        control.AttachedToVisualTree += attached;
        control.DetachedFromVisualTree += detached;
        if (TopLevel.GetTopLevel(control) is not null)
            attached(control, null!);
    }

    private static void OnCoverAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control cover)
            Registered.Add(cover);
    }

    private static void OnCoverDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control cover)
            Registered.Remove(cover);
    }

    // Après chaque mise en page de la fenêtre (défilement, changement de taille, barre d'aide qui change) : l'opacité est
    // recalculée. LayoutUpdated ne se branche qu'une fois l'élément dans une fenêtre.
    private static void OnFaderAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control control)
            control.LayoutUpdated += OnLayoutUpdated;
    }

    private static void OnFaderDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control control)
            control.LayoutUpdated -= OnLayoutUpdated;
    }

    private static void OnLayoutUpdated(object? sender, EventArgs e)
    {
        switch (sender)
        {
            case Panel panel when GetFadeChildren(panel):
                // Une liste qui atténue ses éléments un par un (rangée de cartes sur plusieurs lignes) n'est pas atténuée en bloc
                foreach (var child in panel.Children.Where(c => c is not ItemsControl list || !GetFadeItems(list)))
                    Apply(child);
                break;
            case ItemsControl list when GetFadeItems(list):
                foreach (var container in list.GetRealizedContainers())
                    Apply(container);
                break;
            case Control control when GetMasksAbove(control):
                ApplyMask(control);
                break;
            case Control control:
                Apply(control);
                break;
        }
    }

    /// <summary>
    /// Masque la partie de <paramref name="element"/> au-dessus de la ligne des en-têtes de sa zone qui défile (éléments
    /// couvrants dont le bas est dans sa moitié haute, en face de lui) : <see cref="MaskStops"/>.
    /// </summary>
    private static void ApplyMask(Control element)
    {
        if (ScrollingViewport(element) is not { } viewport || element.TranslatePoint(default, viewport) is not { } origin)
            return;

        var line = 0.0;
        foreach (var cover in CoverRects(viewport))
        {
            var faces = cover.X < origin.X + element.Bounds.Width && cover.Right > origin.X;
            if (faces && cover.Bottom < viewport.Bounds.Height / 2)
                line = Math.Max(line, cover.Bottom - origin.Y);
        }

        if (MaskStops(line, element.Bounds.Height) is not { } stops)
        {
            element.ClearValue(Visual.OpacityMaskProperty);
            return;
        }

        var faded = Color.FromArgb((byte)(255 * CoveredOpacity), 0, 0, 0);
        element.OpacityMask = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(faded, 0),
                new GradientStop(faded, stops.FadeEnd),
                new GradientStop(Colors.Black, stops.ClearStart),
                new GradientStop(Colors.Black, 1),
            ],
        };
    }
}
