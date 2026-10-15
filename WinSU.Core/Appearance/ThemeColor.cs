using System.Globalization;

namespace WinSU.Core.Appearance;

/// <summary>
/// Une couleur (M14) : rouge, vert, bleu et opacité, sans dépendance à l'interface. Conversions hexadécimal, TSV (carré
/// du sélecteur) et TSL (générateur de palette), luminance et contraste au sens des WCAG 2, mélange.
/// </summary>
public readonly record struct ThemeColor(byte R, byte G, byte B, byte A = 255)
{
    public static readonly ThemeColor White = new(255, 255, 255);
    public static readonly ThemeColor Black = new(0, 0, 0);

    /// <summary>« #RRGGBB », « RRGGBB » ou « #AARRGGBB » (espaces tolérés autour).</summary>
    public static bool TryParse(string? text, out ThemeColor color)
    {
        color = default;
        var hex = text?.Trim().TrimStart('#');
        if (hex is null || (hex.Length != 6 && hex.Length != 8)
            || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        color = hex.Length == 6
            ? new ThemeColor((byte)(value >> 16), (byte)(value >> 8), (byte)value)
            : new ThemeColor((byte)(value >> 16), (byte)(value >> 8), (byte)value, (byte)(value >> 24));
        return true;
    }

    /// <exception cref="FormatException">Le texte n'est pas une couleur hexadécimale.</exception>
    public static ThemeColor Parse(string text) =>
        TryParse(text, out var color) ? color : throw new FormatException($"Couleur invalide : {text}");

    /// <summary>« #RRGGBB », ou « #AARRGGBB » si la couleur n'est pas opaque.</summary>
    public string ToHex() => A == 255 ? $"#{R:X2}{G:X2}{B:X2}" : $"#{A:X2}{R:X2}{G:X2}{B:X2}";

    public override string ToString() => ToHex();

    /// <summary>Même couleur avec une opacité de 0 à 1.</summary>
    public ThemeColor WithAlpha(double alpha) => this with { A = ToByte(alpha * 255) };

    /// <summary>Teinte en degrés (0–360), saturation et valeur de 0 à 1.</summary>
    public static ThemeColor FromHsv(double hue, double saturation, double value)
    {
        saturation = Math.Clamp(saturation, 0, 1);
        value = Math.Clamp(value, 0, 1);
        var chroma = value * saturation;
        return FromChroma(hue, chroma, value - chroma);
    }

    public (double Hue, double Saturation, double Value) ToHsv()
    {
        var (max, min, hue) = HueOf();
        return (hue, max == 0 ? 0 : (max - min) / max, max);
    }

    /// <summary>Teinte en degrés (0–360), saturation et luminosité de 0 à 1.</summary>
    public static ThemeColor FromHsl(double hue, double saturation, double lightness)
    {
        saturation = Math.Clamp(saturation, 0, 1);
        lightness = Math.Clamp(lightness, 0, 1);
        var chroma = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        return FromChroma(hue, chroma, lightness - chroma / 2);
    }

    public (double Hue, double Saturation, double Lightness) ToHsl()
    {
        var (max, min, hue) = HueOf();
        var lightness = (max + min) / 2;
        var saturation = max == min ? 0 : (max - min) / (1 - Math.Abs(2 * lightness - 1));
        return (hue, Math.Clamp(saturation, 0, 1), lightness);
    }

    /// <summary>Luminance relative (WCAG 2), de 0 (noir) à 1 (blanc) ; l'opacité est ignorée.</summary>
    public double Luminance => 0.2126 * Linear(R) + 0.7152 * Linear(G) + 0.0722 * Linear(B);

    /// <summary>Rapport de contraste (WCAG 2), de 1 à 21. Texte courant lisible : au moins 4,5.</summary>
    public static double Contrast(ThemeColor first, ThemeColor second)
    {
        var (light, dark) = first.Luminance >= second.Luminance ? (first, second) : (second, first);
        return (light.Luminance + 0.05) / (dark.Luminance + 0.05);
    }

    /// <summary>Mélange avec <paramref name="other"/> : 0 = cette couleur, 1 = l'autre (opacités mélangées aussi).</summary>
    public ThemeColor Mix(ThemeColor other, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return new ThemeColor(Lerp(R, other.R, amount), Lerp(G, other.G, amount), Lerp(B, other.B, amount), Lerp(A, other.A, amount));
    }

    public ThemeColor Lighten(double amount) => Mix(White with { A = A }, amount);

    public ThemeColor Darken(double amount) => Mix(Black with { A = A }, amount);

    private static ThemeColor FromChroma(double hue, double chroma, double offset)
    {
        var sector = (((hue % 360) + 360) % 360) / 60;
        var x = chroma * (1 - Math.Abs(sector % 2 - 1));
        var (r, g, b) = (int)sector switch
        {
            0 => (chroma, x, 0.0),
            1 => (x, chroma, 0.0),
            2 => (0.0, chroma, x),
            3 => (0.0, x, chroma),
            4 => (x, 0.0, chroma),
            _ => (chroma, 0.0, x),
        };
        return new ThemeColor(ToByte((r + offset) * 255), ToByte((g + offset) * 255), ToByte((b + offset) * 255));
    }

    /// <summary>Maximum et minimum des composantes (0 à 1) et teinte en degrés.</summary>
    private (double Max, double Min, double Hue) HueOf()
    {
        double r = R / 255.0, g = G / 255.0, b = B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        double hue;
        if (delta == 0)
            hue = 0;
        else if (max == r)
            hue = 60 * (((g - b) / delta) % 6);
        else if (max == g)
            hue = 60 * ((b - r) / delta + 2);
        else
            hue = 60 * ((r - g) / delta + 4);
        return (max, min, (hue + 360) % 360);
    }

    private static double Linear(byte component)
    {
        var c = component / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    private static byte Lerp(byte from, byte to, double amount) => ToByte(from + (to - from) * amount);

    private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);
}
