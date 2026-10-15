using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinSU.App.Input;
using WinSU.App.Localization;
using WinSU.App.Overlays;
using WinSU.App.Services;
using WinSU.App.ViewModels.Wizard;
using WinSU.Core.Audio;
using WinSU.Core.Input;
using WinSU.Core.Settings;
using Microsoft.Extensions.Logging;

namespace WinSU.App.ViewModels;

/// <summary>Une liaison affichée en pastille : glyphe et/ou texte ; Confirmer la remplace, Menu la retire.</summary>
public sealed class BindingChipViewModel(BindingRowViewModel row, int index, BindingText text, IAsyncRelayCommand replace)
{
    public BindingRowViewModel Row { get; } = row;

    /// <summary>Place de la liaison dans la liste de l'action (0 = celle de la barre d'aide).</summary>
    public int Index { get; } = index;

    public string? Glyph { get; } = text.Glyph;

    public string? Text { get; } = text.Text;

    /// <summary>Couleur du glyphe (réglage « Glyphes en couleurs », M12).</summary>
    public GlyphTint Tint { get; } = text.Tint;

    public bool HasGlyph => Glyph is not null;

    public bool HasText => Text is not null;

    public IAsyncRelayCommand ReplaceCommand { get; } = replace;
}

/// <summary>Une ligne de l'écran « Boutons » : une action, ses liaisons, et « Ajouter ».</summary>
public sealed partial class BindingRowViewModel(InputAction action) : ObservableObject
{
    public InputAction Action { get; } = action;

    public string Label { get; } = ActionLabels.Of(action);

    public ObservableCollection<BindingChipViewModel> Chips { get; } = [];

    public IAsyncRelayCommand AddCommand { get; set; } = null!;

    /// <summary>Une capture attend l'entrée de cette ligne (« Appuyez… » à la place de « Ajouter »).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AddLabel))]
    public partial bool IsCapturing { get; set; }

    /// <summary>« Ajouter » est proposé : moins de deux liaisons (étape 32, D-130 : <see cref="BindingEdits.MaxPerAction"/>).</summary>
    [ObservableProperty]
    public partial bool CanAdd { get; set; } = true;

    public string AddLabel => Tr.Get(IsCapturing ? "win_buttons_press" : "win_buttons_add");
}

/// <summary>Un onglet de l'écran « Boutons » : la manette ou le clavier.</summary>
public sealed partial class InputDeviceTab(InputDevice device, string title) : ObservableObject
{
    public InputDevice Device { get; } = device;

    public string Title { get; } = title;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>
/// Écran « Boutons » (M11, D-27) : jusqu'à deux liaisons par action (étape 32, D-130), à la manette et au clavier (deux
/// onglets, LB/RB). « Ajouter » ou Confirmer sur une pastille capture une entrée (Échap ou 10 s sans rien : annulé) ; Menu
/// sur une pastille la retire. Garde-fous : une entrée ne sert qu'à une action, les directions, Valider et Retour gardent
/// au moins une liaison, F11 reste au plein écran, « Rétablir par défaut » est toujours là. Chaque modification est
/// enregistrée. À droite (maquette Sprite-0006), la manette ou le clavier dessinés s'allument quand on appuie.
/// </summary>
public sealed partial class ButtonsViewModel : ViewModelBase, IInputHandler, IHintSource, IFocusRequester
{
    /// <summary>Sans entrée pendant ce délai, la capture s'arrête.</summary>
    public static readonly TimeSpan CaptureTimeout = TimeSpan.FromSeconds(10);

    private readonly ISettingsStore _settings;
    private readonly IInputService _input;
    private readonly KeyboardInput _keyboard;
    private readonly IOverlayService _overlays;
    private readonly IAudioService _audio;
    private readonly ILogger<ButtonsViewModel> _logger;
    private CancellationTokenSource? _capture;
    private bool _cancelledByUser;

    public ButtonsViewModel(
        ISettingsStore settings,
        IInputService input,
        KeyboardInput keyboard,
        IKeyboardLayout layout,
        IOverlayService overlays,
        IAudioService audio,
        ILogger<ButtonsViewModel> logger)
    {
        _settings = settings;
        _input = input;
        _keyboard = keyboard;
        _overlays = overlays;
        _audio = audio;
        _logger = logger;
        Tabs = [new(InputDevice.Controller, Tr.Get("win_buttons_tab_controller")), new(InputDevice.Keyboard, Tr.Get("win_buttons_tab_keyboard"))];
        Rows = [.. FirstRunWizardViewModel.MappingOrder.Select(action => CreateRow(action))];
        Pad = new GamepadPreviewViewModel(input, settings, withIndicators: false);
        Keys = new KeyboardPreviewViewModel(keyboard, layout);
        SelectedTab = Tabs[0];
        SelectedTab.IsSelected = true;
        Refresh();
    }

