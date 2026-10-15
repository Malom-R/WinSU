using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace WinSU.App.Controls;

/// <summary>
/// Heure (ou date) qui se met à jour seule : la seule horloge des trois styles (en-tête du Standard, accueil du Switch,
/// haut des écrans du Wii U ; D-181 : un seul code, il y en avait deux). Arrêtée pendant la veille derrière un jeu (classe
/// « resting » de la fenêtre, Themes/Controls.axaml), remise à l'heure dès qu'elle repart.
/// </summary>
public sealed class ClockText : TextBlock
{
    public static readonly StyledProperty<string> FormatProperty =
        AvaloniaProperty.Register<ClockText, string>(nameof(Format), "t");

    public static readonly StyledProperty<bool> IsTickingProperty =
        AvaloniaProperty.Register<ClockText, bool>(nameof(IsTicking), true);

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(10) };

    public ClockText() => _timer.Tick += (_, _) => Update();

    /// <summary>Format de <see cref="DateTime.ToString(string, IFormatProvider)"/> : « t » l'heure, « dddd d MMMM » la date.</summary>
    public string Format
    {
        get => GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    /// <summary>L'horloge avance (faux pendant la veille derrière un jeu).</summary>
    public bool IsTicking
    {
        get => GetValue(IsTickingProperty);
        set => SetValue(IsTickingProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(TextBlock);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Update(); // à l'heure dès l'affichage, même arrêtée
        Follow();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer.Stop();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FormatProperty || change.Property == IsTickingProperty)
            Follow();
    }

    private void Follow()
    {
        if (!IsTicking || TopLevel.GetTopLevel(this) is null)
        {
            _timer.Stop();
            return;
        }

        Update();
        _timer.Start();
    }

    private void Update() => Text = DateTime.Now.ToString(Format, CultureInfo.CurrentCulture);
}
