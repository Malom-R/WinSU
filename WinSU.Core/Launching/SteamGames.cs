using System.Globalization;
using System.Text.RegularExpressions;

namespace WinSU.Core.Launching;

/// <summary>Reconnaît un jeu Steam dans une adresse ou une ligne de commande (étape 35, D-177).</summary>
public static partial class SteamGames
{
    [GeneratedRegex(@"steam://(?:rungameid|run)/(\d+)|-applaunch\s+(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AppIdPattern();

    /// <summary>
    /// Le numéro du jeu Steam : <c>steam://rungameid/570</c>, <c>steam://run/570</c>, <c>-applaunch 570</c>. Un raccourci
    /// Steam vers un jeu hors de Steam a un numéro de 64 bits : il n'est pas reconnu (Steam ne le dit pas « en cours »).
    /// </summary>
    public static bool TryGetAppId(string? text, out uint appId)
    {
        appId = 0;
        if (string.IsNullOrEmpty(text) || AppIdPattern().Match(text) is not { Success: true } match)
            return false;

        var digits = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        return uint.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out appId) && appId > 0;
    }
}
