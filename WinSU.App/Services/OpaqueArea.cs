namespace WinSU.App.Services;

/// <summary>
/// Partie visible d'une image à fond transparent (logo du jeu, étape 33) : le plus petit rectangle qui contient tous ses
/// pixels non transparents. Un logo est centré et agrandi d'après ce rectangle, pas d'après ses marges transparentes, qui
/// ne sont pas toujours égales (demande de l'utilisateur : le logo « vraiment » au centre).
/// </summary>
public static class OpaqueArea
{
    /// <summary>Alpha (0 à 255) en dessous duquel un pixel compte comme transparent (une ombre presque invisible ne compte pas).</summary>
    public const byte Threshold = 8;

    /// <summary>
    /// Le rectangle visible d'une image BGRA ou RGBA (alpha au 4ᵉ octet de chaque pixel) de <paramref name="width"/> ×
    /// <paramref name="height"/> pixels, <paramref name="rowBytes"/> octets par ligne ; null si elle est toute transparente.
    /// </summary>
    public static (int X, int Y, int Width, int Height)? Find(ReadOnlySpan<byte> pixels, int width, int height, int rowBytes)
    {
        var (left, top, right, bottom) = (width, height, -1, -1);
        for (var y = 0; y < height; y++)
        {
            var row = pixels.Slice(y * rowBytes, width * 4);
            for (var x = 0; x < width; x++)
            {
                if (row[(x * 4) + 3] < Threshold)
                    continue;

                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = y;
            }
        }

        return right < 0 ? null : (left, top, right - left + 1, bottom - top + 1);
    }
}
