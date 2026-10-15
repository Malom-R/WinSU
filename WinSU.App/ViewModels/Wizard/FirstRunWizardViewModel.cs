using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinSU.App.Input;
using WinSU.App.Localization;
using WinSU.App.Navigation;
using WinSU.App.Services;
using WinSU.App.ViewModels.Shell;
using WinSU.Core.Input;
using WinSU.Core.Settings;
using Microsoft.Extensions.Logging;

namespace WinSU.App.ViewModels.Wizard;

public enum WizardStep
{
    Welcome,
    Mapping,
    Test,
    Theme,
    Done,
}

/// <summary>
/// Assistant de premier lancement (D-5, notes 3.7b) : détection de la manette, mapping action par action,
/// écran de test, thème. Seulement au premier lancement depuis l'étape 30 (30.10 : plus de « Configurer la manette »
/// dans les Réglages, ni d'étape ES-DE) ; le cas « lancé depuis les Réglages » (D-44 : valider le test y ramène) reste
/// dans le code sans être atteint. Le clavier reste utilisable partout : Échap pendant la détection
/// ou le mapping applique la disposition standard, aussi proposée à l'étape de test (garde-fou de M11).
/// Depuis M11, le mapping remplit la première liaison de chaque action : les autres (le stick à côté de la croix,
/// celles ajoutées dans l'écran « Boutons ») restent.
/// </summary>
public sealed partial class FirstRunWizardViewModel : ViewModelBase, IInputHandler
{
    public static readonly InputAction[] MappingOrder =
    [
        InputAction.Up, InputAction.Down, InputAction.Left, InputAction.Right,
        InputAction.Confirm, InputAction.Back, InputAction.ContextMenu, InputAction.Start,
        InputAction.PagePrevious, InputAction.PageNext,
    ];

    private readonly IInputService _input;
    private readonly ISettingsStore _settings;
    private readonly INavigationService _navigation;
    private readonly ILogger<FirstRunWizardViewModel> _logger;
    private readonly bool _isFirstRun;
    private readonly Dictionary<InputAction, InputBinding> _captured = [];
    private CancellationTokenSource? _capture;
    private bool _initialized;

    public FirstRunWizardViewModel(
        IInputService input,
        ISettingsStore settings,
        INavigationService navigation,
        ILogger<FirstRunWizardViewModel> logger)
    {
        _input = input;
        _settings = settings;
        _navigation = navigation;
        _logger = logger;
        _isFirstRun = !settings.Current.FirstRunCompleted;

        MappingRows = [.. MappingOrder.Select(action => new MappingRowViewModel(action))];
        Indicators = [.. MappingOrder.Select(action => new ActionIndicatorViewModel(action))];
        Pad = new GamepadPreviewViewModel(input, settings);
        ThemeOptions =
        [
            new(ThemePreference.System, Tr.Get("win_theme_system"), Tr.Get("win_theme_system_description")),
            new(ThemePreference.Light, Tr.Get("win_theme_light"), Tr.Get("win_theme_light_description")),
            new(ThemePreference.Dark, Tr.Get("win_theme_dark"), Tr.Get("win_theme_dark_description")),
            .. settings.Current.Appearance is { Theme: ThemePreference.Custom, CustomThemeId: { } id }
                ? [new ThemeOption(ThemePreference.Custom, Tr.Get("win_theme_custom_current"), Tr.Get("win_theme_custom_current_description"), id)]
                : Array.Empty<ThemeOption>(),
        ];
        SelectedTheme = ThemeOptions.FirstOrDefault(o => o.Value == settings.Current.Appearance.Theme) ?? ThemeOptions[0];
        _initialized = true;
    }

    public IReadOnlyList<MappingRowViewModel> MappingRows { get; }

    public IReadOnlyList<ActionIndicatorViewModel> Indicators { get; }

    /// <summary>Aperçu de la manette de l'étape de test (étape 22, D-45).</summary>
    public GamepadPreviewViewModel Pad { get; }

    /// <summary>
    /// Étape de test : la manette (zone d'essai) a le focus ; signalé par la vue. Là, chaque appui ne fait que s'allumer,
    /// et Droite mène aux boutons.
    /// </summary>
    [ObservableProperty]
    public partial bool IsPadFocused { get; set; }

