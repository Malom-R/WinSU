using Avalonia.Controls;
using Avalonia.Controls.Templates;
using WinSU.App.Overlays;
using WinSU.App.ViewModels;
using WinSU.App.ViewModels.Shell;
using WinSU.App.ViewModels.Wizard;
using WinSU.Core.Settings;

namespace WinSU.App.Views;

/// <summary>
/// Associe chaque ViewModel à sa vue pour le style d'interface actif (voir notes, D-2) : la vue propre au style, sinon la
/// vue commune aux trois styles (<c>Views/Common</c>, étape 36), sinon celle du Standard (style sans vue pour cet écran).
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    private readonly ISettingsStore _settings;

    // Style null : vue commune à tous les styles.
    private readonly Dictionary<(UiStyle? Style, Type ViewModel), Func<Control>> _factories = [];

    public ViewLocator(ISettingsStore settings)
    {
        _settings = settings;

        // Écrans communs (Réglages compris, étape 32 ; D-57 pour le Wii U)
        Register<ConsolePickerViewModel, Common.ConsolePickerView>();
        Register<FirstRunWizardViewModel, Common.FirstRunWizardView>();
        Register<ButtonsViewModel, Common.ButtonsView>();
        Register<ThemeEditorViewModel, Common.ThemeEditorView>();
        Register<ArtworkPickerViewModel, Common.ArtworkPickerView>(); // SteamGridDB (étape 31)
        Register<SettingsViewModel, Common.SettingsView>();

        // Fenêtres superposées communes
        Register<ContextMenuViewModel, Common.ContextMenuView>();
        Register<ConfirmDialogViewModel, Common.ConfirmDialogView>();
        Register<TextInputDialogViewModel, Common.TextInputDialogView>();

        // Style Standard : l'écran principal, ses onglets et les jeux d'une console
        Register<HomeShellViewModel, Standard.HomeShellView>(UiStyle.Standard);
        Register<HomeTabViewModel, Standard.HomeTabView>(UiStyle.Standard);
        Register<GamesTabViewModel, Standard.GamesTabView>(UiStyle.Standard);
        Register<ConsoleGamesViewModel, Standard.ConsoleGamesView>(UiStyle.Standard);

        // Style Switch (M15) : l'écran principal et ses pages
        Register<HomeShellViewModel, Switch.HomeShellView>(UiStyle.Switch);
        Register<HomeTabViewModel, Switch.HomeTabView>(UiStyle.Switch);
        Register<GamesTabViewModel, Switch.GamesTabView>(UiStyle.Switch);
        Register<ConsoleGamesViewModel, Switch.ConsoleGamesView>(UiStyle.Switch);

        // Style Wii U (M16) : l'écran principal, ses grilles et les jeux d'une console
        Register<HomeShellViewModel, WiiU.HomeShellView>(UiStyle.WiiU);
        Register<HomeTabViewModel, WiiU.HomeTabView>(UiStyle.WiiU);
        Register<GamesTabViewModel, WiiU.GamesTabView>(UiStyle.WiiU);
        Register<ConsoleGamesViewModel, WiiU.ConsoleGamesView>(UiStyle.WiiU);
    }

    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var type = param.GetType();
        var style = _settings.Current.Appearance.Style;
        if (_factories.TryGetValue((style, type), out var factory) ||
            _factories.TryGetValue((null, type), out factory) ||
            _factories.TryGetValue((UiStyle.Standard, type), out factory))
        {
            return factory();
        }

        return new TextBlock { Text = $"Vue manquante : {type.Name}" };
    }

    public bool Match(object? data) => data is ViewModelBase;

    private void Register<TViewModel, TView>(UiStyle? style = null)
        where TViewModel : ViewModelBase
        where TView : Control, new() =>
        _factories[(style, typeof(TViewModel))] = static () => new TView();
}
