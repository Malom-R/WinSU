using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Skia;
using SkiaSharp;

namespace WinSU.App.Controls;

/// <summary>
/// Fond « lampe à lave » (M10, E2) : des gouttes de couleur montent, redescendent, fusionnent et se séparent
/// (mouvement : <see cref="LavaField"/>). Les formes sont des « metaballs » calculées par un shader SkSL.
/// <para>
/// Le dessin se fait dans le fil du compositeur (<see cref="CompositionCustomVisualHandler"/>) : l'animation n'occupe
/// pas le fil d'interface, et elle est limitée à <see cref="FramesPerSecond"/> images par seconde quelle que soit la
/// fréquence de l'écran. Avec un GPU, le shader est évalué directement à la taille de la fenêtre ; sans GPU (rendu
/// logiciel), il l'est dans une petite surface agrandie, ce qui ne se voit pas puisque les formes sont floues.
/// </para>
/// </summary>
public sealed class LavaLamp : Control
{
    /// <summary>L'animation avance ; à l'arrêt, la dernière image reste affichée et plus rien n'est redessiné.</summary>
    public static readonly StyledProperty<bool> IsRunningProperty =
        AvaloniaProperty.Register<LavaLamp, bool>(nameof(IsRunning), true);

    /// <summary>
    /// Couleurs des gouttes : les arrêts d'un dégradé (ou une couleur unie). Les gouttes les prennent à tour de rôle.
    /// </summary>
    public static readonly StyledProperty<IBrush?> PaletteProperty =
        AvaloniaProperty.Register<LavaLamp, IBrush?>(nameof(Palette));

    /// <summary>Cadence de l'animation (les gouttes sont lentes : 30 images par seconde suffisent).</summary>
    public const double FramesPerSecond = 30;

    private static readonly Color[] DefaultPalette =
    [
        Color.Parse("#6EC3F5"), Color.Parse("#9D8CF5"), Color.Parse("#D88CEB"), Color.Parse("#8E7BEF"),
    ];

    private CompositionCustomVisual? _visual;

    public LavaLamp() => IsHitTestVisible = false;

    public bool IsRunning
    {
        get => GetValue(IsRunningProperty);
        set => SetValue(IsRunningProperty, value);
    }

    public IBrush? Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (ElementComposition.GetElementVisual(this)?.Compositor is not { } compositor)
            return;

        _visual = compositor.CreateCustomVisual(new LavaLampRenderer());
        ElementComposition.SetElementChildVisual(this, _visual);
        _visual.Size = new Vector(Bounds.Width, Bounds.Height);
        SendState();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_visual is null)
            return;

        _visual.SendHandlerMessage(new LavaLampRenderer.State(Running: false, []));
        ElementComposition.SetElementChildVisual(this, null);
        _visual = null;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (_visual is not null)
            _visual.Size = new Vector(e.NewSize.Width, e.NewSize.Height);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsRunningProperty || change.Property == PaletteProperty)
            SendState();
    }

    private void SendState() =>
        _visual?.SendHandlerMessage(new LavaLampRenderer.State(IsRunning, PaletteColors(Palette)));

    private static Color[] PaletteColors(IBrush? palette) => palette switch
    {
        IGradientBrush { GradientStops.Count: > 0 } gradient => gradient.GradientStops.Select(stop => stop.Color).ToArray(),
        ISolidColorBrush solid => [solid.Color],
        _ => DefaultPalette,
    };
}

/// <summary>Dessin et horloge de la lampe à lave, dans le fil du compositeur.</summary>
internal sealed class LavaLampRenderer : CompositionCustomVisualHandler
{
    /// <summary>Message du contrôle : animation en cours ou non, couleurs.</summary>
    public sealed record State(bool Running, Color[] Palette);

    // Champ des metaballs : chaque goutte vaut r² / d² ; la somme dépasse le seuil à l'intérieur des gouttes, et deux
    // gouttes proches se rejoignent par un pont. La couleur est la moyenne des couleurs des gouttes, pondérée par leur
    // champ saturé f / (1 + f) : sans cette saturation, le centre d'une petite goutte fondue dans une grande faisait un
    // point de couleur vive. Autour de chaque goutte, une lueur douce. Sortie prémultipliée.
    private static readonly string Shader = $$"""
        uniform float2 size;
        uniform float4 blobs[{{LavaField.BlobCount}}];
        uniform float4 colors[{{LavaField.BlobCount}}];
        uniform float threshold;
        uniform float softness;

        half4 main(float2 p) {
            float2 q = p / size.y;
            float field = 0.0;
            float weights = 0.0;
            float3 tint = float3(0.0);
            for (int i = 0; i < {{LavaField.BlobCount}}; i++) {
                float2 d = q - blobs[i].xy;
                float r = blobs[i].z;
                float f = r * r / (dot(d, d) + 0.0004);
                float w = f / (1.0 + f);
                field += f;
                weights += w;
                tint += colors[i].rgb * w;
            }
            float3 rgb = tint / max(weights, 0.0001);
            float body = smoothstep(threshold - softness, threshold + softness, field);
            float glow = 0.3 * smoothstep(0.15, threshold, field);
            float a = max(body, glow);
            return half4(half3(rgb * a), half(a));
        }
        """;

    private const float Threshold = 1f;
    private const float Softness = 0.25f;

    /// <summary>Réduction de la surface de calcul sans GPU (1 pixel calculé pour 8 × 8 pixels affichés).</summary>
    private const int SoftwareDivisor = 8;