    public IReadOnlyList<InputDeviceTab> Tabs { get; }

    /// <summary>La manette dessinée de l'onglet Manette (celle de l'assistant) : ce qu'on appuie s'allume.</summary>
    public GamepadPreviewViewModel Pad { get; }

    /// <summary>Le clavier dessiné de l'onglet Clavier : la touche enfoncée s'allume.</summary>
    public KeyboardPreviewViewModel Keys { get; }

    public IReadOnlyList<BindingRowViewModel> Rows { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsKeyboard), nameof(DeviceText))]
    public partial InputDeviceTab SelectedTab { get; set; }

    public bool IsKeyboard => SelectedTab.Device == InputDevice.Keyboard;

    /// <summary>Message de la dernière action : refus (déjà utilisé, dernière liaison), consigne de capture…</summary>
    [ObservableProperty]
    public partial string? Message { get; set; }

    /// <summary>Élément qui a le focus (pastille ou ligne de « Ajouter »), donné par la vue.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Hints))]
    public partial object? Focused { get; set; }

    /// <summary>« Manette : Xbox 360 Controller » ou, au clavier, le rappel de F11.</summary>
    public string DeviceText => IsKeyboard
        ? Tr.Get("win_buttons_keyboard_note")
        : _input.Controllers.Count == 0
            ? Tr.Get("win_buttons_no_controller")
            : Tr.Format("win_buttons_controller", string.Join(", ", _input.Controllers.Select(c => c.Name)));

    public bool IsCapturing => _capture is not null;

    /// <summary>Demande à la vue de donner le focus à un élément (pastille ou ligne) après une modification.</summary>
    public event EventHandler<object>? FocusRequested;

    public IReadOnlyList<Hint> Hints => Focused switch
    {
        BindingChipViewModel =>
        [
            Hint.Navigate,
            new(InputAction.Confirm, Tr.Get("win_hint_replace")),
            new(InputAction.ContextMenu, Tr.Get("win_hint_remove")),
            new(InputAction.PagePrevious, Tr.Get("win_hint_buttons_tabs")),
            new(InputAction.Back, Tr.Get("win_hint_back")),
        ],
        _ =>
        [
            Hint.Navigate,
            new(InputAction.Confirm, Tr.Get(Focused is BindingRowViewModel ? "win_hint_add" : "win_hint_confirm")),
            new(InputAction.PagePrevious, Tr.Get("win_hint_buttons_tabs")),
            new(InputAction.Back, Tr.Get("win_hint_back")),
        ],
    };

    public override void OnNavigatedTo()
    {
        _input.ControllersChanged += OnControllersChanged;
        Refresh();
        FollowDevice();
    }

    public override void OnNavigatedFrom()
    {
        _input.ControllersChanged -= OnControllersChanged;
        CancelCapture();
        Pad.End();
        Keys.End();
    }

    public bool HandleAction(InputActionEvent e)
    {
        if (IsCapturing)
        {
            // La manette est en capture ; au clavier, Échap l'annule (la capture d'une touche le traite elle-même).
            if (e is { Device: InputDevice.Keyboard, Action: InputAction.Back, IsRepeat: false })
                CancelCapture();
            return true;
        }

        switch (e.Action)
        {
            case InputAction.PagePrevious or InputAction.PageNext when !e.IsRepeat:
                SelectTab(Tabs[SelectedTab.Device == InputDevice.Controller ? 1 : 0]);
                return true;

            case InputAction.ContextMenu when !e.IsRepeat && Focused is BindingChipViewModel chip:
                _ = RemoveAsync(chip);
                return true;

            default:
                return false;
        }
    }

    /// <summary>Clic ou LB/RB sur un onglet.</summary>
    [RelayCommand]
    private void SelectTab(InputDeviceTab tab)
    {
        if (tab == SelectedTab)
            return;

        SelectedTab.IsSelected = false;
        SelectedTab = tab;
        tab.IsSelected = true;
        Message = null;
        _audio.Play(UiSound.Navigate);
        Refresh();
        FollowDevice();
        FocusRequested?.Invoke(this, (object?)Rows[0].Chips.FirstOrDefault() ?? Rows[0]);
    }

    /// <summary>À droite, le dessin de l'onglet choisi suit son appareil ; l'autre s'arrête.</summary>
    private void FollowDevice()
    {
        if (IsKeyboard)
        {
            Pad.End();
            Keys.Begin();
        }
        else
        {
            Keys.End();
            Pad.Begin();
        }
    }

    /// <summary>Rétablit les liaisons par défaut de l'onglet (après confirmation, focus sur Annuler).</summary>
    [RelayCommand]
    private async Task ResetAsync()
    {
        var keyboard = IsKeyboard;
        if (!await _overlays.ConfirmAsync(
                Tr.Get("win_buttons_reset_title"),
                Tr.Get(keyboard ? "win_buttons_reset_keyboard_message" : "win_buttons_reset_controller_message"),
                Tr.Get("win_buttons_reset"),
                Tr.Get("win_cancel")))
        {
            return;
        }

        await SaveAsync(s => s with
        {
            Input = keyboard
                ? s.Input with { Keys = null }
                : s.Input with { Profile = InputProfile.Default, Bindings = new InputSettings().Bindings },
        });
        Message = Tr.Get("win_buttons_reset_done");
    }

    private BindingRowViewModel CreateRow(InputAction action)
    {
        var row = new BindingRowViewModel(action);
        row.AddCommand = new AsyncRelayCommand(() => CaptureAsync(row, replaceIndex: null));
        return row;
    }

    /// <summary>Style des glyphes en vigueur (M12) : réglages et manettes branchées.</summary>
    private GlyphStyle Glyphs() => GlyphStyle.From(_settings.Current.Appearance);

    /// <summary>Remplit les lignes avec les liaisons de l'onglet choisi.</summary>
    private void Refresh()
    {
        OnPropertyChanged(nameof(DeviceText));
        var input = _settings.Current.Input;
        var glyphs = Glyphs();
        var mapping = ControllerInfo.MappingOf(_input.Controllers); // une ancienne liaison brute y prend son glyphe (DF-4)
        foreach (var row in Rows)
        {
            row.Chips.Clear();
            var texts = IsKeyboard
                ? KeyboardInput.Normalize(input.GetKeys()).GetValueOrDefault(row.Action, []).Select(name => new BindingText(null, ActionLabels.KeyName(name)))
                : input.GetControllerBindings().GetValueOrDefault(row.Action, []).Select(binding => ActionLabels.Show(GamepadMapping.ForDisplay(binding, mapping), glyphs));
            foreach (var (text, index) in texts.Select((text, index) => (text, index)))
            {
                BindingChipViewModel? chip = null;
                chip = new BindingChipViewModel(row, index, text, new AsyncRelayCommand(() => CaptureAsync(row, chip!.Index)));
                row.Chips.Add(chip);
            }

            row.CanAdd = row.Chips.Count < BindingEdits.MaxPerAction;
        }
    }

    /// <summary>Capture une entrée pour la ligne : ajoutée à la fin, ou à la place de la pastille n° <paramref name="replaceIndex"/>.</summary>
    private async Task CaptureAsync(BindingRowViewModel row, int? replaceIndex)
    {
        if (IsCapturing)
            return;

        var keyboard = IsKeyboard;
        if (!keyboard && _input.Controllers.Count == 0)
        {
            Message = Tr.Get("win_buttons_no_controller");
            return;
        }

        using var capture = new CancellationTokenSource(CaptureTimeout);
        _capture = capture;
        _cancelledByUser = false;
        row.IsCapturing = true;
        Message = Tr.Format(keyboard ? "win_buttons_capture_key" : "win_buttons_capture_button", row.Label);
        try
        {
            if (keyboard)
            {
                var key = await _keyboard.CaptureAsync(capture.Token);
                Message = key is { } pressed ? await ApplyAsync(row, replaceIndex, KeyboardInput.NameOf(pressed)) : Tr.Get("win_buttons_cancelled");
            }
            else
            {
                var captured = await _input.CaptureAsync(capture.Token);
                Message = await ApplyAsync(row, replaceIndex, captured.Binding.Normalized(), captured.Controller.Name);
            }
        }
        catch (OperationCanceledException)
        {
            Message = Tr.Get(_cancelledByUser ? "win_buttons_cancelled" : "win_buttons_timeout");
        }
        finally
        {
            _capture = null;
            row.IsCapturing = false;
        }

        FocusRequested?.Invoke(this, replaceIndex is { } index && index < row.Chips.Count ? row.Chips[index] : row);
    }

    private void CancelCapture()
    {
        _cancelledByUser = true;
        _capture?.Cancel();
    }

    /// <summary>Touche capturée : ajoutée ou remplacée si les garde-fous le permettent ; renvoie le message à afficher.</summary>
    private async Task<string?> ApplyAsync(BindingRowViewModel row, int? replaceIndex, string key)
    {
        var keys = KeyboardInput.Normalize(_settings.Current.Input.GetKeys());
        var edit = replaceIndex is { } index ? BindingEdits.Replace(keys, row.Action, index, key) : BindingEdits.Add(keys, row.Action, key);
        if (edit.Status == BindingEditStatus.Done)
            await SaveAsync(s => s with { Input = s.Input with { Keys = edit.Bindings } });
        return Explain(edit);
    }

    /// <summary>Entrée de manette capturée : ajoutée ou remplacée si les garde-fous le permettent.</summary>
    private async Task<string?> ApplyAsync(BindingRowViewModel row, int? replaceIndex, InputBinding binding, string controllerName)
    {
        var bindings = ControllerBindings();
        var edit = replaceIndex is { } index ? BindingEdits.Replace(bindings, row.Action, index, binding) : BindingEdits.Add(bindings, row.Action, binding);
        if (edit.Status == BindingEditStatus.Done)
            await SaveAsync(s => s with { Input = s.Input with { Profile = InputProfile.Custom, Bindings = edit.Bindings, ControllerName = controllerName } });
        return Explain(edit);
    }

    /// <summary>Menu sur une pastille : la liaison est retirée, sauf la dernière d'une action essentielle.</summary>
    private async Task RemoveAsync(BindingChipViewModel chip)
    {
        var row = chip.Row;
        if (IsKeyboard)
        {
            var edit = BindingEdits.Remove(KeyboardInput.Normalize(_settings.Current.Input.GetKeys()), row.Action, chip.Index);
            if (edit.Status == BindingEditStatus.Done)
                await SaveAsync(s => s with { Input = s.Input with { Keys = edit.Bindings } });
            Message = Explain(edit);
        }
        else
        {
            var edit = BindingEdits.Remove(ControllerBindings(), row.Action, chip.Index);
            if (edit.Status == BindingEditStatus.Done)
                await SaveAsync(s => s with { Input = s.Input with { Profile = InputProfile.Custom, Bindings = edit.Bindings } });
            Message = Explain(edit);
        }

        FocusRequested?.Invoke(this, row.Chips.Count == 0 ? row : row.Chips[Math.Min(chip.Index, row.Chips.Count - 1)]);
    }

    /// <summary>Liaisons de manette en vigueur, normalisées (comparables entre elles).</summary>
    private Dictionary<InputAction, IReadOnlyList<InputBinding>> ControllerBindings() =>
        _settings.Current.Input.GetControllerBindings()
            .ToDictionary(pair => pair.Key, pair => (IReadOnlyList<InputBinding>)[.. pair.Value.Select(b => b.Normalized())]);

    /// <summary>Son de la modification et message d'un refus (null si elle est faite).</summary>
    private string? Explain<T>(BindingEdit<T> edit)
    {
        _audio.Play(edit.Status == BindingEditStatus.Done ? UiSound.Open : UiSound.Close);
        return edit.Status switch
        {
            BindingEditStatus.AlreadyBound => Tr.Format("win_buttons_already_bound", ActionLabels.Of(edit.Owner!.Value)),
            BindingEditStatus.UsedByOther => Tr.Format("win_buttons_used_by", ActionLabels.Of(edit.Owner!.Value)),
            BindingEditStatus.LastEssential => Tr.Format(IsKeyboard ? "win_buttons_last_key" : "win_buttons_last_button", ActionLabels.Of(edit.Owner!.Value)),
            BindingEditStatus.Full => Tr.Format("win_buttons_full", ActionLabels.Of(edit.Owner!.Value), BindingEdits.MaxPerAction),
            _ => null,
        };
    }

    /// <summary>Enregistre (les réglages en mémoire changent après l'écriture du fichier), puis remet les pastilles à jour.</summary>
    private async Task SaveAsync(Func<AppSettings, AppSettings> update)
    {
        await _settings.TrySaveAsync(update, _logger);
        Refresh();
    }

    private void OnControllersChanged(object? sender, EventArgs e) => UiThread.Run(() => OnPropertyChanged(nameof(DeviceText)));
}
