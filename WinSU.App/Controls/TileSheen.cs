using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using WinSU.Core.Settings;

namespace WinSU.App.Controls;

/// <summary>
/// Balayage de lumière d'une tuile (étape 29, D-90) : quand la tuile prend le focus (<see cref="Play"/>, posé par les styles
/// de Themes/Controls.axaml), une bande de lumière oblique traverse sa face de gauche à droite, par-dessus l'icône (le
/// reflet de la surface vitrée), puis la tuile redevient fixe. Durée tirée des réglages (<see cref="DurationFor"/> :
/// activé ou non, vitesse), appliquée par <see cref="Services.ThemeService"/> dans la ressource
/// <see cref="DurationResource"/>. Hors de ]0, 1[, <see cref="Progress"/> ne dessine rien : aucun coût au repos.
/// </summary>
public sealed class TileSheen : Control
{
    /// <summary>Largeur de la bande, rapportée à la largeur de la face.</summary>
    public const double BandWidth = 0.38;

    /// <summary>Inclinaison de la bande : décalage horizontal de son haut par rapport à son bas, rapporté à la hauteur.</summary>
    public const double Slant = 0.45;

    /// <summary>Part de la durée passée à attendre, pendant que la tuile grossit, avant que la bande parte.</summary>
    public const double Wait = 0.15;

    /// <summary>Ressource de l'application qui porte la durée du balayage (zéro : balayage désactivé).</summary>
    public const string DurationResource = "WinSU.TileSheenDuration";

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<TileSheen, CornerRadius>(nameof(CornerRadius), TileMetrics.FaceCornerRadius);

    public static readonly StyledProperty<IBrush?> BrushProperty =
        AvaloniaProperty.Register<TileSheen, IBrush?>(nameof(Brush));

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<TileSheen, double>(nameof(Progress));

    public static readonly StyledProperty<bool> PlayProperty =
        AvaloniaProperty.Register<TileSheen, bool>(nameof(Play));

    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<TileSheen, TimeSpan>(nameof(Duration), Motion.Sheen);

    private CancellationTokenSource? _sweep;

    static TileSheen()
    {
        AffectsRender<TileSheen>(CornerRadiusProperty, BrushProperty, ProgressProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<TileSheen>(false);
    }

    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>Remplissage de la bande (dégradé en travers : transparent, lumière, transparent).</summary>
    public IBrush? Brush
    {
        get => GetValue(BrushProperty);
        set => SetValue(BrushProperty, value);
    }

    /// <summary>Avancée du balayage : 0, la bande est encore à gauche de la face ; 1, elle en est sortie à droite.</summary>
    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <summary>La tuile a le focus : passer à vrai lance un balayage ; à faux, l'arrête.</summary>
    public bool Play
    {
        get => GetValue(PlayProperty);
        set => SetValue(PlayProperty, value);
    }

    /// <summary>Durée d'un balayage ; zéro : pas de balayage (réglage coupé).</summary>
    public TimeSpan Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    /// <summary>Durée d'un balayage d'après les réglages : zéro s'il est coupé ; sinon la durée normale divisée par la vitesse.</summary>
    public static TimeSpan DurationFor(bool enabled, double speed) =>
        enabled
            ? Motion.Sheen / Math.Clamp(double.IsFinite(speed) ? speed : 1, AppearanceSettings.MinTileSheenSpeed, AppearanceSettings.MaxTileSheenSpeed)
            : TimeSpan.Zero;

    /// <summary>
    /// Bord gauche du bas de la bande, rapporté à la largeur de la face : de −(largeur + inclinaison), bande entièrement à
    /// gauche, à 1, bande entièrement à droite.
    /// </summary>
    public static double BandLeft(double progress, double aspect = 1)
    {
        var start = -(BandWidth + (Slant * aspect));
        return start + ((1 - start) * progress);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PlayProperty)
        {
            if (Play)
                _ = SweepAsync();
            else
                StopSweep();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        StopSweep();
        base.OnDetachedFromVisualTree(e);
    }

    private async Task SweepAsync()
    {
        StopSweep();
        if (Duration <= TimeSpan.Zero)
            return;

        var sweep = _sweep = new CancellationTokenSource();
        var animation = new Animation
        {
            Duration = Duration,
            Easing = Motion.Soft,
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(ProgressProperty, 0d) } },
                new KeyFrame { Cue = new Cue(Wait), Setters = { new Setter(ProgressProperty, 0d) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(ProgressProperty, 1d) } },
            },
        };
        try
        {
            await animation.RunAsync(this, sweep.Token);
        }
        catch (OperationCanceledException)
        {
            // Le focus est parti avant la fin : la bande disparaît (StopSweep).
        }
    }

    private void StopSweep()
    {
        if (_sweep is { } sweep)
        {
            _sweep = null;
            sweep.Cancel();
        }

        Progress = 0;
    }

    public override void Render(DrawingContext context)
    {
        var progress = Progress;
        if (Brush is not { } brush || progress <= 0 || progress >= 1 || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        var face = new Rect(Bounds.Size);
        var aspect = face.Height / face.Width;
        var left = BandLeft(progress, aspect) * face.Width;
        var slant = Slant * face.Height;
        // Un rectangle droit incliné : x' = x + slant × (1 − y / hauteur). Le dégradé en travers s'incline avec lui.
        var skew = new Matrix(1, 0, -slant / face.Height, 1, slant, 0);
        using (context.PushClip(new RoundedRect(face, CornerRadius.TopLeft)))
        using (context.PushTransform(skew))
            context.DrawRectangle(brush, null, new Rect(left, 0, BandWidth * face.Width, face.Height));
    }
}