    /// <summary>Droite dans la zone d'essai : la vue donne le focus au bouton « Valider ».</summary>
    public event EventHandler? TestButtonsRequested;

    public IReadOnlyList<ThemeOption> ThemeOptions { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWelcome), nameof(IsMapping), nameof(IsTest), nameof(IsTheme), nameof(IsDone))]
    public partial WizardStep Step { get; set; }

    public bool IsWelcome => Step == WizardStep.Welcome;
    public bool IsMapping => Step == WizardStep.Mapping;
    public bool IsTest => Step == WizardStep.Test;
    public bool IsTheme => Step == WizardStep.Theme;
    public bool IsDone => Step == WizardStep.Done;

    [ObservableProperty]
    public partial string ControllersText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ControllerName { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentActionLabel), nameof(MappingProgress))]
    public partial MappingRowViewModel? CurrentRow { get; set; }

    public string CurrentActionLabel => CurrentRow?.Label ?? string.Empty;

    public string MappingProgress => CurrentRow is null
        ? string.Empty
        : Tr.Format("win_wizard_mapping_progress", Array.IndexOf(MappingOrder, CurrentRow.Action) + 1, MappingOrder.Length);

    [ObservableProperty]
    public partial string? MappingMessage { get; set; }

    /// <summary>Étape de test : « Disposition standard appliquée ».</summary>
    [ObservableProperty]
    public partial string? TestMessage { get; set; }

    [ObservableProperty]
    public partial ThemeOption? SelectedTheme { get; set; }

    public bool InputUnavailable => !_input.IsAvailable;

    public override void OnNavigatedTo()
    {
        _input.ControllersChanged += OnControllersChanged;
        UpdateControllersText();
        GoToWelcome();
    }

    public override void OnNavigatedFrom()
    {
        _input.ControllersChanged -= OnControllersChanged;
        CancelCapture();
        Pad.End();
    }

    public bool HandleAction(InputActionEvent e)
    {
        var keyboard = e.Device == InputDevice.Keyboard;
        switch (Step)
        {
            case WizardStep.Welcome:
            case WizardStep.Mapping:
                // La manette est en capture ; au clavier, Entrée ou Échap continuent sans mapping personnalisé.
                if (keyboard && !e.IsRepeat && (e.Action is InputAction.Back || (IsWelcome && e.Action is InputAction.Confirm)))
                    UseStandardLayout();
                return true;

            case WizardStep.Test:
                Indicators.FirstOrDefault(i => i.Action == e.Action)?.Flash();
                Pad.ShowAction(e);
                if (!IsPadFocused)
                    return e.Action == InputAction.Back; // on ne quitte pas l'assistant par Retour

                // Zone d'essai (D-45) : chaque appui ne fait que s'allumer ; Droite mène aux boutons.
                if (e.Action == InputAction.Right && !e.IsRepeat)
                    TestButtonsRequested?.Invoke(this, EventArgs.Empty);
                return true;

            // Étape du thème : Confirmer choisit la carte qui a le focus, « Continuer » passe à la suite (D-78)
            case WizardStep.Theme when e.Action == InputAction.Back && !e.IsRepeat:
                GoToTest();
                return true;

            case WizardStep.Done when e.Action == InputAction.Back && !e.IsRepeat:
                Step = WizardStep.Theme;
                return true;

            default:
                return false;
        }
    }

    [RelayCommand]
    private void RestartMapping() => GoToMapping();

    /// <summary>Étape de test : revenir à la disposition standard sans refaire le mapping.</summary>
    [RelayCommand]
    private void StandardLayout()
    {
        UseStandardLayout();
        TestMessage = Tr.Get("win_wizard_standard_applied");
    }

    /// <summary>Premier lancement : étape du thème ; depuis les Réglages (D-44) : retour aux Réglages.</summary>
    [RelayCommand]
    private async Task ValidateTestAsync()
    {
        if (_isFirstRun)
            Step = WizardStep.Theme;
        else
            await FinishAsync();
    }

    [RelayCommand]
    private void ValidateTheme() => Step = WizardStep.Done;

    [RelayCommand]
    private async Task FinishAsync()
    {
        await _settings.TrySaveAsync(s => s with { FirstRunCompleted = true }, _logger);
        if (_isFirstRun)
            _navigation.ResetTo<HomeShellViewModel>();
        else
            _navigation.GoBack();
    }

    /// <summary>L'aperçu de la manette ne suit la manette que pendant l'étape de test.</summary>
    partial void OnStepChanged(WizardStep value)
    {
        if (value == WizardStep.Test)
            Pad.Begin();
        else
            Pad.End();
    }

    partial void OnSelectedThemeChanged(ThemeOption? value)
    {
        if (_initialized && value is not null)
            _ = _settings.TrySaveAsync(s => s with { Appearance = s.Appearance with { Theme = value.Value, CustomThemeId = value.CustomThemeId } }, _logger);
    }

    private void GoToWelcome()
    {
        Step = WizardStep.Welcome;
        StartCapture(async cancellationToken =>
        {
            var first = await _input.CaptureAsync(cancellationToken);
            ControllerName = first.Controller.Name;
            GoToMapping();
        });
    }

    private void GoToMapping()
    {
        Step = WizardStep.Mapping;
        _captured.Clear();
        MappingMessage = null;
        TestMessage = null;
        foreach (var row in MappingRows)
            row.Reset();
        StartCapture(RunMappingAsync);
    }

    private async Task RunMappingAsync(CancellationToken cancellationToken)
    {
        foreach (var row in MappingRows)
        {
            row.State = MappingRowState.Current;
            CurrentRow = row;

            while (true)
            {
                var captured = await _input.CaptureAsync(cancellationToken);
                var binding = captured.Binding.Normalized();
                ControllerName ??= captured.Controller.Name;

                var owner = _captured
                    .Where(pair => pair.Value == binding)
                    .Select(pair => (InputAction?)pair.Key)
                    .FirstOrDefault();
                if (owner is { } usedBy)
                {
                    MappingMessage = Tr.Format("win_wizard_mapping_duplicate", ActionLabels.Of(usedBy));
                    continue;
                }

                MappingMessage = null;
                _captured[row.Action] = binding;
                row.Show(ActionLabels.Show(binding, GlyphStyle.From(_settings.Current.Appearance)));
                row.State = MappingRowState.Done;
                break;
            }
        }

        CurrentRow = null;
        var current = _settings.Current.Input.GetControllerBindings()
            .ToDictionary(pair => pair.Key, pair => (IReadOnlyList<InputBinding>)[.. pair.Value.Select(b => b.Normalized())]);
        var bindings = BindingEdits.WithPrimaries(current, _captured);
        await _settings.TrySaveAsync(s => s with
        {
            Input = s.Input with { Profile = InputProfile.Custom, ControllerName = ControllerName, Bindings = bindings },
        }, _logger);
        _logger.LogInformation("Mapping personnalisé enregistré pour {Controller}", ControllerName);
        GoToTest();
    }

    private void UseStandardLayout()
    {
        CancelCapture();
        CurrentRow = null;
        _ = _settings.TrySaveAsync(s => s with
        {
            Input = s.Input with
            {
                Profile = InputProfile.Default,
                ControllerName = ControllerName,
                Bindings = new InputSettings().Bindings,
            },
        }, _logger);
        GoToTest();
    }

    private void GoToTest()
    {
        CancelCapture();
        Step = WizardStep.Test;
    }

    private void StartCapture(Func<CancellationToken, Task> work)
    {
        CancelCapture();
        _capture = new CancellationTokenSource();
        _ = RunCaptureAsync(work, _capture.Token);
    }

    private async Task RunCaptureAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        try
        {
            await work(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Étape quittée (clavier, écran fermé) : rien à faire.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur pendant la capture des boutons");
        }
    }

    private void CancelCapture()
    {
        _capture?.Cancel();
        _capture?.Dispose();
        _capture = null;
    }

    private void OnControllersChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(UpdateControllersText);

    private void UpdateControllersText()
    {
        var names = _input.Controllers.Select(c => c.Name).ToList();
        ControllersText = names.Count == 0
            ? Tr.Get("win_wizard_controllers_none")
            : Tr.Format("win_wizard_controllers_list", string.Join(", ", names));
    }
}
