using System.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;
using WinSU.App.Input;
using WinSU.App.ViewModels;
using WinSU.App.ViewModels.Shell;

namespace WinSU.App.Views.Switch;

/// <summary>
/// Base des pages en grille du style Switch (Jeux, jeux d'une console ; M15) : la tuile « + » des grilles
/// Standard n'y est pas montrée, son action passe au grand bouton sous la grille (référence : « Télécharger un
/// logiciel à nouveau », D-54) ; focus gardé quand la liste est recréée, et suivi des demandes du ViewModel.
/// </summary>
public abstract class SwitchGridPage : UserControl
{
    private ListBox? _list;
    private Button? _addButton;

    /// <summary>À appeler après InitializeComponent, avec la grille de la page et son grand bouton d'ajout.</summary>
    protected void UseGrid(ListBox list, Button addButton)
    {
        _list = list;
        _addButton = addButton;
        ListFocus.KeepOnReset(list); // tuiles recréées à la fin d'une session (date de dernier lancement)
        list.ContainerPrepared += (_, e) => e.Container.IsVisible = !IsAddTile(e.Container.DataContext);
        // Le bouton tient la place de la tuile « + » : elle est choisie quand il a le focus, l'aide et Options la suivent (DF-21)
        addButton.GotFocus += (_, _) => list.SelectedItem = (list.ItemsSource as IEnumerable)?.OfType<object>().FirstOrDefault(IsAddTile);
        // Après un déplacement, un retrait ou une partie, le focus suit l'élément concerné ; la tuile « + » est le grand bouton ici
        FocusRequests.Follow(this, item => list.ContainerFromItem(item) is { IsVisible: true } container ? container
            : IsAddTile(item) ? addButton
            : null);
    }

    /// <summary>Le grand bouton d'ajout : l'action de la tuile « + ».</summary>
    protected void OnAddClick(object? sender, RoutedEventArgs e) =>
        (_list?.ItemsSource as IEnumerable)?.OfType<IActivatable>().FirstOrDefault(IsAddTile)?.Activate();

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (_list is null || FocusNavigator.HasFocusWithin(this))
            return;

        // Au retour, le focus revient où l'on était : la tuile choisie, ou le grand bouton si c'est la tuile « + »
        if (_list.SelectedItem is { } item && IsAddTile(item))
            FocusNavigator.Focus(_addButton!);
        else if (_list.ContainerFromIndex(Math.Max(_list.SelectedIndex, 0)) is { IsVisible: true } container)
            FocusNavigator.Focus(container);
        else
            FocusNavigator.FocusFirstIn(this);
    }

    private static bool IsAddTile(object? item) =>
        item is AddConsoleTileViewModel or AddGamesTileViewModel;
}
