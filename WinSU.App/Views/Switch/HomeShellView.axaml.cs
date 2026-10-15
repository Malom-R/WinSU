using Avalonia.Controls;
using Avalonia.VisualTree;
using WinSU.App.Input;
using WinSU.App.ViewModels.Shell;

namespace WinSU.App.Views.Switch;

public partial class HomeShellView : UserControl, IHintBarPlacement
{
    /// <summary>
    /// Élément nommé qui avait le focus sur l'accueil (bouton rond, tuile « Jeux »…) : il le retrouve au retour. Gardé
    /// hors de la vue : elle est recréée au retour d'un écran ouvert par-dessus (les jeux d'une console).
    /// </summary>
    private static string? s_homeFocus;

    public HomeShellView()
    {
        InitializeComponent();
        GotFocus += (_, e) =>
        {
            if (DataContext is HomeShellViewModel { IsHomeSelected: true } && e.Source is Control source)
                s_homeFocus = source.Name;
        };

        // Après le changement de page : sur l'accueil, l'élément qui avait le focus (sinon la rangée) ; ailleurs, le premier
        // élément de la page, sauf si sa vue l'a déjà placé (Views/TabFocus)
        TabFocus.Follow(this, TabHost, () => FocusHomeElement() || FocusNavigator.FocusFirstIn(TabHost));
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is HomeShellViewModel shell)
            shell.TabsAsPages = true;
    }

    private bool FocusHomeElement() =>
        DataContext is HomeShellViewModel { IsHomeSelected: true } && s_homeFocus is { } name
        && this.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == name && c.IsEffectivelyVisible) is { } previous
        && FocusNavigator.Focus(previous);
}
