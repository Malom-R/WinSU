namespace WinSU.App.Services;

/// <summary>
/// « L'utilisateur se sert de WinSU » : la fenêtre est au premier plan (donc ni réduite ni derrière une autre, Alt+Tab
/// compris) et aucun jeu ni aucune application lancé depuis WinSU n'est en cours. Après une mise en veille demandée par
/// WinSU, plus rien jusqu'au prochain appui ou au prochain retour dans WinSU (écran éteint, la fenêtre reste active).
/// Règle du fond animé (M10, <see cref="BackdropDirector"/>).
/// </summary>
public sealed class ActiveUse(IWindowControl window, LaunchCoordinator launcher)
{
    private bool _started;
    private bool _heldUntilInput;

    /// <summary>L'utilisateur se sert de WinSU (faux tant que <see cref="Start"/> n'a pas été appelé).</summary>
    public bool IsActive { get; private set; }

    /// <summary><see cref="IsActive"/> a changé.</summary>
    public event EventHandler? Changed;

    /// <summary>Commence à suivre la fenêtre et les lancements (sans effet la deuxième fois).</summary>
    public void Start()
    {
        if (_started)
            return;

        _started = true;
        window.ActivationChanged += (_, _) => Release();
        launcher.RunningChanged += (_, _) => Update();
        Update();
    }

    /// <summary>Mise en veille : plus rien jusqu'à la prochaine touche, le prochain bouton ou le prochain retour dans WinSU.</summary>
    public void HoldUntilInput()
    {
        _heldUntilInput = true;
        Update();
    }

    /// <summary>Appelé à chaque touche ou bouton (voir <see cref="Input.InputRouter"/>).</summary>
    public void NotifyInput()
    {
        if (_heldUntilInput)
            Release();
    }

    private void Release()
    {
        _heldUntilInput = false;
        Update();
    }

    private void Update()
    {
        var active = _started && window.IsActive && !launcher.IsRunning && !_heldUntilInput;
        if (active == IsActive)
            return;

        IsActive = active;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
