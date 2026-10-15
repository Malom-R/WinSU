using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WinSU.App.Controls;

/// <summary>
/// Repère de la tuile choisie (étape 29, retour de l'utilisateur 29.14, d'après le curseur de la Wii U) : quatre équerres
/// pleines aux coins de la tuile, une couleur par coin (tirées du thème : jetons <c>WinSU.Cursor*Brush</c>), sans lueur.
/// Posé dans la boîte de la tuile, débordant de <see cref="TileMetrics.CursorOutset"/> (styles de Themes/Controls.axaml),
/// invisible au repos.
/// </summary>
public sealed class TileCursor : Control
{
    public static readonly StyledProperty<IBrush?> TopLeftProperty =
        AvaloniaProperty.Register<TileCursor, IBrush?>(nameof(TopLeft));

    public static readonly StyledProperty<IBrush?> TopRightProperty =
        AvaloniaProperty.Register<TileCursor, IBrush?>(nameof(TopRight));

    public static readonly StyledProperty<IBrush?> BottomRightProperty =
        AvaloniaProperty.Register<TileCursor, IBrush?>(nameof(BottomRight));

    public static readonly StyledProperty<IBrush?> BottomLeftProperty =
        AvaloniaProperty.Register<TileCursor, IBrush?>(nameof(BottomLeft));

    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<TileCursor, double>(nameof(Thickness), TileMetrics.CursorThickness);

    public static readonly StyledProperty<double> ArmProperty =
        AvaloniaProperty.Register<TileCursor, double>(nameof(Arm), TileMetrics.CursorArm);

    public static readonly StyledProperty<double> RadiusProperty =
        AvaloniaProperty.Register<TileCursor, double>(nameof(Radius), TileMetrics.CursorRadius);

    public static readonly StyledProperty<bool> OutsideProperty =
        AvaloniaProperty.Register<TileCursor, bool>(nameof(Outside));

