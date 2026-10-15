using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinSU.App.Input;
using WinSU.App.Localization;
using WinSU.App.Navigation;
using WinSU.App.Overlays;
using WinSU.App.Services;
using WinSU.Core.Appearance;
using WinSU.Core.Audio;
using WinSU.Core.Input;
using WinSU.Core.Settings;
using Microsoft.Extensions.Logging;

namespace WinSU.App.ViewModels;

/// <summary>Une couleur réglable du thème (pastille) : « Couleur », « Fond », « Dégradé 1 »…</summary>
public sealed partial class ColorSlotViewModel(string key, string label, ThemeColor color) : ObservableObject
{
    public string Key { get; } = key;

    public string Label { get; } = label;

    [ObservableProperty]
    public partial ThemeColor Color { get; set; } = color;

    /// <summary>Le sélecteur règle cette couleur.</summary>
    [ObservableProperty]
    public partial bool IsEditing { get; set; }
}

/// <summary>Une rangée de pastilles : « Fond, verre et accent », « Dégradé », « Lampe à lave ».</summary>
public sealed record ColorSlotGroup(string Label, IReadOnlyList<ColorSlotViewModel> Slots);

/// <summary>Sections du tableau déroulant de l'éditeur (étape 32, D-131), dans leur ordre d'affichage.</summary>
public enum ThemeEditorSection
{
    Name,
    Base,

    /// <summary>Monocouleur ou multicouleur, et les pastilles.</summary>
    Colors,
}

/// <summary>Une carte « Base » de l'éditeur : clair ou sombre.</summary>
public sealed record ThemeBaseChoice(ThemeBase Value, string Label, string Description);

/// <summary>Une carte « Couleurs » de l'éditeur : monocouleur ou multicouleur.</summary>
public sealed record ThemeModeChoice(ThemeMode Value, string Label, string Description);

/// <summary>
/// Éditeur de thème (M14, E10, E11) : nom, base claire ou sombre, monocouleur ou multicouleur, pastilles de couleur et
/// sélecteur (D-36, D-37) ; l'application entière montre le thème pendant l'édition (aperçu en direct) ; Enregistrer
/// l'enregistre et l'applique, Annuler (ou Retour) revient au thème d'avant, avec confirmation si quelque chose a changé.
/// </summary>
public sealed partial class ThemeEditorViewModel : ViewModelBase, IInputHandler, IHintSource, IFocusRequester
{
    private readonly IThemeStore _themes;
    private readonly ThemeService _themeService;
    private readonly ISettingsStore _settings;
    private readonly INavigationService _navigation;
    private readonly IOverlayService _overlays;
    private readonly IAudioService _audio;
    private readonly ILogger<ThemeEditorViewModel> _logger;
    private CustomTheme _original;
    private ThemeColor _colorBeforeEdit;
    private bool _isNew = true;
    private bool _leaving;
    private bool _loading;

    public ThemeEditorViewModel(
        IThemeStore themes,
        ThemeService themeService,
        ISettingsStore settings,
        INavigationService navigation,
        IOverlayService overlays,
        IAudioService audio,
        ILogger<ThemeEditorViewModel> logger)
    {
        _themes = themes;
        _themeService = themeService;
        _settings = settings;
        _navigation = navigation;
        _overlays = overlays;
        _audio = audio;
        _logger = logger;
        BaseOptions =
        [
            new(ThemeBase.Light, Tr.Get("win_theme_light"), Tr.Get("win_theme_light_description")),
            new(ThemeBase.Dark, Tr.Get("win_theme_dark"), Tr.Get("win_theme_dark_description")),
        ];
        ModeOptions =
        [
            new(ThemeMode.Mono, Tr.Get("win_theme_mono"), Tr.Get("win_theme_mono_description")),
            new(ThemeMode.Multi, Tr.Get("win_theme_multi"), Tr.Get("win_theme_multi_description")),
        ];
        Picker.ColorChanged += (_, _) => OnPickerChanged();
        _original = NewTheme();
        Load(_original);
    }

    public IReadOnlyList<ThemeBaseChoice> BaseOptions { get; }

    public IReadOnlyList<ThemeModeChoice> ModeOptions { get; }

    public ObservableCollection<ColorSlotGroup> Groups { get; } = [];

    public ColorPickerViewModel Picker { get; } = new();

    /// <summary>La section ouverte du tableau déroulant (étape 32) : une seule à la fois ; Retour la referme.</summary>
    public FoldState Fold { get; } = new();

    /// <summary>Le thème tel qu'il est à l'écran (appliqué à toute l'application).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(IsDirty), nameof(PreviewLava))]
    public partial CustomTheme Draft { get; set; } = new();

