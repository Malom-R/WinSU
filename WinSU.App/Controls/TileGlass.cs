using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WinSU.App.Controls;

/// <summary>
/// Verre d'une tuile (étape 29, d'après le verre des tuiles de la Wii U), toujours là sous l'icône : il se voit autour
/// d'une icône plus petite que la face et dans les emplacements vides. Dessiné par <see cref="TileGlassPainter"/>, comme
/// les cadres vides de <see cref="PageSlots"/> ; les pinceaux viennent du thème (styles de Themes/Controls.axaml).
/// </summary>
public sealed class TileGlass : Control
{
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<TileGlass, CornerRadius>(nameof(CornerRadius), TileMetrics.FaceCornerRadius);

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<TileGlass, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> LensProperty =
        AvaloniaProperty.Register<TileGlass, IBrush?>(nameof(Lens));

    public static readonly StyledProperty<IBrush?> RimProperty =
        AvaloniaProperty.Register<TileGlass, IBrush?>(nameof(Rim));

    public static readonly StyledProperty<BoxShadows> ShadowProperty =
        AvaloniaProperty.Register<TileGlass, BoxShadows>(nameof(Shadow));

    public static readonly StyledProperty<BoxShadows> InnerGlowProperty =
        AvaloniaProperty.Register<TileGlass, BoxShadows>(nameof(InnerGlow));

    static TileGlass()
    {
        AffectsRender<TileGlass>(CornerRadiusProperty, FillProperty, LensProperty, RimProperty, ShadowProperty, InnerGlowProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<TileGlass>(false);
    }

    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>Fond : léger voile, bande plus sombre sous le reflet, lumière qui remonte du bas.</summary>
    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <summary>Reflet du haut, jusqu'à sa courbe (<see cref="TileGlassPainter.LensBottom"/>).</summary>
    public IBrush? Lens
    {
        get => GetValue(LensProperty);
        set => SetValue(LensProperty, value);
    }

    /// <summary>Bord lumineux (<see cref="TileMetrics.GlassStroke"/>), à l'intérieur de la face.</summary>
    public IBrush? Rim
    {
        get => GetValue(RimProperty);
        set => SetValue(RimProperty, value);
    }

    /// <summary>Ombre portée.</summary>
    public BoxShadows Shadow
    {
        get => GetValue(ShadowProperty);
        set => SetValue(ShadowProperty, value);
    }

    /// <summary>Lueur intérieure le long du bord (ombres « inset »).</summary>
    public BoxShadows InnerGlow
    {
        get => GetValue(InnerGlowProperty);
        set => SetValue(InnerGlowProperty, value);
    }

    public override void Render(DrawingContext context) =>
        TileGlassPainter.Paint(context, new Rect(Bounds.Size), CornerRadius.TopLeft, TileMetrics.GlassStroke,
            new TileGlassPaint(Fill, Lens, Rim, Shadow, InnerGlow));
}

/// <summary>Pinceaux du verre d'une tuile.</summary>
public readonly record struct TileGlassPaint(IBrush? Fill, IBrush? Lens, IBrush? Rim, BoxShadows Shadow, BoxShadows InnerGlow);

/// <summary>
/// Dessin du verre d'une tuile, commun à <see cref="TileGlass"/> et aux cadres vides de <see cref="PageSlots"/>. Couches,
/// relevées sur une tuile de la Wii U : ombre portée et fond ; reflet du haut dont le bas est une courbe qui descend au
/// milieu ; lueur intérieure et bord lumineux. (Les deux petites taches de lumière de la référence, en haut à gauche et en
/// bas à droite, ont été retirées à la demande de l'utilisateur.)
/// </summary>
public static class TileGlassPainter
{
    /// <summary>Bas du reflet aux bords de la face, rapporté à sa hauteur (référence : 59 %).</summary>
    public const double LensEdge = 0.59;

    /// <summary>Bas du reflet au milieu de la face (référence : 66,5 %) : la courbe descend au milieu.</summary>
    public const double LensCentre = 0.665;

    /// <summary>Bas du reflet à la position <paramref name="x"/> (0 à gauche, 1 à droite), rapporté à la hauteur de la face.</summary>
    public static double LensBottom(double x) => LensEdge + (4 * x * (1 - x) * (LensCentre - LensEdge));

    public static void Paint(DrawingContext context, Rect face, double radius, double stroke, TileGlassPaint paint)
    {
        if (face.Width <= 0 || face.Height <= 0)
            return;

        var rounded = new RoundedRect(face, radius);
        context.DrawRectangle(paint.Fill, null, rounded, paint.Shadow);
        if (paint.Lens is { } lens)
        {
            using (context.PushClip(rounded))
                context.DrawGeometry(lens, null, Lens(face));
        }

        // Le bord est à l'intérieur de la face (comme celui d'un Border) : contour rentré d'un demi-trait.
        var pen = paint.Rim is { } rim && stroke > 0 ? new Pen(rim, stroke) : null;
        var inner = new RoundedRect(face.Deflate(stroke / 2), Math.Max(0, radius - (stroke / 2)));
        context.DrawRectangle(Brushes.Transparent, pen, inner, paint.InnerGlow);
    }

    /// <summary>
    /// Le reflet : le haut de la face jusqu'à la courbe de <see cref="LensBottom"/> (une Bézier quadratique, dont le point
    /// de contrôle est deux fois plus bas que le milieu de la courbe au-dessus des bords).
    /// </summary>
    private static StreamGeometry Lens(Rect face)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        var edge = face.Top + (face.Height * LensEdge);
        var control = face.Top + (face.Height * ((2 * LensCentre) - LensEdge));
        ctx.BeginFigure(face.TopLeft, true);
        ctx.LineTo(face.TopRight);
        ctx.LineTo(new Point(face.Right, edge));
        ctx.QuadraticBezierTo(new Point(face.Center.X, control), new Point(face.Left, edge));
        ctx.EndFigure(true);
        return geometry;
    }
}
