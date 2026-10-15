using System.Globalization;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinSU.App.Input;
using WinSU.App.Localization;
using WinSU.App.Navigation;
using WinSU.App.Overlays;
using WinSU.App.Services;
using WinSU.App.Themes;
using WinSU.Core.Appearance;
using WinSU.Core.Audio;
using WinSU.Core.Input;
using WinSU.Core.Library;
using WinSU.Core.Power;
using WinSU.Core.Settings;
using WinSU.Core.Storage;
using Microsoft.Extensions.Logging;

namespace WinSU.App.ViewModels;

/// <summary>
/// Une carte de thème : Système, Clair, Sombre, ou un thème enregistré (M14) avec son identifiant et les couleurs de son
/// dégradé en aperçu.
/// </summary>
public sealed record ThemeOption(ThemePreference Value, string Label, string Description, string? CustomThemeId = null, IReadOnlyList<ThemeColor>? Swatches = null)
{
    public bool IsCustom => CustomThemeId is not null;
}

public sealed record LanguageOption(string Code, string Label);

/// <summary>Une carte « Style du menu » (M15, D-49) : Standard, Switch…</summary>
public sealed record MenuStyleOption(UiStyle Value, string Label, string Description);

/// <summary>
/// Sections des Réglages, dans leur ordre d'affichage : les capsules du tableau déroulant (étape 32, D-128), dans les
/// trois styles.
/// </summary>
public enum SettingsSection
{
    MenuStyle,
    Theme,

    /// <summary>Style des glyphes de boutons (M12), une section à part depuis l'étape 32 (maquette Sprite-0005).</summary>
    ButtonStyle,
    Controller,
    Audio,
    Display,

    /// <summary>Clé d'API SteamGridDB (étape 31).</summary>
    SteamGridDb,
    System,
}

/// <summary>
/// Une carte « Police » (étape 25 ; étape 32, D-129) : son nom, écrit dans la police qu'elle propose, et ses crédits
/// (auteurs, licence).
/// </summary>
public sealed record FontOption(AppFont Value, string Label, string Description);

/// <summary>Une carte « Style des boutons » (M12) : nom, aperçu des quatre boutons de face et précision.</summary>
public sealed partial class ButtonStyleOption(ButtonStyle value, string label) : ObservableObject
{
    public ButtonStyle Value { get; } = value;

    public string Label { get; } = label;

    /// <summary>A B X Y, ✕ ○ □ △… dans le style de la carte (celui de la manette pour « Automatique »), en couleurs si demandé.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<BindingText> Preview { get; set; } = [];

    /// <summary>« Selon la manette : PlayStation » pour « Automatique », gâchettes sinon (« LB RB · LT RT »).</summary>
    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;
}

/// <summary>
/// Onglet « Réglages » (M7) : thème, cadre, fond animé et style des boutons, manette, audio (sons de l'interface et
/// leur volume), affichage et langue, clé SteamGridDB (étape 31), système
/// (alimentation, quitter). Chaque modification est enregistrée aussitôt.
/// </summary>
public sealed partial class SettingsViewModel : ViewModelBase, IHintSource, IInputHandler
{
    private readonly ISettingsStore _settings;
    private readonly INavigationService _navigation;
    private readonly IShell _shell;
    private readonly IThemeStore _themes;
    private readonly IOverlayService _overlays;
    private readonly PowerMenu _power;
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly bool _initialized;
    private bool _rebuildingThemes;
    private bool _menuStyleFocusPending;

