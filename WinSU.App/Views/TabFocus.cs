using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using WinSU.App.Input;
using WinSU.App.ViewModels.Shell;

namespace WinSU.App.Views;

/// <summary>
/// Le focus de l'écran principal, le même pour les trois styles (étape 36, C-3) : au chargement et à chaque changement
/// d'onglet (ou de page), une fois la mise en page faite, s'il n'est pas déjà dans l'onglet (sa vue l'a placé), la vue de
/// l'onglet choisit (<see cref="IInitialFocus"/>), sinon le style (<c>place</c>), sinon le premier élément de l'onglet dans
/// l'ordre de lecture. Chaque style garde sa disposition et ses particularités.
/// </summary>
public static class TabFocus
{
    /// <param name="shell">La vue de l'écran principal (son ViewModel : <see cref="HomeShellViewModel"/>).</param>
    /// <param name="tabHost">Là où s'affiche l'onglet choisi.</param>
    /// <param name="place">Le choix du style quand la vue de l'onglet ne choisit pas ; par défaut, le premier élément de l'onglet.</param>
    public static void Follow(Control shell, ContentControl tabHost, Func<bool>? place = null)
    {
        void FocusSoon() => Dispatcher.UIThread.Post(() =>
        {
            if (TopLevel.GetTopLevel(shell) is null || FocusNavigator.HasFocusWithin(tabHost))
                return;

            if (tabHost.Presenter?.Child is IInitialFocus initial && initial.FocusInitial())
                return;

            if (place is not null)
                place();
            else
                FocusNavigator.FocusFirstIn(tabHost);
        }, DispatcherPriority.Loaded);

        void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HomeShellViewModel.SelectedTab))
                FocusSoon();
        }

        ViewModelWatch.Follow<HomeShellViewModel>(shell, vm => vm.PropertyChanged += OnPropertyChanged, vm => vm.PropertyChanged -= OnPropertyChanged);
        shell.Loaded += (_, _) => FocusSoon();
    }
}
