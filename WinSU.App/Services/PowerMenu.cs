using System.ComponentModel;
using WinSU.App.Localization;
using WinSU.App.Overlays;
using WinSU.Core.Power;
using Microsoft.Extensions.Logging;

namespace WinSU.App.Services;

/// <summary>
/// Alimentation (D-18) : menu du bouton Start (Veille, Redémarrer, Éteindre, Quitter WinSU), aussi utilisé par
/// Réglages → Système. Redémarrer et Éteindre demandent confirmation, le focus sur « Annuler ».
/// </summary>
public sealed class PowerMenu(IOverlayService overlays, IPowerService power, IShell shell, ActiveUse use, ILogger<PowerMenu> logger)
{
    private bool _open;

    /// <summary>
    /// Attente avant la veille : le temps de relâcher la touche qui l'a demandée, dont le relâchement rallumerait
    /// aussitôt l'écran éteint (veille moderne).
    /// </summary>
    public TimeSpan SleepDelay { get; init; } = TimeSpan.FromSeconds(1);

    public async Task ShowAsync()
    {
        if (_open)
            return;

        _open = true;
        try
        {
            // Deux groupes (30.14) : l'ordinateur ; WinSU
            await overlays.ShowActionsAsync(Tr.Get("win_power_title"),
            [
                new(Tr.Get("win_power_sleep"), () => RequestAsync(PowerAction.Sleep)),
                new(Tr.Get("win_power_restart"), () => RequestAsync(PowerAction.Restart)),
                new(Tr.Get("win_power_shutdown"), () => RequestAsync(PowerAction.Shutdown)),
                new(Tr.Get("win_home_quit"), () =>
                {
                    shell.Quit();
                    return Task.CompletedTask;
                }, startsGroup: true),
            ]);
        }
        finally
        {
            _open = false;
        }
    }

    /// <summary>La veille tout de suite ; redémarrer et éteindre après confirmation.</summary>
    public async Task RequestAsync(PowerAction action)
    {
        if (action != PowerAction.Sleep)
        {
            var (title, message, label) = action == PowerAction.Restart
                ? ("win_power_restart_title", "win_power_restart_message", "win_power_restart")
                : ("win_power_shutdown_title", "win_power_shutdown_message", "win_power_shutdown");
            if (!await overlays.ConfirmAsync(Tr.Get(title), Tr.Get(message), Tr.Get(label), Tr.Get("win_cancel")))
                return;
        }

        try
        {
            if (action == PowerAction.Sleep)
            {
                use.HoldUntilInput(); // ce qui suit ActiveUse s'arrête jusqu'au prochain appui
                await Task.Delay(SleepDelay);
            }

            power.Execute(action);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            logger.LogError(ex, "Windows a refusé : {Action}", action);
            await overlays.AlertAsync(Tr.Get("win_power_failed_title"), ex.Message, Tr.Get("win_ok"));
        }
    }
}
