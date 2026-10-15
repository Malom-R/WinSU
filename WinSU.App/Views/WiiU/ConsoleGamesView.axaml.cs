using Avalonia;
using WinSU.App.ViewModels.Shell;

namespace WinSU.App.Views.WiiU;

/// <summary>
/// Écran refait dans le style : jusqu'en bas, sans barre d'aide (D-63) ; Retour n'y fait rien, l'icône « retour » du bas
/// ramène aux consoles (D-68) ; le nom de l'élément choisi est en haut à droite (D-66).
/// </summary>
public partial class ConsoleGamesView : WiiUGridPage, IHintBarPlacement
{
    private ConsoleGamesViewModel? _viewModel;

    public ConsoleGamesView()
    {
        InitializeComponent();
        UseGrid(TileList, Slots, Pager);
        GotFocus += (_, e) =>
        {
            var name = FocusName.Of(e.Source as Visual);
            NameText.Text = name;
            NamePill.IsVisible = name is not null;
        };
    }

    public bool HidesHintBar => true;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Release();
        _viewModel = DataContext as ConsoleGamesViewModel;
        if (_viewModel is not null)
            _viewModel.IgnoresBack = true;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Release();
        _viewModel = null;
    }

    /// <summary>Le ViewModel est commun aux styles : l'indication posée ici est retirée quand la vue le lâche.</summary>
    private void Release()
    {
        if (_viewModel is not null)
            _viewModel.IgnoresBack = false;
    }
}
