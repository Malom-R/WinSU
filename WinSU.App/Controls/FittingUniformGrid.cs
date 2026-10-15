using Avalonia;
using Avalonia.Controls.Primitives;

namespace WinSU.App.Controls;

/// <summary>
/// Grille uniforme dont le nombre de colonnes suit la largeur disponible : autant de cases de <see cref="CellWidth"/>
/// (à peu près) qu'il en tient ; les cases s'étirent un peu pour occuper toute la largeur (grilles du style Switch, D-69).
/// </summary>
public sealed class FittingUniformGrid : UniformGrid
{
    public static readonly StyledProperty<double> CellWidthProperty =
        AvaloniaProperty.Register<FittingUniformGrid, double>(nameof(CellWidth), TileMetrics.SwitchGridCell);

    static FittingUniformGrid() => AffectsMeasure<FittingUniformGrid>(CellWidthProperty);

    public double CellWidth
    {
        get => GetValue(CellWidthProperty);
        set => SetValue(CellWidthProperty, value);
    }

    /// <summary>Nombre de colonnes pour une largeur : la plus proche de cases de <paramref name="cellWidth"/>, une au moins.</summary>
    public static int ColumnsFor(double width, double cellWidth) =>
        double.IsFinite(width) && cellWidth > 0 ? Math.Max(1, (int)Math.Round(width / cellWidth)) : 1;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (double.IsFinite(availableSize.Width))
            SetCurrentValue(ColumnsProperty, ColumnsFor(availableSize.Width, CellWidth));

        return base.MeasureOverride(availableSize);
    }
}
