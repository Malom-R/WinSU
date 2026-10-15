using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinSU.App.Localization;
using WinSU.App.Overlays;
using WinSU.App.Services;
using WinSU.Core.Media;
using WinSU.Core.Settings;
using Microsoft.Extensions.Logging;

namespace WinSU.App.ViewModels;

/// <summary>
/// Réglages → SteamGridDB (étape 31, D-115) : la clé d'API de l'utilisateur. Collée au clavier, vérifiée avant d'être
/// enregistrée (refusée : rien n'est enregistré ; site injoignable : enregistrée, vérifiée à la première recherche) ;
/// retirée après confirmation ; la page du site où on la crée s'ouvre dans le navigateur. Jamais écrite au journal.
/// </summary>
public sealed partial class SteamGridDbSettingsViewModel : ObservableObject
{
    /// <summary>Page du compte SteamGridDB où l'utilisateur crée sa clé.</summary>
    public const string KeysPage = "https://www.steamgriddb.com/profile/preferences/api";

    private readonly ISettingsStore _settings;
    private readonly IArtworkSearch _search;
    private readonly IOverlayService _overlays;
    private readonly IShell _shell;
    private readonly ILogger<SteamGridDbSettingsViewModel> _logger;

    public SteamGridDbSettingsViewModel(
        ISettingsStore settings,
        IArtworkSearch search,
        IOverlayService overlays,
        IShell shell,
        ILogger<SteamGridDbSettingsViewModel> logger)
    {
        _settings = settings;
        _search = search;
        _overlays = overlays;
        _shell = shell;
        _logger = logger;
        Refresh();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EnterKeyLabel))]
    public partial bool HasKey { get; private set; }

    [ObservableProperty]
    public partial string StatusText { get; private set; } = string.Empty;

    public string EnterKeyLabel => Tr.Get(HasKey ? "win_sgdb_change_key" : "win_sgdb_enter_key");

    /// <summary>L'adresse de la page des clés, à lire (sans « https:// »).</summary>
    public static string KeysPageText => KeysPage["https://www.".Length..];

    // Le bouton reste actif pendant la commande : à la fermeture de la saisie puis du message, le focus lui revient
    // (un bouton grisé ne le reprend pas ; il retombait en haut des Réglages, vu en essai réel, 31.5).
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task EnterKeyAsync()
    {
        var text = await _overlays.PromptTextAsync(Tr.Get("win_sgdb_key_prompt"), string.Empty, Tr.Get("win_ok"), Tr.Get("win_cancel"));
        if (text?.Trim() is not { Length: > 0 } key)
            return;

        StatusText = Tr.Get("win_sgdb_checking");
        var error = await _search.CheckKeyAsync(key);
        if (error == ArtworkError.KeyRefused)
        {
            Refresh();
            await _overlays.AlertAsync(Tr.Get("win_sgdb_key_refused_title"), Tr.Get("win_sgdb_key_refused_message"), Tr.Get("win_ok"));
            return;
        }

        var saved = await _settings.TrySaveAsync(s => s with { Online = s.Online with { SteamGridDbKey = key } }, _logger);
        Refresh();
        if (!saved)
            return;

        _logger.LogInformation("Clé SteamGridDB enregistrée (vérification : {Result})", error);
        if (error == ArtworkError.None)
            await _overlays.AlertAsync(Tr.Get("win_sgdb_key_ok_title"), Tr.Get("win_sgdb_key_ok_message"), Tr.Get("win_ok"));
        else
            await _overlays.AlertAsync(Tr.Get("win_sgdb_key_unchecked_title"), Tr.Get("win_sgdb_key_unchecked_message"), Tr.Get("win_ok"));
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task RemoveKeyAsync()
    {
        if (!HasKey || !await _overlays.ConfirmAsync(
                Tr.Get("win_sgdb_remove_title"), Tr.Get("win_sgdb_remove_message"), Tr.Get("win_sgdb_remove_key"), Tr.Get("win_cancel")))
            return;

        if (await _settings.TrySaveAsync(s => s with { Online = s.Online with { SteamGridDbKey = null } }, _logger))
            _logger.LogInformation("Clé SteamGridDB retirée");
        Refresh();
    }

    [RelayCommand]
    private void OpenKeysPage() => _shell.OpenInBrowser(new Uri(KeysPage));

    private void Refresh()
    {
        HasKey = _search.HasKey;
        var key = _settings.Current.Online.SteamGridDbKey?.Trim() ?? string.Empty;
        StatusText = HasKey ? Tr.Format("win_sgdb_key_saved", key[Math.Max(0, key.Length - 4)..]) : Tr.Get("win_sgdb_no_key");
    }
}
