using CommunityToolkit.Mvvm.ComponentModel;
using WinSU.Core.Settings;

namespace WinSU.App.Services;

/// <summary>
/// Décide quand le fond animé (lampe à lave, M10) avance : réglage « Fond animé » activé (par défaut) et utilisateur
/// dans WinSU (<see cref="ActiveUse"/> : arrêt hors premier plan, fenêtre réduite, pendant
/// un jeu et après une mise en veille). À l'arrêt, la dernière image reste affichée et ne coûte plus rien.
/// </summary>
public sealed partial class BackdropDirector : ObservableObject
{
    private readonly ActiveUse _use;
    private readonly ISettingsStore _settings;

    public BackdropDirector(ActiveUse use, ISettingsStore settings)
    {
        _use = use;
        _settings = settings;
        use.Changed += (_, _) => UiThread.Run(Update);
        settings.Changed += (_, e) =>
        {
            if (e.Previous.Appearance.AnimatedBackground != e.Current.Appearance.AnimatedBackground)
                UiThread.Run(Update);
        };
        Update();
    }

    /// <summary>La lampe à lave avance.</summary>
    [ObservableProperty]
    public partial bool IsRunning { get; private set; }

    private void Update() => IsRunning = _settings.Current.Appearance.AnimatedBackground && _use.IsActive;
}
