using CommunityToolkit.Mvvm.ComponentModel;
using WinSU.Core.Appearance;
using WinSU.Core.Input;

namespace WinSU.App.ViewModels;

/// <summary>
/// Sélecteur de couleur de l'éditeur de thèmes (M14, D-36) : carré saturation (horizontale) / luminosité (verticale),
/// barre de teinte, code hexadécimal. Réglé par les actions (D-37) : directions = curseur du carré, onglet précédent /
/// suivant = teinte ; il suit donc la configuration de touches de l'utilisateur (M11). À la souris (demande de
/// l'utilisateur) : clic ou glissé dans le carré et sur la barre, molette pour la teinte. La vue place les curseurs
/// d'après <see cref="CursorX"/>, <see cref="CursorY"/> et <see cref="HueX"/>.
/// </summary>
public sealed partial class ColorPickerViewModel : ObservableObject
{
    public const double SquareWidth = 320;
    public const double SquareHeight = 200;
    public const double CursorRadius = 7;
    public const double HueCursorHalfWidth = 3;

    /// <summary>Pas d'un appui (les directions et les onglets se répètent quand on les maintient).</summary>
    public const double SaturationStep = 0.02;
    public const double ValueStep = 0.02;
    public const double HueStep = 5;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Color), nameof(HueColor), nameof(HueX), nameof(Hex))]
    public partial double Hue { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Color), nameof(CursorX), nameof(Hex))]
    public partial double Saturation { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Color), nameof(CursorY), nameof(Hex))]
    public partial double Value { get; set; }

    public ThemeColor Color => ThemeColor.FromHsv(Hue, Saturation, Value);

    /// <summary>Teinte pure : le fond du carré.</summary>
    public ThemeColor HueColor => ThemeColor.FromHsv(Hue, 1, 1);

    public string Hex => Color.ToHex();

    public double CursorX => Saturation * SquareWidth - CursorRadius;

    public double CursorY => (1 - Value) * SquareHeight - CursorRadius;

    public double HueX => Hue / 360 * SquareWidth - HueCursorHalfWidth;

    /// <summary>Après chaque réglage (pour l'aperçu en direct).</summary>
    public event EventHandler? ColorChanged;

    /// <summary>Place les curseurs sur une couleur. Une couleur grise n'a pas de teinte : on garde la teinte actuelle.</summary>
    public void Load(ThemeColor color)
    {
        var (hue, saturation, value) = color.ToHsv();
        if (saturation > 0 && value > 0)
            Hue = hue;
        Saturation = saturation;
        Value = value;
    }

    /// <summary>Souris (clic ou glissé dans le carré) : saturation et luminosité au point donné, en DIP depuis le coin du carré.</summary>
    public void PointSquare(double x, double y)
    {
        Saturation = Math.Clamp(x / SquareWidth, 0, 1);
        Value = Math.Clamp(1 - y / SquareHeight, 0, 1);
        ColorChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Souris (clic ou glissé sur la barre) : teinte au point donné, en DIP depuis le bord gauche de la barre.</summary>
    public void PointHue(double x)
    {
        Hue = Math.Clamp(x / SquareWidth, 0, 1) * 359.9; // 360° = 0° : on reste sous le bord droit
        ColorChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Molette sur le sélecteur : un cran de teinte par cran de molette (vers le haut : teinte suivante).</summary>
    public void WheelHue(double delta) => Handle(delta > 0 ? InputAction.PageNext : InputAction.PagePrevious);

    /// <summary>Applique une action ; false si elle ne règle rien (Valider, Retour, Menu restent à l'appelant).</summary>
    public bool Handle(InputAction action)
    {
        switch (action)
        {
            case InputAction.Left: Saturation = Math.Max(0, Saturation - SaturationStep); break;
            case InputAction.Right: Saturation = Math.Min(1, Saturation + SaturationStep); break;
            case InputAction.Up: Value = Math.Min(1, Value + ValueStep); break;
            case InputAction.Down: Value = Math.Max(0, Value - ValueStep); break;
            case InputAction.PagePrevious: Hue = (Hue - HueStep + 360) % 360; break;
            case InputAction.PageNext: Hue = (Hue + HueStep) % 360; break;
            default: return false;
        }

        ColorChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
