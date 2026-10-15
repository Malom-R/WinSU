using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using WinSU.App.Input;
using NavigationDirection = WinSU.Core.Input.NavigationDirection;

namespace WinSU.App.Controls;

/// <summary>
/// Panneau d'une file qui glisse (étape 30, style Standard 2.0), posé comme panneau d'éléments d'une liste : chaque élément
/// a un rang, et l'élément de rang r est centré sur <c>Ancre + (r − Offset) × Step</c>. La file suit l'élément choisi de
/// la liste : <see cref="Offset"/> devient son rang (le « point fixe », D-103), et une transition sur Offset la fait
/// glisser d'une place à l'autre ; aucun coût au repos.
/// <para>
/// Rang : l'index dans la liste ; avec <see cref="LastFirst"/>, le dernier élément est au rang 0 et les autres le suivent
/// (colonne des jeux : la tuile « + », dernière de la liste, en face de la console, les jeux au-dessus, D-104).
/// </para>
/// <para>
/// Navigation (<see cref="ITrackArea"/>) : <see cref="TowardsPrevious"/> mène au rang précédent, <see cref="TowardsNext"/>
/// au suivant ; les autres directions restent spatiales. La molette de la souris fait de même.
/// </para>
/// </summary>
public sealed class TrackPanel : Panel, ITrackArea
{
    public static readonly StyledProperty<Vector> StepProperty =
        AvaloniaProperty.Register<TrackPanel, Vector>(nameof(Step), new Vector(0, 180));

    public static readonly StyledProperty<double> AnchorXProperty =
        AvaloniaProperty.Register<TrackPanel, double>(nameof(AnchorX), 0.5);

    public static readonly StyledProperty<double> AnchorYProperty =
        AvaloniaProperty.Register<TrackPanel, double>(nameof(AnchorY), 0.5);

    public static readonly StyledProperty<double> OffsetProperty =
        AvaloniaProperty.Register<TrackPanel, double>(nameof(Offset));

    public static readonly StyledProperty<bool> LastFirstProperty =
        AvaloniaProperty.Register<TrackPanel, bool>(nameof(LastFirst));

    public static readonly StyledProperty<TrackFollow> FollowProperty =
        AvaloniaProperty.Register<TrackPanel, TrackFollow>(nameof(Follow));

    public static readonly StyledProperty<double> ItemSizeProperty =
        AvaloniaProperty.Register<TrackPanel, double>(nameof(ItemSize));

    public static readonly StyledProperty<NavigationDirection> TowardsPreviousProperty =
        AvaloniaProperty.Register<TrackPanel, NavigationDirection>(nameof(TowardsPrevious), NavigationDirection.Up);

    public static readonly StyledProperty<NavigationDirection> TowardsNextProperty =
        AvaloniaProperty.Register<TrackPanel, NavigationDirection>(nameof(TowardsNext), NavigationDirection.Down);

    private SelectingItemsControl? _list;

    static TrackPanel() => AffectsArrange<TrackPanel>(StepProperty, AnchorXProperty, AnchorYProperty, OffsetProperty, LastFirstProperty);

    public TrackPanel()
    {
        ClipToBounds = false;
        // Les tuiles qui passent sous la barre d'aide ou sous l'en-tête « Vos consoles » sont atténuées (30.5, 30.6) : la
        // règle commune (Covers), recalculée à chaque mise en page, donc à chaque image d'un glissement.
        Covers.SetFadeChildren(this, true);
        Transitions =
        [
            new DoubleTransition { Property = OffsetProperty, Duration = Motion.Slide, Easing = Motion.Soft },
        ];
    }