    public SettingsViewModel(
        ISettingsStore settings,
        INavigationService navigation,
        IShell shell,
        IThemeStore themes,
        IDataPaths paths,
        IOverlayService overlays,
        PowerMenu power,
        SteamGridDbSettingsViewModel steamGridDb,
        ILogger<SettingsViewModel> logger)
    {
        _settings = settings;
        _navigation = navigation;
        _shell = shell;
        _themes = themes;
        _overlays = overlays;
        _power = power;
        _logger = logger;
        SteamGridDb = steamGridDb;

        MenuStyleOptions =
        [
            new(UiStyle.Standard, Tr.Get("win_menu_style_standard"), Tr.Get("win_menu_style_standard_description")),
            new(UiStyle.Switch, "Switch", Tr.Get("win_menu_style_switch_description")),
            new(UiStyle.WiiU, "Wii U", Tr.Get("win_menu_style_wiiu_description")),
        ];
        Fold.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Hints));
        ButtonStyleOptions =
        [
            new(ButtonStyle.Xbox, "Xbox"),
            new(ButtonStyle.PlayStation, "PlayStation"),
            new(ButtonStyle.Switch, "Switch"),
        ];
        // Polices libres (SIL OFL 1.1 ; étape 32, D-129), aucune de l'application iiSU d'origine (30.10) : chaque carte nomme
        // ses auteurs (ceux de Google Fonts) et sa licence
        FontOptions = [.. AppFonts.Choices.Select(font => new FontOption(font.Value, font.Name, Tr.Format("win_font_credit", font.Designers)))];
        LanguageOptions = [new("fr", "Français"), new("en", "English")];
        DataFolderText = Tr.Format("win_data_folder", paths.Root);
        AboutText = Tr.Format(
            "win_settings_about",
            typeof(SettingsViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?");

        var current = settings.Current;
        SelectedMenuStyle = MenuStyleOptions.FirstOrDefault(o => o.Value == current.Appearance.Style) ?? MenuStyleOptions[0];
        RefreshThemes();
        ShowBorders = current.Appearance.ShowBorders;
        AnimatedBackground = current.Appearance.AnimatedBackground;
        TileSheen = current.Appearance.TileSheen;
        TileSheenSpeed = SpeedPercent(current.Appearance.TileSheenSpeed);
        SelectedButtonStyle = ButtonStyleOptions.First(o => o.Value == GlyphStyle.Normalize(current.Appearance.ButtonStyle));
        ColoredGlyphs = current.Appearance.ColoredGlyphs;
        RefreshButtonStyles();
        SelectedFont = FontOptions.FirstOrDefault(o => o.Value == current.Appearance.Font) ?? FontOptions[0];
        UiSounds = current.Audio.UiSounds;
        UiVolume = Percent(current.Audio.UiVolume);
        StartFullscreen = current.General.StartFullscreen;
        RestBehindGame = !current.Launch.MinimizeWhileRunning;
        SelectedLanguage = LanguageOptions.FirstOrDefault(o => o.Code == current.General.Language) ?? LanguageOptions[0];
        _initialized = true;
    }

    public override UiSound? EnterSound => null;

    /// <summary>Réglages → SteamGridDB (étape 31) : la clé d'API.</summary>
    public SteamGridDbSettingsViewModel SteamGridDb { get; }

    /// <summary>Système, Clair, Sombre, puis les thèmes enregistrés (M14).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ThemeOption> ThemeOptions { get; set; } = [];

    /// <summary>
    /// La carte de thème qui a le focus (donnée par la vue ; null hors de la rangée) : Menu y ouvre Modifier / Supprimer
    /// sur un thème enregistré. Ce n'est pas forcément le thème choisi : on choisit par Confirmer (D-78).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Hints))]
    public partial ThemeOption? FocusedTheme { get; set; }

    public IReadOnlyList<ButtonStyleOption> ButtonStyleOptions { get; }

    /// <summary>Polices de l'interface (étape 25, D-71) ; la carte choisie par Confirmer (D-78) s'applique tout de suite.</summary>
    public IReadOnlyList<FontOption> FontOptions { get; }

    /// <summary>Styles du menu (M15) : la carte choisie par Confirmer (D-78) s'applique tout de suite.</summary>
    public IReadOnlyList<MenuStyleOption> MenuStyleOptions { get; }

    [ObservableProperty]
    public partial MenuStyleOption? SelectedMenuStyle { get; set; }

    /// <summary>
    /// La section ouverte du tableau déroulant (étape 32, D-128) : une seule à la fois ; Retour la referme. Gardée quand
    /// on revient d'un écran ouvert depuis elle (« Boutons et touches », l'éditeur de thèmes).
    /// </summary>
    public FoldState Fold { get; } = new();

    public SettingsSection? OpenSection => Fold.Open as SettingsSection?;

    /// <summary>
    /// Le réglage qui avait le focus dans la section ouverte (posé par la vue) : au retour d'un écran ouvert depuis lui
    /// (« Boutons et touches », éditeur de thèmes), le focus y revient (DF-3, étape 35).
    /// </summary>
    public SettingsFocus? ContentFocus { get; set; }

    public IReadOnlyList<LanguageOption> LanguageOptions { get; }

    public string DataFolderText { get; }

    /// <summary>« WinSU 1.6.0 » (version de WinSU ; plus de données de l'APK d'iiSU depuis l'étape 34).</summary>
    public string AboutText { get; }

    /// <summary>Retour referme la section ouverte : l'aide le dit (« Retour »).</summary>
    public IReadOnlyList<Hint> Hints
    {
        get
        {
            List<Hint> hints = FocusedTheme?.IsCustom == true
                ? [Hint.Navigate, new(InputAction.ContextMenu, Tr.Get("win_hint_options"))]
                : [Hint.Navigate, new(InputAction.Confirm, Tr.Get("win_hint_confirm"))];
            if (Fold.Open is not null)
                hints.Add(new(InputAction.Back, Tr.Get("win_hint_back")));
            hints.Add(Hint.Tabs);
            return hints;
        }
    }

    /// <summary>Recalculé à chaque affichage : l'assistant (Configurer la manette) peut avoir changé le profil.</summary>
    [ObservableProperty]
    public partial string ControllerProfileText { get; set; } = string.Empty;

    /// <summary>Touches du clavier par défaut ou personnalisées (écran « Boutons », M11).</summary>
    [ObservableProperty]
    public partial string KeyboardProfileText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Hints))]
    public partial ThemeOption? SelectedTheme { get; set; }

    /// <summary>Cadre de la console sur les tuiles des jeux.</summary>
    [ObservableProperty]
    public partial bool ShowBorders { get; set; }

    /// <summary>Fond animé (lampe à lave, M10).</summary>
    [ObservableProperty]
    public partial bool AnimatedBackground { get; set; }

    /// <summary>Balayage de lumière sur la tuile qui prend le focus (étape 29, D-90).</summary>
    [ObservableProperty]
    public partial bool TileSheen { get; set; }

    /// <summary>Vitesse du balayage, en pourcentage de la vitesse normale (curseur : 50 à 200 %).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TileSheenSpeedText))]
    public partial double TileSheenSpeed { get; set; }

    public string TileSheenSpeedText => Tr.Format("win_percent", (int)TileSheenSpeed);

    /// <summary>Style des glyphes de boutons (M12).</summary>
    [ObservableProperty]
    public partial ButtonStyleOption? SelectedButtonStyle { get; set; }

    /// <summary>Glyphes des boutons en couleurs (D-34).</summary>
    [ObservableProperty]
    public partial bool ColoredGlyphs { get; set; }

    [ObservableProperty]
    public partial FontOption? SelectedFont { get; set; }

    [ObservableProperty]
    public partial bool UiSounds { get; set; }

    /// <summary>Volume des sons d'interface, en pourcentage (curseur).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UiVolumeText))]
    public partial double UiVolume { get; set; }

    public string UiVolumeText => Tr.Format("win_percent", (int)UiVolume);

    [ObservableProperty]
    public partial bool StartFullscreen { get; set; }

    /// <summary>Pendant un jeu, WinSU reste derrière lui en veille au lieu de se réduire (D-181, section Système).</summary>
    [ObservableProperty]
    public partial bool RestBehindGame { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LanguageNeedsRestart))]
    public partial LanguageOption? SelectedLanguage { get; set; }

    /// <summary>La langue choisie n'est pas celle affichée : elle s'appliquera au prochain démarrage.</summary>
    public bool LanguageNeedsRestart =>
        SelectedLanguage is { } language && language.Code != CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    public override void OnNavigatedTo()
    {
        var input = _settings.Current.Input;
        ControllerProfileText = input.Profile == InputProfile.Custom
            ? Tr.Format("win_settings_controller_custom", input.ControllerName ?? "?")
            : Tr.Get("win_settings_controller_default");
        KeyboardProfileText = Tr.Get(input.Keys is null ? "win_settings_keyboard_default" : "win_settings_keyboard_custom");
        RefreshThemes(); // l'éditeur de thèmes a pu en ajouter, en modifier ou en supprimer
        RefreshButtonStyles();
    }

    /// <summary>
    /// Retour referme la section ouverte (étape 32) ; Menu sur un thème enregistré : Modifier / Supprimer (M14).
    /// </summary>
    public bool HandleAction(InputActionEvent e)
    {
        if (e.Action == InputAction.Back && !e.IsRepeat && Fold.CloseOnBack())
            return true;

        if (e.Action != InputAction.ContextMenu || e.IsRepeat)
            return false;

        if (_themes.Find(FocusedTheme?.CustomThemeId) is { } theme)
        {
            _ = ShowThemeMenuAsync(theme);
            return true;
        }

        return false;
    }

    /// <summary>Ouvre une section (depuis l'écran principal, ou après un changement de style).</summary>
    public void ShowSection(SettingsSection section) => Fold.Show(section);

    /// <summary>
    /// Vrai une seule fois après un changement de style du menu : la page vient d'être reconstruite avec les vues du
    /// nouveau style, qui rendent le focus aux cartes du style.
    /// </summary>
    public bool TakeMenuStyleFocus()
    {
        var pending = _menuStyleFocusPending;
        _menuStyleFocusPending = false;
        return pending;
    }

    partial void OnSelectedThemeChanged(ThemeOption? value)
    {
        if (value is not null && !_rebuildingThemes)
            Save(s => s with { Appearance = s.Appearance with { Theme = value.Value, CustomThemeId = value.CustomThemeId } });
    }

    partial void OnSelectedMenuStyleChanged(MenuStyleOption? value)
    {
        if (value is null || !_initialized || value.Value == _settings.Current.Appearance.Style)
            return;

        _menuStyleFocusPending = true;
        Save(s => s with { Appearance = s.Appearance with { Style = value.Value } });
    }

    partial void OnShowBordersChanged(bool value) =>
        Save(s => s with { Appearance = s.Appearance with { ShowBorders = value } });

    partial void OnAnimatedBackgroundChanged(bool value) =>
        Save(s => s with { Appearance = s.Appearance with { AnimatedBackground = value } });

    partial void OnTileSheenChanged(bool value) =>
        Save(s => s with { Appearance = s.Appearance with { TileSheen = value } });

    partial void OnTileSheenSpeedChanged(double value) =>
        Save(s => s with { Appearance = s.Appearance with { TileSheenSpeed = value / 100 } });

    partial void OnSelectedButtonStyleChanged(ButtonStyleOption? value)
    {
        if (value is not null)
            Save(s => s with { Appearance = s.Appearance with { ButtonStyle = value.Value } });
    }

    partial void OnSelectedFontChanged(FontOption? value)
    {
        if (value is not null)
            Save(s => s with { Appearance = s.Appearance with { Font = value.Value } });
    }

    partial void OnColoredGlyphsChanged(bool value)
    {
        RefreshButtonStyles();
        Save(s => s with { Appearance = s.Appearance with { ColoredGlyphs = value } });
    }

    partial void OnUiSoundsChanged(bool value) =>
        Save(s => s with { Audio = s.Audio with { UiSounds = value } });

    partial void OnUiVolumeChanged(double value) =>
        Save(s => s with { Audio = s.Audio with { UiVolume = value / 100 } });

    partial void OnStartFullscreenChanged(bool value) =>
        Save(s => s with { General = s.General with { StartFullscreen = value } });

    partial void OnRestBehindGameChanged(bool value) =>
        Save(s => s with { Launch = s.Launch with { MinimizeWhileRunning = !value } });

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (value is not null)
            Save(s => s with { General = s.General with { Language = value.Code } });
    }

    [RelayCommand]
    private void CreateTheme() => _navigation.NavigateTo<ThemeEditorViewModel>();

    /// <summary>Menu d'un thème enregistré : Modifier (éditeur) ou Supprimer (confirmation, focus sur Annuler).</summary>
    private async Task ShowThemeMenuAsync(CustomTheme theme)
    {
        var choice = await _overlays.ShowMenuAsync(theme.Name, [Tr.Get("win_theme_edit"), Tr.Get("win_theme_delete")]);
        if (choice == 0)
        {
            _navigation.NavigateTo<ThemeEditorViewModel>(editor => editor.Edit(theme));
        }
        else if (choice == 1 && await _overlays.ConfirmAsync(
                     Tr.Get("win_theme_delete_title"),
                     Tr.Format("win_theme_delete_message", theme.Name),
                     Tr.Get("win_theme_delete"),
                     Tr.Get("win_cancel")))
        {
            await ThemeRemoval.DeleteAsync(theme, _themes, _settings, _logger);
            RefreshThemes();
        }
    }

    /// <summary>Reconstruit la rangée des thèmes et y choisit celui des réglages (sans enregistrer).</summary>
    private void RefreshThemes()
    {
        _rebuildingThemes = true;
        try
        {
            ThemeOptions =
            [
                new(ThemePreference.System, Tr.Get("win_theme_system"), Tr.Get("win_theme_system_description")),
                new(ThemePreference.Light, Tr.Get("win_theme_light"), Tr.Get("win_theme_light_description")),
                new(ThemePreference.Dark, Tr.Get("win_theme_dark"), Tr.Get("win_theme_dark_description")),
                .. _themes.Themes.Select(theme => new ThemeOption(
                    ThemePreference.Custom,
                    theme.Name,
                    Tr.Format(
                        theme.Mode == ThemeMode.Mono ? "win_theme_card_mono" : "win_theme_card_multi",
                        Tr.Get(theme.Base == ThemeBase.Light ? "win_theme_light" : "win_theme_dark")),
                    theme.Id,
                    ThemePalette.For(theme).Stops["Gradient"])),
            ];
            var appearance = _settings.Current.Appearance;
            SelectedTheme = ThemeOptions.FirstOrDefault(o => o.Value == appearance.Theme && (o.CustomThemeId is null || o.CustomThemeId == appearance.CustomThemeId))
                ?? ThemeOptions[0];
        }
        finally
        {
            _rebuildingThemes = false;
        }
    }

    [RelayCommand]
    private void EditButtons() => _navigation.NavigateTo<ButtonsViewModel>();

    /// <summary>Liste des sauvegardes (menu), confirmation, puis restauration ; la bibliothèque du moment est sauvegardée avant.</summary>

    [RelayCommand]
    private Task SleepAsync() => _power.RequestAsync(PowerAction.Sleep);

    [RelayCommand]
    private Task RestartAsync() => _power.RequestAsync(PowerAction.Restart);

    [RelayCommand]
    private Task ShutdownAsync() => _power.RequestAsync(PowerAction.Shutdown);

    [RelayCommand]
    private void Quit() => _shell.Quit();

    private static double Percent(double volume) => Math.Round(Math.Clamp(volume, 0, 1) * 100);

    /// <summary>Vitesse du balayage en pourcentage, dans les bornes du curseur.</summary>
    private static double SpeedPercent(double speed) =>
        Math.Round(Math.Clamp(double.IsFinite(speed) ? speed : 1, AppearanceSettings.MinTileSheenSpeed, AppearanceSettings.MaxTileSheenSpeed) * 100);

    /// <summary>Aperçus des cartes « Style des boutons » (M12) : les quatre boutons de face, en couleurs si demandé.</summary>
    private void RefreshButtonStyles()
    {
        foreach (var option in ButtonStyleOptions)
        {
            var glyphs = new GlyphStyle(option.Value, ColoredGlyphs);
            option.Preview = glyphs.FaceButtons;
            option.Description = glyphs.Pick("LB RB · LT RT", "L1 R1 · L2 R2", "L R · ZL ZR");
        }
    }

    private void Save(Func<AppSettings, AppSettings> update)
    {
        if (_initialized)
            _ = _settings.TrySaveAsync(update, _logger);
    }
}

/// <summary>Un réglage d'une section des Réglages : par son nom dans la vue s'il en a un, sinon par sa place (ordre de lecture).</summary>
public sealed record SettingsFocus(SettingsSection Section, string? Name, int Index);
