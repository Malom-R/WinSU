using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using static WinSU.App.Controls.PageGridMetrics;

namespace WinSU.App.Controls;

/// <summary>
/// Verre des emplacements vides d'une grille en pages (style Wii U, M16), et de ceux de la page vide qui suit la dernière
/// quand celle-ci est pleine (la colonne qui dépasse à droite, <see cref="PageGridLayout.DrawnSlots"/>) ; les emplacements occupés (<see cref="Occupied"/>) sont la tuile
/// de leur élément, mise à l'échelle du cadre (étape 29) : le verre est dessiné ici à la place et au rayon de sa face, par
/// le même peintre et avec les mêmes pinceaux que <see cref="TileGlass"/>.
/// Posé sous la liste, à la même place : même géométrie (<see cref="PageGridLayout"/>) ; la page et le glissement viennent
/// de son <see cref="PagedGridPanel"/> (Views/WiiU/WiiUGridPage). Avalonia 12 ne laisse pas un Panel dessiner lui-même
/// (Render scellé).
/// </summary>
public sealed class PageSlots : Control
{
    public static readonly StyledProperty<IBrush?> FillProperty = TileGlass.FillProperty.AddOwner<PageSlots>();

    public static readonly StyledProperty<IBrush?> LensProperty = TileGlass.LensProperty.AddOwner<PageSlots>();

    public static readonly StyledProperty<IBrush?> RimProperty = TileGlass.RimProperty.AddOwner<PageSlots>();

    public static readonly StyledProperty<BoxShadows> ShadowProperty = TileGlass.ShadowProperty.AddOwner<PageSlots>();

    public static readonly StyledProperty<BoxShadows> InnerGlowProperty = TileGlass.InnerGlowProperty.AddOwner<PageSlots>();

    public static readonly StyledProperty<double> PagePositionProperty =
        AvaloniaProperty.Register<PageSlots, double>(nameof(PagePosition));

    public static readonly StyledProperty<int> PageCountProperty =
        AvaloniaProperty.Register<PageSlots, int>(nameof(PageCount), defaultValue: 1);

    public static readonly StyledProperty<int> OccupiedProperty =
        AvaloniaProperty.Register<PageSlots, int>(nameof(Occupied));

    private Size _clipSize;

    static PageSlots()
    {
        AffectsRender<PageSlots>(FillProperty, LensProperty, RimProperty, ShadowProperty, InnerGlowProperty, PagePositionProperty,
            PageCountProperty, OccupiedProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<PageSlots>(false);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public IBrush? Lens
    {
        get => GetValue(LensProperty);
        set => SetValue(LensProperty, value);
    }

    public IBrush? Rim
    {
        get => GetValue(RimProperty);
        set => SetValue(RimProperty, value);
    }

    public BoxShadows Shadow
    {
        get => GetValue(ShadowProperty);
        set => SetValue(ShadowProperty, value);
    }

    public BoxShadows InnerGlow
    {
        get => GetValue(InnerGlowProperty);
        set => SetValue(InnerGlowProperty, value);
    }

    public double PagePosition
    {
        get => GetValue(PagePositionProperty);
        set => SetValue(PagePositionProperty, value);
    }

    public int PageCount
    {
        get => GetValue(PageCountProperty);
        set => SetValue(PageCountProperty, value);
    }

    /// <summary>Nombre d'éléments de la grille : leurs emplacements, les premiers, ne sont pas dessinés ici.</summary>
    public int Occupied
    {
        get => GetValue(OccupiedProperty);
        set => SetValue(OccupiedProperty, value);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (finalSize != _clipSize)
        {
            _clipSize = finalSize;
            Clip = new RectangleGeometry(PageGridLayout.Compute(finalSize).VisibleArea(finalSize));
        }

        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var layout = PageGridLayout.Compute(Bounds.Size);
        if (layout.Frame <= 0)
            return;

        var paint = new TileGlassPaint(Fill, Lens, Rim, Shadow, InnerGlow);
        var bounds = new Rect(Bounds.Size);
        var first = Math.Max(Occupied, Math.Max(0, (int)Math.Floor(PagePosition)) * PerPage);
        var last = PageGridLayout.DrawnSlots(Occupied);
        for (var slot = first; slot < last; slot++)
        {
            if (layout.FrameRect(slot, PagePosition).Intersects(bounds))
                TileGlassPainter.Paint(context, layout.FaceRect(slot, PagePosition), layout.FaceRadius, layout.GlassStrokeThickness, paint);
        }
    }
}