    /// <summary>Déplacement d'un rang au suivant (en DIP) : (−172, 172) pour la diagonale de l'accueil.</summary>
    public Vector Step
    {
        get => GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    /// <summary>Le point fixe, en part de la largeur du panneau.</summary>
    public double AnchorX
    {
        get => GetValue(AnchorXProperty);
        set => SetValue(AnchorXProperty, value);
    }

    /// <summary>Le point fixe, en part de la hauteur du panneau.</summary>
    public double AnchorY
    {
        get => GetValue(AnchorYProperty);
        set => SetValue(AnchorYProperty, value);
    }

    /// <summary>Le rang posé sur le point fixe (celui de l'élément choisi ; fractionnaire pendant un glissement).</summary>
    public double Offset
    {
        get => GetValue(OffsetProperty);
        set => SetValue(OffsetProperty, value);
    }

    /// <summary>Le dernier élément au rang 0, les autres ensuite.</summary>
    public bool LastFirst
    {
        get => GetValue(LastFirstProperty);
        set => SetValue(LastFirstProperty, value);
    }

    /// <summary>Comment la file suit l'élément choisi : point fixe (par défaut) ou au bord.</summary>
    public TrackFollow Follow
    {
        get => GetValue(FollowProperty);
        set => SetValue(FollowProperty, value);
    }

    /// <summary>
    /// Côté de ce que montre un élément (la tuile, sans la place de ses équerres) : en mode « au bord », seules les places
    /// où il tient entier comptent (DF-15) ; 0 : son centre suffit.
    /// </summary>
    public double ItemSize
    {
        get => GetValue(ItemSizeProperty);
        set => SetValue(ItemSizeProperty, value);
    }

    /// <summary>Direction qui mène au rang précédent.</summary>
    public NavigationDirection TowardsPrevious
    {
        get => GetValue(TowardsPreviousProperty);
        set => SetValue(TowardsPreviousProperty, value);
    }

    /// <summary>Direction qui mène au rang suivant.</summary>
    public NavigationDirection TowardsNext
    {
        get => GetValue(TowardsNextProperty);
        set => SetValue(TowardsNextProperty, value);
    }

    /// <summary>Le rang d'un élément d'index <paramref name="index"/> parmi <paramref name="count"/>.</summary>
    public static int RankOf(int index, int count, bool lastFirst) =>
        !lastFirst ? index : index == count - 1 ? 0 : index + 1;

    /// <summary>L'index de l'élément de rang <paramref name="rank"/> (−1 hors de la file).</summary>
    public static int IndexOf(int rank, int count, bool lastFirst)
    {
        if (rank < 0 || rank >= count)
            return -1;

        return !lastFirst ? rank : rank == 0 ? count - 1 : rank - 1;
    }

    /// <summary>
    /// Combien de places entières se voient à partir du point fixe, dans le sens du pas : un élément de côté
    /// <paramref name="itemSize"/> y tient sans passer le bord du panneau (le point fixe compris ; au moins 1). DF-15 : la
    /// place dont seul le centre était à l'écran comptait, et le jeu choisi y dépassait du haut.
    /// </summary>
    public static int Capacity(Size size, double anchorX, double anchorY, Vector step, double itemSize)
    {
        double Room(double length, double anchor, double delta) =>
            delta == 0
                ? double.PositiveInfinity
                : ((delta < 0 ? length * anchor : length * (1 - anchor)) - (itemSize / 2)) / Math.Abs(delta);

        var steps = Math.Min(Room(size.Width, anchorX, step.X), Room(size.Height, anchorY, step.Y));
        return double.IsInfinity(steps) ? int.MaxValue : Math.Max(1, (int)Math.Floor(steps) + 1);
    }

    /// <summary>
    /// Le rang posé sur le point fixe pour que l'élément de rang <paramref name="rank"/> se voie : en mode « au bord », la
    /// file ne bouge que s'il en sortirait (<paramref name="current"/> est le rang actuel au point fixe).
    /// </summary>
    public static double EdgeOffset(int rank, double current, int capacity)
    {
        if (rank < current)
            return rank;

        return rank > current + capacity - 1 ? rank - capacity + 1 : current;
    }

    /// <summary>Centre de l'élément de rang <paramref name="rank"/> dans un panneau de taille <paramref name="size"/>.</summary>
    public static Point CenterOf(double rank, Size size, double anchorX, double anchorY, Vector step, double offset) =>
        new Point(size.Width * anchorX, size.Height * anchorY) + (step * (rank - offset));

    public bool Handles(NavigationDirection direction) => direction == TowardsPrevious || direction == TowardsNext;

    public Control? Neighbour(Control from, NavigationDirection direction)
    {
        var index = _list?.IndexFromContainer(from) ?? -1;
        if (_list is null || index < 0)
            return null;

        var count = _list.ItemCount;
        var rank = RankOf(index, count, LastFirst) + (direction == TowardsNext ? 1 : -1);
        var target = IndexOf(rank, count, LastFirst);
        return target < 0 ? null : _list.ContainerFromIndex(target);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
            child.Measure(Size.Infinity);

        // La file occupe toute sa zone, quelle que soit la place de ses éléments.
        return new Size(
            double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var count = Children.Count;
        for (var i = 0; i < count; i++)
        {
            var child = Children[i];
            var index = _list?.IndexFromContainer(child) is >= 0 and var found ? found : i;
            var center = CenterOf(RankOf(index, count, LastFirst), finalSize, AnchorX, AnchorY, Step, Offset);
            var size = child.DesiredSize;
            var rect = new Rect(center.X - (size.Width / 2), center.Y - (size.Height / 2), size.Width, size.Height);
            child.Arrange(rect);
        }

        return finalSize;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _list = this.FindAncestorOfType<SelectingItemsControl>();
        if (_list is null)
            return;

        _list.SelectionChanged += OnSelectionChanged;
        JumpToSelection();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_list is not null)
            _list.SelectionChanged -= OnSelectionChanged;
        _list = null;
    }

    protected override void ChildrenChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        base.ChildrenChanged(sender, e);
        // Liste remplie ou refaite (autre console) : la file se place d'un coup, sans glisser.
        if (e.Action is System.Collections.Specialized.NotifyCollectionChangedAction.Reset || Children.Count <= 1)
            JumpToSelection();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_list is null || e.Delta.Y == 0 || _list.SelectedIndex < 0)
            return;

