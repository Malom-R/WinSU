using System.Globalization;
using System.Text;

namespace WinSU.Core.Media;

/// <summary>
/// Clé de comparaison d'un nom de jeu (notes 3.7e) : « Super Mario Bros. 3 (USA) [!] » et « super mario bros 3 »
/// donnent la même clé. On retire les étiquettes entre parenthèses ou crochets (région, version, disque), les accents
/// des lettres latines, la ponctuation et les espaces, et on met en minuscules. Ainsi « Zelda: Ocarina » (nom ES-DE)
/// et « Zelda - Ocarina » (nom de fichier, où « : » est interdit) se rejoignent. Les signes des autres écritures
/// sont gardés : sans son dakuten, « ゼ » deviendrait « セ », une autre syllabe.
/// </summary>
public static class GameNameKey
{
    /// <summary>Fin du latin étendu B : au-delà, les signes diacritiques font partie de la lettre.</summary>
    private const char LastLatinLetter = (char)0x024F;

    /// <summary>La clé, ou une chaîne vide si le nom ne contient ni lettre ni chiffre hors étiquettes.</summary>
    public static string From(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var decomposed = name.Normalize(NormalizationForm.FormD);
        var key = new StringBuilder(decomposed.Length);
        var depth = 0;
        foreach (var c in decomposed)
        {
            switch (c)
            {
                case '(' or '[':
                    depth++;
                    continue;
                case ')' or ']':
                    depth = Math.Max(0, depth - 1);
                    continue;
            }

            if (depth > 0)
                continue;
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                if (key.Length > 0 && key[^1] > LastLatinLetter)
                    key.Append(c);
            }
            else if (char.IsLetterOrDigit(c))
            {
                key.Append(char.ToLowerInvariant(c));
            }
        }

        return key.ToString().Normalize(NormalizationForm.FormC);
    }
}
