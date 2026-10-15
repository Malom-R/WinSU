namespace WinSU.Core.Playtime;

/// <summary>Découpage d'une durée pour l'affichage (« 2 h 05 », « 45 min », « moins d'une minute »).</summary>
public static class PlaytimeFormatter
{
    public enum Kind
    {
        LessThanAMinute,
        Minutes,
        Hours,
    }

    /// <returns>Le type d'affichage, les heures et les minutes (arrondies à la minute inférieure).</returns>
    public static (Kind Kind, int Hours, int Minutes) Split(TimeSpan duration)
    {
        var totalMinutes = (int)Math.Floor(Math.Max(0, duration.TotalMinutes));
        return totalMinutes switch
        {
            0 => (Kind.LessThanAMinute, 0, 0),
            < 60 => (Kind.Minutes, 0, totalMinutes),
            _ => (Kind.Hours, totalMinutes / 60, totalMinutes % 60),
        };
    }
}
