using WinSU.App.ViewModels.Shell;

namespace WinSU.App.Views.WiiU;

public partial class HomeTabView : WiiUGridPage
{
    public HomeTabView()
    {
        InitializeComponent();
        UseGrid(RecentList, Slots, Pager);

        // Bannière héro : seulement quand le focus est dans la grille des récents (D-26). Le focus peut partir hors de
        // cette vue (icônes du bas de l'écran principal) : on suit la liste elle-même.
        RecentList.PropertyChanged += (_, e) =>
        {
            if (e.Property == IsKeyboardFocusWithinProperty && DataContext is HomeTabViewModel home)
                home.IsRecentFocused = RecentList.IsKeyboardFocusWithin;
        };
    }
}
