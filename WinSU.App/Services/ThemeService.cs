using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using WinSU.App.Controls;
using WinSU.App.Themes;
using WinSU.Core.Appearance;
using WinSU.Core.Settings;

namespace WinSU.App.Services;

/// <summary>
/// Applique le thème clair, sombre ou système choisi dans les réglages, ou un thème enregistré (M14), et la police de
/// l'interface (étape 25, <see cref="AppFonts"/>) et la durée du balayage de lumière des tuiles (étape 29,
/// <see cref="TileSheen"/>), et les met à jour à chaud. Un thème enregistré = sa base (Clair ou Sombre) + un dictionnaire de jetons généré (<see cref="ThemeResources"/>),
/// ajouté après <c>Themes/Theme.axaml</c>, + l'accent des commandes Fluent. Pendant l'édition d'un thème,
/// <see cref="Preview"/> l'applique à toute l'application jusqu'à <see cref="EndPreview"/>.
/// </summary>
public sealed class ThemeService(ISettingsStore settings, IThemeStore themes) : IDisposable
{
    private Application? _application;
    private ResourceDictionary? _custom;
    private CustomTheme? _preview;
    private Dictionary<ThemeVariant, Color>? _fluentAccents;

    public void Attach(Application application)
    {
        _application = application;
        _fluentAccents = FluentPalettes()?.ToDictionary(pair => pair.Key, pair => pair.Value.Accent);
        Apply();
        AppFonts.Apply(application.Resources, settings.Current.Appearance.Font);
        ApplyTileSheen(application.Resources, settings.Current.Appearance);
        settings.Changed += OnSettingsChanged;
        themes.Changed += OnThemesChanged;
    }

    public void Dispose()
    {
        settings.Changed -= OnSettingsChanged;
        themes.Changed -= OnThemesChanged;
    }

    /// <summary>Aperçu en direct (éditeur de thèmes) : ce thème s'applique partout, à la place de celui des réglages.</summary>
    public void Preview(CustomTheme theme)
    {
        _preview = theme;
        Apply();
    }

    /// <summary>Fin de l'aperçu : le thème des réglages revient.</summary>
    public void EndPreview()
    {
        _preview = null;
        Apply();
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (e.Previous.Appearance.Theme != e.Current.Appearance.Theme || e.Previous.Appearance.CustomThemeId != e.Current.Appearance.CustomThemeId)
            Dispatcher.UIThread.Post(Apply);
        if (e.Previous.Appearance.Font != e.Current.Appearance.Font && _application is { } application)
            Dispatcher.UIThread.Post(() => AppFonts.Apply(application.Resources, e.Current.Appearance.Font));
        if ((e.Previous.Appearance.TileSheen != e.Current.Appearance.TileSheen
                || e.Previous.Appearance.TileSheenSpeed != e.Current.Appearance.TileSheenSpeed)
            && _application is { } app)
            Dispatcher.UIThread.Post(() => ApplyTileSheen(app.Resources, e.Current.Appearance));
    }

    /// <summary>Balayage de lumière des tuiles (D-90) : zéro s'il est coupé, sinon la durée à la vitesse choisie.</summary>
    private static void ApplyTileSheen(IResourceDictionary resources, AppearanceSettings appearance) =>
        resources[TileSheen.DurationResource] = TileSheen.DurationFor(appearance.TileSheen, appearance.TileSheenSpeed);

    private void OnThemesChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(Apply);

    private void Apply()
    {
        if (_application is null)
            return;

        var appearance = settings.Current.Appearance;
        var custom = _preview ?? (appearance.Theme == ThemePreference.Custom ? themes.Find(appearance.CustomThemeId) : null);
        var resources = _application.Resources;
        if (_custom is not null)
            resources.MergedDictionaries.Remove(_custom);
        _custom = null;
        RestoreFluentAccents();

        if (custom is null)
        {
            // Un thème enregistré introuvable (supprimé à la main) : on suit Windows.
            _application.RequestedThemeVariant = appearance.Theme switch
            {
                ThemePreference.Light => ThemeVariant.Light,
                ThemePreference.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default,
            };
            return;
        }

        var palette = ThemePalette.For(custom);
        var variant = palette.Base == ThemeBase.Light ? ThemeVariant.Light : ThemeVariant.Dark;
        _custom = ThemeResources.Build(palette);
        resources.MergedDictionaries.Add(_custom);
        if (FluentPalettes() is { } fluent && fluent.TryGetValue(variant, out var fluentPalette))
            fluentPalette.Accent = ThemeResources.ToColor(palette["Accent"]);
        _application.RequestedThemeVariant = variant;
    }

    private void RestoreFluentAccents()
    {
        if (_fluentAccents is null || FluentPalettes() is not { } fluent)
            return;

        foreach (var (variant, accent) in _fluentAccents)
        {
            if (fluent.TryGetValue(variant, out var palette) && palette.Accent != accent)
                palette.Accent = accent;
        }
    }

    /// <summary>Palettes du thème Fluent d'<c>App.axaml</c> (accent des champs de saisie, des sélections).</summary>
    private IDictionary<ThemeVariant, ColorPaletteResources>? FluentPalettes() =>
        _application?.Styles.OfType<FluentTheme>().FirstOrDefault()?.Palettes;
}
