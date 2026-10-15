using Avalonia.Controls;
using WinSU.App.Views;
using WinSU.Infrastructure.Win32;
using Microsoft.Extensions.DependencyInjection;

namespace WinSU.App.Services;

/// <summary>Ce dont le lancement des jeux a besoin sur la fenêtre principale.</summary>
public interface IWindowControl
{
    /// <summary>Handle Win32 de la fenêtre (propriétaire des boîtes de dialogue Windows).</summary>
    IntPtr Handle { get; }

    /// <summary>WinSU est la fenêtre active (au premier plan).</summary>
    bool IsActive { get; }

    /// <summary>La fenêtre vient d'être activée ou désactivée (jeu lancé, Alt+Tab, réduction…).</summary>
    event EventHandler? ActivationChanged;

    /// <summary>
    /// Réduit WinSU dès qu'il perd le premier plan (le jeu est passé devant), jusqu'à la libération du résultat.
    /// Réduire tout de suite donnerait le premier plan à la fenêtre suivante (navigateur…), et le jeu s'ouvrirait derrière.
    /// </summary>
    IDisposable MinimizeWhenDeactivated();

    /// <summary>
    /// Laisse WinSU derrière le jeu, en veille (D-181, réglage du Système) : dès qu'il perd le premier plan, plus rien ne
    /// bouge (classe « resting » de la fenêtre : horloges arrêtées), priorité basse et mémoire inutilisée rendue
    /// (<see cref="ProcessRest"/>) ; tout reprend à la libération du résultat.
    /// </summary>
    IDisposable RestWhenDeactivated();

    /// <summary>Restaure la fenêtre et la ramène au premier plan. Renvoie false si Windows a refusé le focus.</summary>
    bool BringToFront();

    /// <summary>Terminée la prochaine fois que l'utilisateur réactive WinSU (clic, Alt+Tab, barre des tâches).</summary>
    Task WaitForActivationAsync(CancellationToken cancellationToken = default);
}

/// <summary>Implémentation sur <see cref="MainWindow"/>, résolue à la demande (évite une dépendance circulaire).</summary>
internal sealed class MainWindowControl(IServiceProvider services) : IWindowControl
{
    private MainWindow Window => services.GetRequiredService<MainWindow>();

    public IntPtr Handle => Window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

    private EventHandler? _activationChanged;
    private bool _watchingActivation;

    public bool IsActive => Window.IsActive;

    /// <summary>
    /// Suit la propriété <see cref="WindowBase.IsActive"/> et non les événements Activated/Deactivated : Avalonia les
    /// déclenche avant de mettre la propriété à jour (vu en réel : WinSU se croyait en arrière-plan au démarrage).
    /// </summary>
    public event EventHandler? ActivationChanged
    {
        add
        {
            if (!_watchingActivation)
            {
                _watchingActivation = true;
                Window.PropertyChanged += (_, e) =>
                {
                    if (e.Property == WindowBase.IsActiveProperty)
                        _activationChanged?.Invoke(this, EventArgs.Empty);
                };
            }

            _activationChanged += value;
        }
        remove => _activationChanged -= value;
    }

    public IDisposable MinimizeWhenDeactivated() => WhenDeactivated(() =>
    {
        Minimize();
        return null;
    });

    public IDisposable RestWhenDeactivated() => WhenDeactivated(() =>
    {
        var window = Window;
        window.Classes.Set("resting", true);
        var rest = ProcessRest.Enter();
        return new Subscription(() =>
        {
            rest.Dispose();
            window.Classes.Set("resting", false);
        });
    });

    /// <summary>
    /// Fait <paramref name="act"/> dès que WinSU perd le premier plan (le jeu est passé devant), tout de suite s'il l'a déjà
    /// perdu ; la libération du résultat défait ce qu'il a rendu (rien pour une réduction : le retour au premier plan la défait).
    /// </summary>
    private IDisposable WhenDeactivated(Func<IDisposable?> act)
    {
        var window = Window;
        IDisposable? done = null;
        if (!window.IsActive)
        {
            done = act();
            return new Subscription(() => done?.Dispose());
        }

        void OnDeactivated(object? sender, EventArgs e)
        {
            window.Deactivated -= OnDeactivated;
            done = act();
        }

        window.Deactivated += OnDeactivated;
        return new Subscription(() =>
        {
            window.Deactivated -= OnDeactivated;
            done?.Dispose();
        });
    }

    public bool BringToFront() => Window.BringToFront();

    public Task WaitForActivationAsync(CancellationToken cancellationToken = default)
    {
        var window = Window;
        var activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnActivated(object? sender, EventArgs e)
        {
            window.Activated -= OnActivated;
            activated.TrySetResult();
        }

        window.Activated += OnActivated;
        cancellationToken.Register(() =>
        {
            window.Activated -= OnActivated;
            activated.TrySetCanceled(cancellationToken);
        });
        return activated.Task;
    }

    /// <summary>Sans activer la fenêtre suivante, qui passerait devant le jeu (Avalonia met son état à jour de lui-même).</summary>
    private void Minimize()
    {
        var handle = Handle;
        if (handle != IntPtr.Zero)
            ForegroundWindow.MinimizeWithoutActivating(handle);
        else
            Window.WindowState = WindowState.Minimized;
    }

    private sealed class Subscription(Action unsubscribe) : IDisposable
    {
        public void Dispose() => unsubscribe();
    }
}
