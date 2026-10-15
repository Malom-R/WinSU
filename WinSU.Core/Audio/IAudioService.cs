namespace WinSU.Core.Audio;

/// <summary>Sons d'interface de WinSU (fichiers : Infrastructure/Audio/UiSoundPlayer.cs).</summary>
public enum UiSound
{
    /// <summary>Déplacement du focus.</summary>
    Navigate,

    /// <summary>Ouverture, validation.</summary>
    Open,

    /// <summary>Fermeture, retour.</summary>
    Close,

    /// <summary>Ouverture d'un menu contextuel (menu Options).</summary>
    ContextMenu,

    /// <summary>Entrée dans une console.</summary>
    EnterConsole,

    /// <summary>Sortie d'une console.</summary>
    ExitConsole,

    /// <summary>Lancement d'un jeu, à partir de M4.</summary>
    LaunchGame,
}

public interface IAudioService
{
    /// <summary>Joue un son d'interface, sans bloquer. Ne fait rien si les sons sont désactivés ou indisponibles.</summary>
    void Play(UiSound sound);
}