    /// <summary>
    /// Écart minimal entre deux images, un peu sous 1/30 s : le compositeur appelle à chaque rafraîchissement de l'écran
    /// (60, 144, 165 Hz…), et l'image dessinée est celle du premier appel après l'écart, soit près de 30 par seconde.
    /// </summary>
    private static readonly TimeSpan FrameInterval = TimeSpan.FromSeconds(0.9 / LavaLamp.FramesPerSecond);

    private static readonly Lazy<SKRuntimeEffect?> Effect = new(() =>
    {
        var effect = SKRuntimeEffect.CreateShader(Shader, out var errors);
        if (effect is null)
            Serilog.Log.Warning("Lampe à lave : shader refusé ({Errors}), fond sans gouttes", errors);
        return effect;
    });

    private readonly float[] _blobs = new float[LavaField.BlobCount * LavaField.Stride];
    private readonly float[] _colors = new float[LavaField.BlobCount * 4];
    private Color[] _palette = [];
    private bool _running;
    private TimeSpan _lastTick;
    private TimeSpan _lastFrame;
    private double _seconds;

    /// <summary>
    /// Instant de l'image demandée : un redessin partiel entre deux images (focus qui s'anime au-dessus) redessine le
    /// fond de sa zone à ce même instant, sans décalage avec le reste de l'image.
    /// </summary>
    private double _frameSeconds;
    private SKSurface? _software;
    private SKRuntimeEffectUniforms? _uniforms;
    private bool _renderReported;

    public override void OnMessage(object message)
    {
        if (message is not State state)
            return;

        if (state.Palette.Length > 0)
            _palette = state.Palette;

        var running = state.Running;
        if (running && !_running)
        {
            _lastTick = CompositionNow;
            RegisterForNextAnimationFrameUpdate();
        }

        _running = running;
        Invalidate();
    }

    public override void OnAnimationFrameUpdate()
    {
        if (!_running)
            return;

        var now = CompositionNow;
        // Après un à-coup (fenêtre déplacée, machine chargée), le mouvement reprend sans sauter.
        _seconds += Math.Min((now - _lastTick).TotalSeconds, 0.25);
        _lastTick = now;
        if (now - _lastFrame >= FrameInterval)
        {
            _lastFrame = now;
            _frameSeconds = _seconds;
            Invalidate();
        }

        RegisterForNextAnimationFrameUpdate();
    }

    public override void OnRender(ImmediateDrawingContext context)
    {
        var bounds = GetRenderBounds();
        if (Effect.Value is not { } effect || _palette.Length == 0 || bounds.Width < 1 || bounds.Height < 1)
            return;

        if (context.TryGetFeature<ISkiaSharpApiLeaseFeature>() is not { } feature)
            return;

        using var lease = feature.Lease();
        var canvas = lease.SkCanvas;
        if (!_renderReported)
        {
            _renderReported = true;
            Serilog.Log.Information("Lampe à lave : rendu {Mode}", lease.GrContext is not null ? "GPU" : "logiciel (surface réduite)");
        }

        var target = new SKRect((float)bounds.X, (float)bounds.Y, (float)bounds.Right, (float)bounds.Bottom);
        LavaField.Compute(_frameSeconds, target.Width / target.Height, _blobs);
        FillColors();

        using var paint = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(255 * lease.CurrentOpacity)) };
        if (lease.GrContext is not null)
        {
            // GPU : le shader est évalué pour chaque pixel affiché, ce qui ne coûte presque rien.
            using var shader = CreateShader(effect, target.Size);
            paint.Shader = shader;
            canvas.DrawRect(target, paint);
            return;
        }

        // Sans GPU : calcul dans une petite surface, agrandie avec un filtrage linéaire.
        var scale = Math.Max(1f, canvas.TotalMatrix.ScaleX);
        var width = Math.Max(1, (int)Math.Ceiling(target.Width * scale / SoftwareDivisor));
        var height = Math.Max(1, (int)Math.Ceiling(target.Height * scale / SoftwareDivisor));
        if (_software is null || _software.Canvas.DeviceClipBounds.Width != width || _software.Canvas.DeviceClipBounds.Height != height)
        {
            _software?.Dispose();
            _software = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        }

        using (var fill = new SKPaint())
        using (var shader = CreateShader(effect, new SKSize(width, height)))
        {
            fill.Shader = shader;
            fill.BlendMode = SKBlendMode.Src;
            _software.Canvas.DrawRect(0, 0, width, height, fill);
        }

        using var image = _software.Snapshot();
        canvas.DrawImage(image, new SKRect(0, 0, width, height), target, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None), paint);
    }

    /// <summary>Couleur de chaque goutte, prise dans la palette à tour de rôle.</summary>
    private void FillColors()
    {
        for (var i = 0; i < LavaField.BlobCount; i++)
        {
            var color = _palette[(int)_blobs[i * LavaField.Stride + 3] % _palette.Length];
            _colors[i * 4] = color.R / 255f;
            _colors[i * 4 + 1] = color.G / 255f;
            _colors[i * 4 + 2] = color.B / 255f;
            _colors[i * 4 + 3] = 1f;
        }
    }

    /// <summary>Shader pour une zone de <paramref name="size"/> dont le coin haut gauche est à l'origine.</summary>
    private SKShader CreateShader(SKRuntimeEffect effect, SKSize size)
    {
        // Mêmes uniformes d'une image à l'autre : seules les valeurs changent (le shader en copie une image).
        _uniforms ??= new SKRuntimeEffectUniforms(effect);
        _uniforms["size"] = size;
        _uniforms["blobs"] = _blobs;
        _uniforms["colors"] = _colors;
        _uniforms["threshold"] = Threshold;
        _uniforms["softness"] = Softness;
        return effect.ToShader(_uniforms);
    }
}
