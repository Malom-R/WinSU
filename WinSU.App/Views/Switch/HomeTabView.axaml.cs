using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using WinSU.App.Input;
using WinSU.App.ViewModels.Shell;

namespace WinSU.App.Views.Switch;

public partial class HomeTabView : UserControl
{
    public HomeTabView()
    {
        InitializeComponent();
        ListFocus.KeepOnReset(RecentList); // liste recréée à la fin d'une session relancée d'ici
        FocusRequests.Follow(this, RecentList); // après une action du menu Options (renommer, retirer)

        // Bannière héro et aide « Lancer » : seulement quand le focus est dans les récents (D-26). Le focus peut partir
        // hors de cette vue (boutons ronds de l'écran principal) : on suit la liste elle-même.
        RecentList.PropertyChanged += (_, e) =>
        {
            if (e.Property == IsKeyboardFocusWithinProperty && DataContext is HomeTabViewModel home)
                home.IsRecentFocused = RecentList.IsKeyboardFocusWithin;
        };
    }

    /// <summary>La tuile « Jeux » ouvre la page correspondante de l'écran principal.</summary>
    private void OnGamesClick(object? sender, RoutedEventArgs e)
    {
        if (this.FindAncestorOfType<HomeShellView>()?.DataContext is HomeShellViewModel shell)
            shell.OpenPageCommand.Execute(shell.GamesTab);
    }
}
