namespace WinSU.Core.Power;

public enum PowerAction
{
    Sleep,
    Restart,
    Shutdown,
}

/// <summary>Veille, redémarrage et arrêt de Windows (D-18).</summary>
public interface IPowerService
{
    /// <summary>Demande l'action à Windows. Lève une exception si Windows la refuse.</summary>
    void Execute(PowerAction action);
}
