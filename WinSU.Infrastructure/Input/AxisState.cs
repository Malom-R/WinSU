namespace WinSU.Infrastructure.Input;

/// <summary>
/// État « poussé » d'un axe dans chaque sens, mesuré par rapport à sa position de repos
/// (0 pour un stick, −32768 pour beaucoup de gâchettes), avec hystérésis pour éviter les rebonds.
/// </summary>
internal struct AxisState
{
    /// <summary>On relâche en dessous de ce pourcentage du seuil d'appui.</summary>
    public const double ReleaseRatio = 0.6;

    public short Rest;
    public bool Positive;
    public bool Negative;

    /// <summary>Met à jour l'état et indique quels sens ont changé.</summary>
    public (bool PositiveChanged, bool NegativeChanged) Update(short value, double threshold)
    {
        var pressAt = threshold * short.MaxValue;
        var releaseAt = pressAt * ReleaseRatio;
        var delta = value - (double)Rest;

        var positive = Positive ? delta > releaseAt : delta > pressAt;
        var negative = Negative ? -delta > releaseAt : -delta > pressAt;
        var changed = (positive != Positive, negative != Negative);
        Positive = positive;
        Negative = negative;
        return changed;
    }
}
