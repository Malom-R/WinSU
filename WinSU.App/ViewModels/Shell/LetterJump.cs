using System.Globalization;
using System.Text;

namespace WinSU.App.ViewModels.Shell;

/// <summary>
/// Saut de groupe en groupe dans une liste triée : d'initiale en initiale (<see cref="Find"/>), ou de fabricant en fabricant
/// (<see cref="FindGroup"/>, catalogue des consoles, étape 34).
/// </summary>
public static class LetterJump
{
    /// <summary>
    /// <paramref name="direction"/> &gt; 0 : premier élément de l'initiale suivante.
    /// <paramref name="direction"/> &lt; 0 : début de l'initiale courante, ou de la précédente si on y est déjà.
    /// Renvoie <paramref name="current"/> s'il n'y a nulle part où aller.
    /// </summary>
    public static int Find(IReadOnlyList<string> sortedNames, int current, int direction) =>
        FindGroup([.. sortedNames.Select(name => KeyOf(name).ToString())], current, direction);

    /// <summary>
    /// Même saut, les groupes étant donnés tels quels (<paramref name="groups"/> : le groupe de chaque élément, dans l'ordre
    /// de la liste ; deux éléments voisins du même groupe sont dans le même bloc).
    /// </summary>
    public static int FindGroup(IReadOnlyList<string> groups, int current, int direction)
    {
        if (groups.Count == 0)
            return -1;

        current = Math.Clamp(current, 0, groups.Count - 1);
        if (direction > 0)
        {
            for (var i = current + 1; i < groups.Count; i++)
            {
                if (groups[i] != groups[current])
                    return i;
            }

            return current;
        }

        var start = StartOfGroup(groups, current);
        return start < current || start == 0 ? start : StartOfGroup(groups, start - 1);
    }

    /// <summary>Initiale sans accent, en majuscule ; tous les chiffres et symboles forment le groupe « # ».</summary>
    public static char KeyOf(string name)
    {
        var first = name.TrimStart().FirstOrDefault();
        var letter = first.ToString().Normalize(NormalizationForm.FormD).FirstOrDefault(c =>
            CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);
        return char.IsLetter(letter) ? char.ToUpperInvariant(letter) : '#';
    }

    private static int StartOfGroup(IReadOnlyList<string> groups, int index)
    {
        while (index > 0 && groups[index - 1] == groups[index])
            index--;
        return index;
    }
}
