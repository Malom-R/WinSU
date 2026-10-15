using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WinSU.App.Controls;
using WinSU.App.Input;

namespace WinSU.App.Views.WiiU;

/// <summary>
/// Base des pages en grille du style Wii U (Accueil, Jeux, jeux d'une console ; M16) : la grille en pages
/// (<see cref="PagedGridPanel"/>), ses cadres (<see cref="PageSlots"/>), ses flèches et points (<see cref="Pager"/>) ;
/// au chargement, focus sur l'élément choisi (sa page est posée sans glissement), ou sur le premier dès que la liste se
/// remplit ; focus gardé quand la liste est recréée, et suivi des demandes du ViewModel.
/// </summary>
public abstract class WiiUGridPage : UserControl
{
    private ListBox? _list;
    private PageSlots? _slots;
    private Pager? _pager;
    private PagedGridPanel? _panel;

    /// <summary>La liste était vide au chargement : le focus ira au premier élément qu'elle recevra.</summary>
    private bool _focusPending;

    /// <summary>À appeler après InitializeComponent, avec la grille de la page, ses cadres (dessous) et ses flèches.</summary>
    protected void UseGrid(ListBox list, PageSlots slots, Pager pager)
    {
        _list = list;
        _slots = slots;
        _pager = pager;
        ListFocus.KeepOnReset(list); // tuiles recréées à la fin d'une session (date de dernier lancement)
        list.Items.CollectionChanged += (_, _) => slots.Occupied = list.Items.Count; // les éléments dessinent leur cadre
        pager.TurnRequested += (_, offset) => Turn(offset);
        FocusRequests.Follow(this, list); // après un déplacement, un retrait ou un ajout : sa page vient avec lui
        list.ContainerPrepared += (_, _) =>
        {
            if (_focusPending)
                Dispatcher.UIThread.Post(FocusSelected, DispatcherPriority.Loaded);
        };
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (_list?.ItemsPanelRoot is PagedGridPanel panel && panel != _panel)
        {
            _panel = panel;
            panel.PropertyChanged += (_, change) =>
            {
                if (change.Property == PagedGridPanel.PagePositionProperty && _slots is not null)
                    _slots.PagePosition = panel.PagePosition; // les cadres glissent avec les tuiles
                else if (change.Property == PagedGridPanel.CurrentPageProperty || change.Property == PagedGridPanel.PageCountProperty)
                    ShowPages();
            };
        }

        ShowPages();
        if (!FocusNavigator.HasFocusWithin(this))
            FocusSelected();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _focusPending = false;
    }

    private void ShowPages()
    {
        if (_panel is null)
            return;

        _pager?.Show(_panel.CurrentPage, _panel.PageCount);
        if (_slots is not null)
        {
            _slots.PageCount = _panel.PageCount;
            _slots.PagePosition = _panel.PagePosition;
            _slots.Occupied = _list?.Items.Count ?? 0;
        }
    }

    /// <summary>Focus sur l'élément choisi de la grille (le premier s'il n'y en a pas) ; en attente si elle est vide.</summary>
    private void FocusSelected()
    {
        if (_list is null || TopLevel.GetTopLevel(this) is null)
            return;

        _focusPending = !ListFocus.FocusSelected(_list);
    }

    /// <summary>Flèche cliquée : la page voisine ; le focus suit s'il était dans la grille.</summary>
    private void Turn(int offset)
    {
        if (_panel is null || _list is null)
            return;

        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        var inGrid = focused is not null && _list.IsVisualAncestorOf(focused);
        if (_panel.TurnPage(inGrid ? focused : null, offset) is { } target && inGrid)
            FocusNavigator.Focus(target);
    }
}