    /// <summary>Couleurs des gouttes de la lampe (aperçu : elles ne se voient que par intermittence sur le fond).</summary>
    public IReadOnlyList<ThemeColor> PreviewLava => ThemePalette.For(Draft).Stops["Lava"];

    [ObservableProperty]
    public partial ThemeBaseChoice? SelectedBase { get; set; }

    [ObservableProperty]
    public partial ThemeModeChoice? SelectedMode { get; set; }

    /// <summary>Pastille réglée par le sélecteur, ou null : le sélecteur est fermé.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPicking), nameof(Hints))]
    public partial ColorSlotViewModel? EditingSlot { get; set; }

    public bool IsPicking => EditingSlot is not null;

    public bool CanDelete => !_isNew;

    public string Title => _isNew ? Tr.Get("win_theme_new_title") : Tr.Format("win_theme_edit_title", Draft.Name);

    public bool IsDirty => Draft != _original;

    /// <summary>Demande à la vue de redonner le focus à une pastille (fin du réglage d'une couleur).</summary>
    public event EventHandler<object>? FocusRequested;

    public IReadOnlyList<Hint> Hints => IsPicking
        ?
        [
            new(InputAction.Up, Tr.Get("win_hint_color_square")),
            new(InputAction.PagePrevious, Tr.Get("win_hint_hue")),
            new(InputAction.ContextMenu, Tr.Get("win_hint_color_code")),
            new(InputAction.Confirm, Tr.Get("win_hint_confirm")),
            new(InputAction.Back, Tr.Get("win_cancel")),
        ]
        : [Hint.Navigate, new(InputAction.Confirm, Tr.Get("win_hint_confirm")), new(InputAction.Back, Tr.Get("win_hint_back"))];