    static TileCursor()
    {
        AffectsRender<TileCursor>(TopLeftProperty, TopRightProperty, BottomRightProperty, BottomLeftProperty,
            ThicknessProperty, ArmProperty, RadiusProperty, OutsideProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<TileCursor>(false);
    }

    public IBrush? TopLeft
    {
        get => GetValue(TopLeftProperty);
        set => SetValue(TopLeftProperty, value);
    }

    public IBrush? TopRight
    {
        get => GetValue(TopRightProperty);
        set => SetValue(TopRightProperty, value);
    }

    public IBrush? BottomRight
    {
        get => GetValue(BottomRightProperty);
        set => SetValue(BottomRightProperty, value);
    }

    public IBrush? BottomLeft
    {
        get => GetValue(BottomLeftProperty);
        set => SetValue(BottomLeftProperty, value);
    }

    /// <summary>Épaisseur des équerres.</summary>
    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    /// <summary>Longueur de chaque bras, depuis le coin extérieur.</summary>
    public double Arm
    {
        get => GetValue(ArmProperty);
        set => SetValue(ArmProperty, value);
    }

    /// <summary>Rayon du coin extérieur (serré, comme le curseur de la Wii U).</summary>
    public double Radius
    {
        get => GetValue(RadiusProperty);
        set => SetValue(RadiusProperty, value);
    }

    /// <summary>
    /// Autour d'une commande (29.15, D-100 : bouton, carte, pastille, ligne…) : les équerres se dessinent au dehors de ses
    /// bornes, à un jour de <see cref="TileMetrics.CursorGap"/>, à sa mesure (<see cref="Fit"/>). Sinon (tuiles), dans les
    /// bornes, que la marge du style fait déborder de la boîte de la tuile.
    /// </summary>
    public bool Outside
    {
        get => GetValue(OutsideProperty);
        set => SetValue(OutsideProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var (bounds, thickness, arm, radius) = Outside
            ? Fit(new Rect(Bounds.Size), Thickness, Arm, Radius)
            : (new Rect(Bounds.Size), Thickness, Arm, Radius);
        var brushes = new[] { TopLeft, TopRight, BottomRight, BottomLeft };
        for (var corner = 0; corner < 4; corner++)
        {
            if (brushes[corner] is { } brush)
                context.DrawGeometry(brush, null, Geometry(Corner(bounds, corner, thickness, radius, arm)));
        }
    }

    /// <summary>
    /// Équerres autour d'une commande de taille quelconque : épaisseur et bras gardés pour une commande aussi grande qu'une
    /// tuile, réduits pour une petite (9 % et 30 % de son petit côté, au moins 4 d'épaisseur), au dehors de ses bornes à un
    /// jour de <see cref="TileMetrics.CursorGap"/>.
    /// </summary>
    public static (Rect Bounds, double Thickness, double Arm, double Radius) Fit(Rect element, double thickness, double arm, double radius)
    {
        var side = Math.Min(element.Width, element.Height);
        var t = Math.Min(thickness, Math.Max(4, side * 0.09));
        var a = Math.Min(arm, Math.Max(2.5 * t, side * 0.3));
        return (element.Inflate(t + TileMetrics.CursorGap), t, a, Math.Min(radius, t));
    }

    /// <summary>
    /// Contour d'une équerre (coin 0 en haut à gauche, puis dans le sens des aiguilles d'une montre), un « L » plein comme
    /// le curseur de la Wii U : bord extérieur le long des deux côtés de <paramref name="bounds"/> sur <paramref name="arm"/>
    /// depuis le coin, coin extérieur arrondi (<paramref name="radius"/>), bouts des bras coupés à 45° vers l'intérieur,
    /// coin intérieur vif à <paramref name="thickness"/> des deux côtés.
    /// </summary>
    public static CornerShape Corner(Rect bounds, int corner, double thickness, double radius, double arm)
    {
        var t = Math.Min(thickness, arm / 2);
        var r = Math.Clamp(radius, 0, t);

        // Coin en haut à gauche, puis symétries : (sx, sy) = sens des bras depuis le coin.
        var (o, sx, sy) = corner switch
        {
            0 => (bounds.TopLeft, 1, 1),
            1 => (bounds.TopRight, -1, 1),
            2 => (bounds.BottomRight, -1, -1),
            _ => (bounds.BottomLeft, 1, -1),
        };
        Point At(double along, double across) => new(o.X + (sx * along), o.Y + (sy * across));
        return new CornerShape(
            VerticalEnd: new Point(o.X, o.Y + (sy * arm)),
            ArcStart: new Point(o.X, o.Y + (sy * r)),
            ArcEnd: At(r, 0),
            HorizontalEnd: At(arm, 0),
            HorizontalInnerEnd: At(arm - t, t),
            InnerCorner: At(t, t),
            VerticalInnerEnd: At(t, arm - t),
            Radius: r,
            Sweep: sx * sy > 0 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise);
    }

    private static StreamGeometry Geometry(CornerShape shape)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(shape.VerticalEnd, true);
        ctx.LineTo(shape.ArcStart);
        ctx.ArcTo(shape.ArcEnd, new Size(shape.Radius, shape.Radius), 0, false, shape.Sweep);
        ctx.LineTo(shape.HorizontalEnd);
        ctx.LineTo(shape.HorizontalInnerEnd);
        ctx.LineTo(shape.InnerCorner);
        ctx.LineTo(shape.VerticalInnerEnd);
        ctx.EndFigure(true);
        return geometry;
    }

    /// <summary>
    /// Contour d'une équerre : bord extérieur (bout du bras vertical, arc du coin, bout du bras horizontal), puis bord
    /// intérieur (bout coupé du bras horizontal, coin intérieur, bout coupé du bras vertical).
    /// </summary>
    public readonly record struct CornerShape(
        Point VerticalEnd, Point ArcStart, Point ArcEnd, Point HorizontalEnd,
        Point HorizontalInnerEnd, Point InnerCorner, Point VerticalInnerEnd, double Radius, SweepDirection Sweep)
    {
        public IReadOnlyList<Point> Points => [VerticalEnd, ArcStart, ArcEnd, HorizontalEnd, HorizontalInnerEnd, InnerCorner, VerticalInnerEnd];
    }
}
