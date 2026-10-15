using Microsoft.Extensions.Logging;
using WinSU.Core.Settings;

namespace WinSU.App.Services;

/// <summary>
/// Enregistrer un réglage depuis un écran, la même façon partout (étape 36, C-10) : une erreur d'écriture de
/// <c>settings.json</c> (disque plein, fichier verrouillé) va au journal au lieu de remonter ; les réglages en mémoire
/// restent alors ceux du disque.
/// </summary>
internal static class SettingsSaving
{
    /// <summary>True si le changement est enregistré (ou n'en était pas un), false si l'écriture a échoué.</summary>
    public static async Task<bool> TrySaveAsync(this ISettingsStore settings, Func<AppSettings, AppSettings> update, ILogger logger)
    {
        try
        {
            await settings.UpdateAsync(update);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Impossible d'enregistrer les réglages");
            return false;
        }
    }
}
