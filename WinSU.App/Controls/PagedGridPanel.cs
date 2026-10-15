using System.Collections.Specialized;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WinSU.App.Input;
using WinSU.Core.Input;

namespace WinSU.App.Controls;

/// <summary>
/// Panneau d'une grille en pages (style Wii U, M16, D-56) : les éléments d'une ListBox rangés par pages de 5 × 3, côte à
/// côte, chacun à la place de son cadre (qu'il dessine lui-même ; les cadres vides sont dessinés dessous par
/// <see cref="PageSlots"/>) ; la page affichée glisse (courbe douce du
/// site) et la première colonne de la suivante dépasse à droite. Seuls les éléments de la page affichée sont atteignables
/// (<see cref="IPagedArea"/>) ; la page suit le focus.
/// </summary>
public sealed class PagedGridPanel : Panel, IPagedArea
{
    /// <summary>Page affichée, fractionnaire pendant le glissement d'une page à l'autre.</summary>
    public static readonly StyledProperty<double> PagePositionProperty =
        AvaloniaProperty.Register<PagedGridPanel, double>(nameof(PagePosition));

    public static readonly DirectProperty<PagedGridPanel, int> CurrentPageProperty =
        AvaloniaProperty.RegisterDirect<PagedGridPanel, int>(nameof(CurrentPage), p => p.CurrentPage);

    public static readonly DirectProperty<PagedGridPanel, int> PageCountProperty =
        AvaloniaProperty.RegisterDirect<PagedGridPanel, int>(nameof(PageCount), p => p.PageCount);

    private int _currentPage;
    private int _pageCount = 1;
    private Size _clipSize;

    /// <summary>Faux jusqu'au premier affichage : la page de l'élément choisi est posée sans glissement.</summary>
    private bool _placed;

    static PagedGridPanel() => AffectsArrange<PagedGridPanel>(PagePositionProperty);

    public PagedGridPanel()
    {
        Transitions =
        [
            new DoubleTransition
            {
                Property = PagePositionProperty,
                Duration = Motion.Page,
                Easing = Motion.Soft,
            },
        ];
        AddHandler(RequestBringIntoViewEvent, OnRequestBringIntoView);
    }

    public double PagePosition
    {
        get => GetValue(PagePositionProperty);
        set => SetValue(PagePositionProperty, value);
    }

    public int CurrentPage => _currentPage;

    public int PageCount => _pageCount;

    public bool Shows(Control element) => IndexOf(element) is var index && (index < 0 || PageGridLayout.PageOf(index) == _currentPage);

    public Control? Neighbour(Control from, NavigationDirection direction)
    {
        var step = direction switch
        {
            NavigationDirection.Left => -1,
            NavigationDirection.Right => 1,
            _ => 0,
        };
        var columns = PageGridLayout.Compute(Bounds.Size).Columns; // 5 couchée, 3 debout
        var target = step == 0 ? -1 : PageGridLayout.Neighbour(IndexOf(from), Children.Count, step, columns);
        return target < 0 ? null : Children[target];
    }

    public Control? TurnPage(Control? from, int offset)
    {
        var target = PageGridLayout.Turn(_currentPage, from is null ? -1 : IndexOf(from), Children.Count, offset);
        if (target < 0)
            return null;

        ShowPage(PageGridLayout.PageOf(target));
        return Children[target];
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = new Size(
            double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height);
        var frame = PageGridLayout.Compute(size).FrameRect(0, 0).Size;
        foreach (var child in Children)
            child.Measure(frame);

        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var layout = PageGridLayout.Compute(finalSize);
        for (var i = 0; i < Children.Count; i++)
            Children[i].Arrange(layout.FrameRect(i, PagePosition));

        if (finalSize != _clipSize)
        {
            _clipSize = finalSize;
            Clip = new RectangleGeometry(layout.VisibleArea(finalSize));
        }

        return finalSize;
    }

    protected override void ChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        base.ChildrenChanged(sender, e);
        SetAndRaise(PageCountProperty, ref _pageCount, PageGridLayout.PageCount(Children.Count));
        if (_currentPage >= _pageCount)
            ShowPage(_pageCount - 1);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // Après la vue, qui donne le focus à l'élément choisi (sa page est posée sans glissement)
        Dispatcher.UIThread.Post(() => _placed = true, DispatcherPriority.Background);
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _placed = false;
    }

    private void ShowPage(int page)
    {
        page = Math.Clamp(page, 0, _pageCount - 1);
        SetAndRaise(CurrentPageProperty, ref _currentPage, page);
        if (_placed)
        {
            PagePosition = page;
            return;
        }

        var transitions = Transitions;
        Transitions = null;
        PagePosition = page;
        Transitions = transitions;
    }

    /// <summary>La page suit le focus : un élément qui le reçoit (Droite au bord, retour d'un écran…) fait venir sa page.</summary>
    private void OnRequestBringIntoView(object? sender, RequestBringIntoViewEventArgs e)
    {
        if (IndexOf(e.TargetObject) is not (>= 0 and var index))
            return;

        ShowPage(PageGridLayout.PageOf(index));
        e.Handled = true;
    }

    /// <summary>Rang de l'élément (ou de l'élément qui le contient) dans la grille ; −1 s'il n'en fait pas partie.</summary>
    private int IndexOf(Visual? visual)
    {
        for (var current = visual; current is not null; current = current.GetVisualParent())
        {
            if (current.GetVisualParent() == this && current is Control child)
                return Children.IndexOf(child);
        }

        return -1;
    }
}
