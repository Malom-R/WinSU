using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace WinSU.App.Controls;

/// <summary>Façon de montrer une bannière héro dans sa zone (choisie par la vue de chaque style).</summary>
public enum HeroBannerFit
{
    /// <summary>
    /// L'image couvre la zone : agrandie depuis son centre jusqu'à la remplir, ce qui dépasse est coupé (Standard, Wii U).
    /// </summary>
    Fill,

    /// <summary>
    /// L'image entière, sur toute la largeur, au milieu de la zone ; pleine sur sa ligne du milieu, elle s'efface vers le
    /// fond de l'application jusqu'à ses bords haut et bas (Switch, étape 28, D-83) : rien n'est coupé.
    /// </summary>
    Whole,

    /// <summary>
    /// L'image couvre la zone, sa fin collée au bord droit : c'est son début qui est coupé, à gauche (Standard, 30.13,
    /// demande de l'utilisateur : « l'extrémité droite » de la bannière se voit ; la gauche passe sous le fondu).
    /// </summary>
    FillRight,

    /// <summary>
    /// L'image entière, le plus grande possible dans la zone et au milieu, sans fondu (logo du jeu, étape 33 :
    /// <see cref="GameLogo"/>).
    /// </summary>
    Contain,
}

/// <summary>
/// Zone de bannière héro (<c>banner_top</c>, M9) : l'image apparaît en fondu ; quand elle change, l'ancienne s'efface
/// pendant que la nouvelle apparaît (fondu enchaîné) ; sans image, la zone se vide en fondu. <see cref="Fit"/> dit si
/// l'image remplit la zone (rognée) ou s'y montre entière. La place, les marges, les coins et l'opacité d'ensemble (40 %
/// en Standard et en Wii U ; en Switch, aucune : l'image entière porte seule son dégradé) sont choisis par la vue de
/// chaque style.
/// </summary>
public sealed class HeroBanner : Panel
{
    public static readonly StyledProperty<IImage?> SourceProperty =
        AvaloniaProperty.Register<HeroBanner, IImage?>(nameof(Source));

    public static readonly StyledProperty<HeroBannerFit> FitProperty =
        AvaloniaProperty.Register<HeroBanner, HeroBannerFit>(nameof(Fit));

    /// <summary>
    /// Image entière : part de sa hauteur fondue vers le fond, en haut comme en bas. 0,5 (retour de l'utilisateur, étape
    /// 28) : pleine seulement sur sa ligne du milieu, puis de moins en moins jusqu'à 0 % à ses bords ; 0,25 : moitié du
    /// milieu pleine.
    /// </summary>
    public const double WholeFade = 0.5;

    private Image _shown;
    private Image _spare;

    public HeroBanner()
    {
        _spare = CreateLayer();
        _shown = CreateLayer();
        Children.Add(_spare);
        Children.Add(_shown);
        ClipToBounds = true;
        IsHitTestVisible = false;
        ApplyFit();
    }

    public IImage? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public HeroBannerFit Fit
    {
        get => GetValue(FitProperty);
        set => SetValue(FitProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty)
            Show(change.GetNewValue<IImage?>());
        else if (change.Property == FitProperty)
            ApplyFit();
    }

    private void Show(IImage? source)
    {
        _shown.Opacity = 0; // l'image affichée s'efface
        if (source is null)
            return;

        // La nouvelle image apparaît sur l'autre calque, au-dessus.
        (_shown, _spare) = (_spare, _shown);
        _shown.Source = source;
        _shown.ZIndex = 1;
        _spare.ZIndex = 0;
        _shown.Opacity = 1;
    }

    private void ApplyFit()
    {
        foreach (var layer in new[] { _shown, _spare })
        {
            var whole = Fit == HeroBannerFit.Whole;
            var right = Fit == HeroBannerFit.FillRight;
            // Calée à droite : chaque calque reçoit sa place (ArrangeOverride) et la remplit
            layer.Stretch = whole || Fit == HeroBannerFit.Contain ? Stretch.Uniform : right ? Stretch.Fill : Stretch.UniformToFill;
            layer.HorizontalAlignment = right ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
            layer.VerticalAlignment = right ? VerticalAlignment.Stretch : VerticalAlignment.Center;
            layer.OpacityMask = whole ? WholeMask() : null;
        }

        InvalidateArrange();
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Fit != HeroBannerFit.FillRight)
            return base.ArrangeOverride(finalSize);

        foreach (var layer in new[] { _shown, _spare })
            layer.Arrange(FillRightRect(layer.Source?.Size, finalSize));
        return finalSize;
    }

    /// <summary>
    /// Place de l'image calée à droite (<see cref="HeroBannerFit.FillRight"/>) : agrandie jusqu'à couvrir la zone, son bord
    /// droit sur celui de la zone, centrée en hauteur ; ce qui dépasse (à gauche, ou en haut et en bas) est coupé.
    /// </summary>
    public static Rect FillRightRect(Size? source, Size zone)
    {
        if (source is not { Width: > 0, Height: > 0 } image)
            return new Rect(zone);

        var scale = Math.Max(zone.Width / image.Width, zone.Height / image.Height);
        var size = new Size(image.Width * scale, image.Height * scale);
        return new Rect(zone.Width - size.Width, (zone.Height - size.Height) / 2, size.Width, size.Height);
    }

    /// <summary>Masque de l'image entière : transparent aux bords haut et bas, plein au milieu (voir <see cref="WholeFade"/>).</summary>
    private static LinearGradientBrush WholeMask() => new()
    {
        StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
        GradientStops =
        [
            new GradientStop(Colors.Transparent, 0),
            new GradientStop(Colors.Black, WholeFade),
            new GradientStop(Colors.Black, 1 - WholeFade),
            new GradientStop(Colors.Transparent, 1),
        ],
    };

    private static Image CreateLayer()
    {
        // Centrée : en remplissage, l'image est agrandie depuis son milieu ; entière, elle est au milieu de la zone.
        var layer = new Image
        {
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0,
            Transitions =
            [
                new DoubleTransition { Property = OpacityProperty, Duration = Motion.HeroFade, Easing = Motion.Soft },
            ],
        };
        RenderOptions.SetBitmapInterpolationMode(layer, BitmapInterpolationMode.HighQuality);
        return layer;
    }
}
