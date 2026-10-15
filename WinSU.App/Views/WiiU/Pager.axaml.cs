using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;

namespace WinSU.App.Views.WiiU;

/// <summary>Flèches et points de pagination d'une grille en pages (M16), tenus à jour par <see cref="WiiUGridPage"/>.</summary>
public partial class Pager : UserControl
{
    public Pager() => InitializeComponent();

    /// <summary>Flèche cliquée : −1 (page précédente) ou +1 (page suivante).</summary>
    public event EventHandler<int>? TurnRequested;

    /// <summary>Flèches seulement là où il y a une page ; un point par page, celui de la page affichée en accent.</summary>
    public void Show(int currentPage, int pageCount)
    {
        PreviousButton.IsVisible = currentPage > 0;
        NextButton.IsVisible = currentPage < pageCount - 1;
        Dots.IsVisible = pageCount > 1;
        if (Dots.Children.Count != pageCount)
        {
            Dots.Children.Clear();
            for (var i = 0; i < pageCount; i++)
                Dots.Children.Add(new Ellipse { Classes = { "pageDot" } });
        }

        for (var i = 0; i < pageCount; i++)
            Dots.Children[i].Classes.Set("current", i == currentPage);
    }

    private void OnPreviousClick(object? sender, RoutedEventArgs e) => TurnRequested?.Invoke(this, -1);

    private void OnNextClick(object? sender, RoutedEventArgs e) => TurnRequested?.Invoke(this, 1);
}