    /// <summary>Ouvre l'éditeur sur un thème enregistré (sinon : nouveau thème monocouleur sur la base actuelle).</summary>
    public void Edit(CustomTheme theme)
    {
        _isNew = false;
        _original = theme;
        Load(theme);
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(Title));
    }

    public override void OnNavigatedTo() => _themeService.Preview(Draft);

    public override void OnNavigatedFrom()
    {
        EditingSlot = null;
        _themeService.EndPreview();
    }

    public bool HandleAction(InputActionEvent e)
    {
        if (EditingSlot is { } slot)
        {
            if (Picker.Handle(e.Action))
                return true;

            if (e.IsRepeat)
                return true;

            switch (e.Action)
            {
                case InputAction.Confirm:
                    StopEditing(slot, keep: true);
                    break;
                case InputAction.Back:
                    StopEditing(slot, keep: false);
                    break;
                case InputAction.ContextMenu:
                    _ = TypeCodeAsync(slot);
                    break;
            }

            return true; // le sélecteur ouvert garde toutes les actions
        }

        if (e.Action == InputAction.Back && !e.IsRepeat && Fold.CloseOnBack())
            return true;

        if (e.Action == InputAction.Back && !e.IsRepeat && IsDirty && !_leaving)
        {
            _ = CancelAsync();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Confirmer (ou clic) sur une pastille : le sélecteur s'ouvre sur sa couleur. À la souris, cliquer une autre pastille
    /// pendant un réglage garde la couleur en cours et passe à l'autre.
    /// </summary>
    [RelayCommand]
    private void EditColor(ColorSlotViewModel slot)
    {
        if (EditingSlot is { } current)
        {
            if (current == slot)
                return;
            StopEditing(current, keep: true, refocus: false);
        }

        _colorBeforeEdit = slot.Color;
        Picker.Load(slot.Color);
        slot.IsEditing = true;
        EditingSlot = slot;
        _audio.Play(UiSound.Open);
    }

    [RelayCommand]
    private async Task RenameAsync()
    {
        var name = await _overlays.PromptTextAsync(Tr.Get("win_theme_name_prompt"), Draft.Name, Tr.Get("win_ok"), Tr.Get("win_cancel"));
        if (!string.IsNullOrWhiteSpace(name))
            Draft = Draft with { Name = name.Trim() };
    }

    /// <summary>Enregistre et applique le thème, puis revient aux Réglages.</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            await _themes.SaveAsync(Draft);
            await _settings.UpdateAsync(s => s with { Appearance = s.Appearance with { Theme = ThemePreference.Custom, CustomThemeId = Draft.Id } });
            _logger.LogInformation("Thème enregistré : {Name} ({Id})", Draft.Name, Draft.Id);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Impossible d'enregistrer le thème");
            await _overlays.AlertAsync(Tr.Get("win_theme_save_failed_title"), Tr.Get("win_theme_save_failed_message"), Tr.Get("win_ok"));
            return;
        }

        _original = Draft;
        Leave();
    }

    /// <summary>Annuler : confirmation si quelque chose a changé, puis retour au thème d'avant.</summary>
    [RelayCommand]
    private async Task CancelAsync()
    {
        if (IsDirty && !await _overlays.ConfirmAsync(
                Tr.Get("win_theme_discard_title"),
                Tr.Format("win_theme_discard_message", Draft.Name),
                Tr.Get("win_theme_discard"),
                Tr.Get("win_theme_keep_editing")))
        {
            return;
        }

        Leave();
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (_isNew || !await _overlays.ConfirmAsync(
                Tr.Get("win_theme_delete_title"),
                Tr.Format("win_theme_delete_message", _original.Name),
                Tr.Get("win_theme_delete"),
                Tr.Get("win_cancel")))
        {
            return;
        }

        await ThemeRemoval.DeleteAsync(_original, _themes, _settings, _logger);
        Leave();
    }

    partial void OnSelectedBaseChanged(ThemeBaseChoice? value)
    {
        if (value is null || _loading || value.Value == Draft.Base)
            return;

        if (Draft.Mode == ThemeMode.Mono)
        {
            Update(Draft with { Base = value.Value });
            return;
        }

        // Multicouleur : un fond et un verre sombres sur une base claire (ou l'inverse) jureraient avec ses ombres et
        // ses liserés ; ils sont recalculés pour la nouvelle base, teintés par l'accent. Le reste des couleurs est gardé.
        var tinted = ThemePalette.Mono(value.Value, ThemeColor.TryParse(Draft.Colors.Accent, out var accent) ? accent : ThemeColor.Parse(Draft.Color));
        Update(Draft with
        {
            Base = value.Value,
            Colors = Draft.Colors with { Background = tinted["Background"].ToHex(), Glass = tinted["GlassOpaque"].ToHex() },
        });
        BuildSlots();
    }

    partial void OnSelectedModeChanged(ThemeModeChoice? value)
    {
        if (value is null || _loading || value.Value == Draft.Mode)
            return;

        // Passer en multicouleur part du rendu monocouleur, pour garder le thème tel qu'on le voit.
        var next = value.Value == ThemeMode.Multi
            ? Draft with { Mode = ThemeMode.Multi, Colors = ThemePalette.For(Draft).ToChosenColors() }
            : Draft with { Mode = ThemeMode.Mono };
        Update(next);
        BuildSlots();
    }

    private CustomTheme NewTheme()
    {
        var dark = _settings.Current.Appearance.Theme switch
        {
            ThemePreference.Light => false,
            ThemePreference.Custom => _themes.Find(_settings.Current.Appearance.CustomThemeId)?.Base != ThemeBase.Light,
            _ => true,
        };
        return new CustomTheme
        {
            Name = Tr.Format("win_theme_default_name", _themes.Themes.Count + 1),
            Base = dark ? ThemeBase.Dark : ThemeBase.Light,
        };
    }

    private void Load(CustomTheme theme)
    {
        _loading = true;
        Draft = theme;
        SelectedBase = BaseOptions.First(o => o.Value == theme.Base);
        SelectedMode = ModeOptions.First(o => o.Value == theme.Mode);
        _loading = false;
        BuildSlots();
    }

    /// <summary>Une pastille en monocouleur ; dix en multicouleur, en trois rangées.</summary>
    private void BuildSlots()
    {
        Groups.Clear();
        if (Draft.Mode == ThemeMode.Mono)
        {
            Groups.Add(new ColorSlotGroup(Tr.Get("win_theme_color"), [Slot("Color", Tr.Get("win_theme_color"), Draft.Color)]));
            return;
        }

        var colors = Draft.Colors;
        Groups.Add(new ColorSlotGroup(Tr.Get("win_theme_group_surfaces"),
        [
            Slot(nameof(ThemeColors.Background), Tr.Get("win_theme_background"), colors.Background),
            Slot(nameof(ThemeColors.Glass), Tr.Get("win_theme_glass"), colors.Glass),
            Slot(nameof(ThemeColors.Accent), Tr.Get("win_theme_accent"), colors.Accent),
        ]));
        Groups.Add(new ColorSlotGroup(Tr.Get("win_theme_group_gradient"),
        [
            Slot(nameof(ThemeColors.Gradient1), "1", colors.Gradient1),
            Slot(nameof(ThemeColors.Gradient2), "2", colors.Gradient2),
            Slot(nameof(ThemeColors.Gradient3), "3", colors.Gradient3),
        ]));
        Groups.Add(new ColorSlotGroup(Tr.Get("win_theme_group_lava"),
        [
            Slot(nameof(ThemeColors.Lava1), "1", colors.Lava1),
            Slot(nameof(ThemeColors.Lava2), "2", colors.Lava2),
            Slot(nameof(ThemeColors.Lava3), "3", colors.Lava3),
            Slot(nameof(ThemeColors.Lava4), "4", colors.Lava4),
        ]));
    }

    private static ColorSlotViewModel Slot(string key, string label, string hex) =>
        new(key, label, ThemeColor.TryParse(hex, out var color) ? color : ThemeColor.Black);

    private void OnPickerChanged()
    {
        if (EditingSlot is not { } slot)
            return;

        slot.Color = Picker.Color;
        Update(WithSlot(Draft, slot));
    }

    /// <summary>Le thème avec la couleur de la pastille.</summary>
    private static CustomTheme WithSlot(CustomTheme theme, ColorSlotViewModel slot)
    {
        var hex = slot.Color.ToHex();
        if (slot.Key == "Color")
            return theme with { Color = hex };

        var colors = theme.Colors;
        return theme with
        {
            Colors = slot.Key switch
            {
                nameof(ThemeColors.Background) => colors with { Background = hex },
                nameof(ThemeColors.Glass) => colors with { Glass = hex },
                nameof(ThemeColors.Accent) => colors with { Accent = hex },
                nameof(ThemeColors.Gradient1) => colors with { Gradient1 = hex },
                nameof(ThemeColors.Gradient2) => colors with { Gradient2 = hex },
                nameof(ThemeColors.Gradient3) => colors with { Gradient3 = hex },
                nameof(ThemeColors.Lava1) => colors with { Lava1 = hex },
                nameof(ThemeColors.Lava2) => colors with { Lava2 = hex },
                nameof(ThemeColors.Lava3) => colors with { Lava3 = hex },
                _ => colors with { Lava4 = hex },
            },
        };
    }

    /// <summary>Nouveau brouillon, montré aussitôt dans toute l'application.</summary>
    private void Update(CustomTheme next)
    {
        Draft = next;
        if (!_leaving)
            _themeService.Preview(next);
    }

    /// <summary>Boutons du sélecteur, pour la souris (la manette et le clavier passent par Valider, Retour et Menu).</summary>
    [RelayCommand]
    private void ConfirmColor()
    {
        if (EditingSlot is { } slot)
            StopEditing(slot, keep: true);
    }

    [RelayCommand]
    private void CancelColor()
    {
        if (EditingSlot is { } slot)
            StopEditing(slot, keep: false);
    }

    [RelayCommand]
    private Task TypeColorCodeAsync() => EditingSlot is { } slot ? TypeCodeAsync(slot) : Task.CompletedTask;

    private void StopEditing(ColorSlotViewModel slot, bool keep, bool refocus = true)
    {
        if (!keep)
        {
            slot.Color = _colorBeforeEdit;
            Update(WithSlot(Draft, slot));
        }

        slot.IsEditing = false;
        EditingSlot = null;
        _audio.Play(keep ? UiSound.Open : UiSound.Close);
        if (refocus)
            FocusRequested?.Invoke(this, slot);
    }

    /// <summary>Menu dans le sélecteur : taper le code hexadécimal (« #9D8CF5 » ou « 9D8CF5 »).</summary>
    private async Task TypeCodeAsync(ColorSlotViewModel slot)
    {
        var text = await _overlays.PromptTextAsync(Tr.Get("win_theme_code_title"), slot.Color.ToHex(), Tr.Get("win_ok"), Tr.Get("win_cancel"));
        if (text is null || EditingSlot != slot)
            return;

        if (!ThemeColor.TryParse(text, out var color))
        {
            await _overlays.AlertAsync(Tr.Get("win_theme_code_title"), Tr.Format("win_theme_code_invalid", text), Tr.Get("win_ok"));
            return;
        }

        Picker.Load(color with { A = 255 });
        slot.Color = color with { A = 255 };
        Update(WithSlot(Draft, slot));
    }

    private void Leave()
    {
        _leaving = true;
        _navigation.GoBack();
    }
}

/// <summary>
/// Suppression d'un thème enregistré (éditeur et Réglages) : s'il était appliqué, sa base claire ou sombre le remplace. Si
/// ce remplacement ne s'enregistre pas, le thème reste (les réglages ne désignent jamais un thème supprimé).
/// </summary>
public static class ThemeRemoval
{
    public static async Task DeleteAsync(CustomTheme theme, IThemeStore themes, ISettingsStore settings, ILogger logger)
    {
        var appearance = settings.Current.Appearance;
        if (appearance.Theme == ThemePreference.Custom && appearance.CustomThemeId == theme.Id)
        {
            var fallback = theme.Base == ThemeBase.Light ? ThemePreference.Light : ThemePreference.Dark;
            if (!await settings.TrySaveAsync(s => s with { Appearance = s.Appearance with { Theme = fallback, CustomThemeId = null } }, logger))
                return;
        }

        await themes.DeleteAsync(theme.Id);
    }
}
