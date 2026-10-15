using Avalonia;

namespace WinSU.App.Controls;

/// <summary>
/// Mise à l'échelle de toute l'interface (étape 25, D-73) : elle est dessinée comme dans une fenêtre de référence, un
/// rectangle d'or (<see cref="ShortSide"/> × <see cref="ShortSide"/>·φ), couché ou debout comme la fenêtre, puis agrandie
/// ou réduite pour la remplir ; ses deux côtés tiennent toujours dans la fenêtre, et la place en plus va aux grilles
/// (colonnes, rangées, pages). Le petit côté de référence est la hauteur utile de la fenêtre agrandie de l'utilisateur
/// (2560 × 1600 à 200 %, zone utile de 1280 × 730) : chez lui, l'échelle vaut 1 et l'interface validée ne bouge pas.
/// </summary>
public static class UiScale
{
    /// <summary>Nombre d'or : proportions de la fenêtre de référence et des grands partages de l'écran.</summary>
    public const double Phi = 1.6180339887498949;

    /// <summary>Petit côté de la fenêtre de référence.</summary>
    public const double ShortSide = 730;

    /// <summary>Grand côté de la fenêtre de référence : petit côté × φ.</summary>
    public const double LongSide = ShortSide * Phi;

    /// <summary>Plus petite échelle : en deçà, les textes ne se liraient plus.</summary>
    public const double Minimum = 0.5;

    /// <summary>Fenêtre plus haute que large : les écrans se réorganisent en hauteur (classe « portrait » de la fenêtre).</summary>
    public static bool IsPortrait(Size window) => window.Height > window.Width;

    /// <summary>Échelle de l'interface pour une fenêtre : la plus grande qui y fait tenir la fenêtre de référence.</summary>
    public static double For(Size window)
    {
        if (window.Width <= 0 || window.Height <= 0)
            return 1;

        var (shortSide, longSide) = IsPortrait(window) ? (window.Width, window.Height) : (window.Height, window.Width);
        return Math.Max(Minimum, Math.Min(shortSide / ShortSide, longSide / LongSide));
    }
}
