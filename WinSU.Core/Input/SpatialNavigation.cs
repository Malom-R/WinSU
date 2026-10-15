namespace WinSU.Core.Input;

public enum NavigationDirection
{
    Up,
    Down,
    Left,
    Right,
}

/// <summary>Rectangle d'un élément à l'écran (coordonnées communes à tous les candidats).</summary>
public readonly record struct Box(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;
}

/// <summary>
/// Choisit l'élément voisin dans une direction, d'après la géométrie seule : fonctionne pour les listes,
/// les grilles (y compris une dernière ligne incomplète) et les mises en page libres.
/// </summary>
public static class SpatialNavigation
{
    /// <summary>Poids de l'écart latéral : un voisin aligné est préféré à un voisin plus proche mais décalé.</summary>
    private const double SecondaryWeight = 3;

    /// <returns>L'index du meilleur candidat, ou -1 s'il n'y en a aucun dans cette direction.</returns>
    public static int FindBest(Box from, IReadOnlyList<Box> candidates, NavigationDirection direction)
    {
        var best = -1;
        var bestScore = double.MaxValue;
        var bestCenterDistance = double.MaxValue;

        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (!IsInDirection(from, candidate, direction))
                continue;

            var (primaryGap, secondaryGap, centerDistance) = Distances(from, candidate, direction);
            var score = primaryGap + SecondaryWeight * secondaryGap;
            if (score < bestScore || (score == bestScore && centerDistance < bestCenterDistance))
            {
                best = i;
                bestScore = score;
                bestCenterDistance = centerDistance;
            }
        }

        return best;
    }

    /// <summary>
    /// Navigation « en lignes » (pages de réglages, M7) : Haut et Bas mènent à la ligne suivante dans l'ordre de lecture,
    /// sans en sauter aucune, même si ses éléments sont étroits et décalés ; dans cette ligne, l'élément le plus proche
    /// horizontalement. Une ligne = les éléments qui chevauchent verticalement le premier rencontré.
    /// Gauche et Droite restent spatiales (<see cref="FindBest"/>).
    /// </summary>
    /// <returns>L'index du candidat choisi, ou -1 s'il n'y a plus de ligne dans cette direction.</returns>
    public static int FindInRows(Box from, IReadOnlyList<Box> candidates, NavigationDirection direction)
    {
        if (direction is NavigationDirection.Left or NavigationDirection.Right)
            return FindBest(from, candidates, direction);

        var down = direction == NavigationDirection.Down;
        var eligible = Enumerable.Range(0, candidates.Count)
            .Where(i => down ? candidates[i].CenterY > from.Bottom - 1 : candidates[i].CenterY < from.Y + 1)
            .ToList();
        if (eligible.Count == 0)
            return -1;

        // Premier élément rencontré dans la direction : il définit la ligne.
        var first = down
            ? eligible.MinBy(i => candidates[i].Y)
            : eligible.MaxBy(i => candidates[i].Bottom);
        var edge = candidates[first];
        return eligible
            .Where(i => candidates[i].Y < edge.Bottom && candidates[i].Bottom > edge.Y)
            .MinBy(i => Math.Abs(candidates[i].CenterX - from.CenterX));
    }

    /// <summary>Le candidat doit commencer au-delà du milieu de l'élément courant dans la direction demandée.</summary>
    private static bool IsInDirection(Box from, Box candidate, NavigationDirection direction) => direction switch
    {
        NavigationDirection.Down => candidate.Y >= from.CenterY && candidate.CenterY > from.CenterY,
        NavigationDirection.Up => candidate.Bottom <= from.CenterY && candidate.CenterY < from.CenterY,
        NavigationDirection.Right => candidate.X >= from.CenterX && candidate.CenterX > from.CenterX,
        _ => candidate.Right <= from.CenterX && candidate.CenterX < from.CenterX,
    };

    private static (double Primary, double Secondary, double CenterDistance) Distances(Box from, Box to, NavigationDirection direction)
    {
        var vertical = direction is NavigationDirection.Up or NavigationDirection.Down;
        var primary = direction switch
        {
            NavigationDirection.Down => to.Y - from.Bottom,
            NavigationDirection.Up => from.Y - to.Bottom,
            NavigationDirection.Right => to.X - from.Right,
            _ => from.X - to.Right,
        };

        var secondary = vertical
            ? Math.Max(0, Math.Max(to.X - from.Right, from.X - to.Right))
            : Math.Max(0, Math.Max(to.Y - from.Bottom, from.Y - to.Bottom));

        var centerDistance = vertical ? Math.Abs(to.CenterX - from.CenterX) : Math.Abs(to.CenterY - from.CenterY);
        return (Math.Max(0, primary), secondary, centerDistance);
    }
}
