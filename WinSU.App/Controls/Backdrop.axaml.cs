using Avalonia;
using Avalonia.Controls;

namespace WinSU.App.Controls;

/// <summary>Fond de toutes les pages : couleur, lampe à lave et motif du thème (voir Backdrop.axaml).</summary>
public partial class Backdrop : UserControl
{
    /// <summary>Les gouttes de la lampe à lave avancent (décidé par <see cref="Services.BackdropDirector"/>).</summary>
    public static readonly StyledProperty<bool> IsRunningProperty =
        AvaloniaProperty.Register<Backdrop, bool>(nameof(IsRunning), true);

    public Backdrop() => InitializeComponent();

    public bool IsRunning
    {
        get => GetValue(IsRunningProperty);
        set => SetValue(IsRunningProperty, value);
    }
}