        var from = _list.ContainerFromIndex(_list.SelectedIndex);
        var direction = e.Delta.Y > 0 ? TowardsPrevious : TowardsNext;
        if (from is not null && Neighbour(from, direction) is { } next)
        {
            FocusNavigator.Focus(next);
            e.Handled = true;
        }
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_list is null || _list.SelectedIndex < 0)
            return;

        // Première sélection (liste remplie) : d'un coup ; ensuite, la file glisse.
        if (e.RemovedItems.Count == 0)
            JumpToSelection();
        else
            Offset = TargetOffset(Offset);
    }

    /// <summary>Le rang à poser au point fixe pour l'élément choisi, en partant de <paramref name="current"/>.</summary>
    private double TargetOffset(double current)
    {
        if (_list is not { SelectedIndex: >= 0 } list)
            return 0;

        var rank = RankOf(list.SelectedIndex, list.ItemCount, LastFirst);
        if (Follow == TrackFollow.Fixed)
            return rank;

        // Pas encore de taille (la file se place avant sa première mise en page) : rien ne glisse.
        var capacity = Bounds.Width > 0 && Bounds.Height > 0 ? Capacity(Bounds.Size, AnchorX, AnchorY, Step, ItemSize) : int.MaxValue;
        return EdgeOffset(rank, current, capacity);
    }

    private void JumpToSelection()
    {
        var target = TargetOffset(current: 0);
        var transitions = Transitions;
        Transitions = null;
        Offset = target;
        Transitions = transitions;
    }
}

/// <summary>Comment une file suit l'élément choisi (<see cref="TrackPanel.Follow"/>).</summary>
public enum TrackFollow
{
    /// <summary>L'élément choisi est toujours au point fixe : la file glisse à chaque pas (accueil, consoles).</summary>
    Fixed,

    /// <summary>La file ne glisse que si l'élément choisi sortirait de l'écran (colonne des jeux).</summary>
    Edge,
}
