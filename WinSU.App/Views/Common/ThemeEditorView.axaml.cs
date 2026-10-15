using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using WinSU.App.Controls;
using WinSU.App.Input;
using WinSU.App.ViewModels;

namespace WinSU.App.Views.Common;

/// <summary>
/// Éditeur de thème (M14), au style des Réglages (étape 32, D-131) : commun aux trois styles ; ses panneaux s'arrêtent
/// au-dessus de la barre d'aide.
/// </summary>
public partial class ThemeEditorView : UserControl
{
    public ThemeEditorView()
    {
        InitializeComponent();
        // Fin du réglage d'une couleur : le focus reste (ou revient) sur sa pastille ; Retour a refermé une section : sur son titre
        FocusRequests.Follow(this, slot => Rows.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.DataContext == slot));
        FocusRequests.Follow(this, context => (context as ThemeEditorViewModel)?.Fold, key => FoldSection.HeaderIn(Rows, key));
    }

    private ThemeEditorViewModel? Editor => DataContext as ThemeEditorViewModel;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        FocusNavigator.FocusFirstIn(Rows);
    }

    // Sélecteur à la souris (demande de l'utilisateur) : un clic place le curseur, le glissé le suit (pointeur capturé
    // tant que le bouton est enfoncé, même hors du carré ou de la barre), la molette tourne la teinte.

    private void OnSquarePressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(Square).Properties.IsLeftButtonPressed || Editor is not { } editor)
            return;

        e.Pointer.Capture(Square);
        var point = e.GetPosition(Square);
        editor.Picker.PointSquare(point.X, point.Y);
        e.Handled = true;
    }

    private void OnSquareMoved(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Captured != Square || Editor is not { } editor)
            return;

        var point = e.GetPosition(Square);
        editor.Picker.PointSquare(point.X, point.Y);
    }

    private void OnHuePressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(HueBar).Properties.IsLeftButtonPressed || Editor is not { } editor)
            return;

        e.Pointer.Capture(HueBar);
        editor.Picker.PointHue(e.GetPosition(HueBar).X);
        e.Handled = true;
    }

    private void OnHueMoved(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Captured == HueBar && Editor is { } editor)
            editor.Picker.PointHue(e.GetPosition(HueBar).X);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e) => e.Pointer.Capture(null);

    private void OnPickerWheel(object? sender, PointerWheelEventArgs e)
    {
        if (Editor is not { } editor || e.Delta.Y == 0)
            return;

        editor.Picker.WheelHue(e.Delta.Y);
        e.Handled = true;
    }
}
