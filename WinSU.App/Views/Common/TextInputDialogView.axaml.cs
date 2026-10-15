using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WinSU.App.Overlays;

namespace WinSU.App.Views.Common;

public partial class TextInputDialogView : UserControl
{
    public TextInputDialogView()
    {
        InitializeComponent();
        // Dans la zone de texte, l'aiguillage clavier est désactivé : Entrée et Échap sont gérées ici.
        Input.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Input.Focus();
        Input.SelectAll();
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not TextInputDialogViewModel dialog)
            return;

        switch (e.Key)
        {
            case Key.Enter:
                dialog.Confirm();
                e.Handled = true;
                break;
            case Key.Escape:
                dialog.Cancel();
                e.Handled = true;
                break;
        }
    }
}
