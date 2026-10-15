using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WinSU.App.Controls;

/// <summary>
/// Logo du jeu choisi (étape 33, D-140, maquettes Sprite-0010 et 0011 de l'utilisateur) : l'image entière, le plus grande
/// possible dans sa place (Themes/Controls.axaml : 320 × 190 au plus), en fondu enchaîné quand il change, comme la bannière
/// héro (<see cref="HeroBanner"/>, <see cref="HeroBannerFit.Contain"/>). Modulaire : une vue qui veut le logo place ce
/// contrôle et le lie au logo de son écran (Standard : en bas à gauche, Switch : en haut au milieu ; Themes/Standard.axaml,
/// Themes/Switch.axaml) ; une vue qui ne le place pas (Wii U) n'en montre aucun.
/// </summary>
public sealed class GameLogo : Decorator
{
    public static readonly StyledProperty<IImage?> SourceProperty =
        AvaloniaProperty.Register<GameLogo, IImage?>(nameof(Source));

    private readonly HeroBanner _image = new() { Fit = HeroBannerFit.Contain };

    public GameLogo()
    {
        Child = _image;
        IsHitTestVisible = false;
    }

    public IImage? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty)
            _image.Source = change.GetNewValue<IImage?>();
    }
}
