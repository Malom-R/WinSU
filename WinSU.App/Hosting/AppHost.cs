using WinSU.App.Input;
using WinSU.App.Navigation;
using WinSU.App.Overlays;
using WinSU.App.Services;
using WinSU.App.ViewModels;
using WinSU.App.ViewModels.Shell;
using WinSU.App.ViewModels.Wizard;
using WinSU.App.Views;
using WinSU.Core.Audio;
using WinSU.Core.Appearance;
using WinSU.Core.Catalog;
using WinSU.Core.Input;
using WinSU.Core.Launching;
using WinSU.Core.Library;
using WinSU.Core.Media;
using WinSU.Core.Playtime;
using WinSU.Core.Power;
using WinSU.Core.Settings;
using WinSU.Core.Storage;
using WinSU.Infrastructure.Audio;
using WinSU.Infrastructure.Catalog;
using WinSU.Infrastructure.Input;
using WinSU.Infrastructure.Launching;
using WinSU.Infrastructure.Library;
using WinSU.Infrastructure.Media;
using WinSU.Infrastructure.Playtime;
using WinSU.Infrastructure.Power;
using WinSU.Infrastructure.Storage;
using WinSU.Infrastructure.Win32;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace WinSU.App.Hosting;

internal static class AppHost
{
    public static IHost Build(string[] args, IDataPaths paths)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.Logging.AddSerilog(Log.Logger, dispose: false);

        var services = builder.Services;
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(paths);
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<IAssetLocator>(new AssetLocator(Path.Combine(AppContext.BaseDirectory, "Assets")));
        services.AddSingleton<IConsoleCatalog, JsonConsoleCatalog>();
        services.AddSingleton<ILibraryService, JsonLibraryService>();
        services.AddSingleton<IPlaytimeService, SqlitePlaytimeService>();

        // Lancement des jeux (D-6 à D-9)
        services.AddSingleton<ILaunchTargetResolver, ShortcutLaunchTargetResolver>();
        services.AddSingleton<IProcessLauncher, JobObjectProcessLauncher>();

        // Images des jeux et des applications (M6) : celles choisies, dans le dossier media (ES-DE retiré, 30.10)
        services.AddSingleton<ICustomArtStore, CustomArtStore>();
        services.AddSingleton<IMediaResolver, MediaResolver>();

        // SteamGridDB (étape 31) : un seul HttpClient pour l'application ; la clé est celle de l'utilisateur (Réglages)
        services.AddSingleton(_ => CreateHttpClient());
        services.AddSingleton<IArtworkSearch>(sp => new SteamGridDbClient(
            sp.GetRequiredService<ISettingsStore>(),
            sp.GetRequiredService<HttpClient>(),
            sp.GetRequiredService<ILogger<SteamGridDbClient>>(),
            SteamGridDbAddress(args)));

        // Sons d'interface : démarrés en arrière-plan par l'hôte
        services.AddSingleton<UiSoundPlayer>();
        services.AddSingleton<IAudioService>(sp => sp.GetRequiredService<UiSoundPlayer>());
        services.AddHostedService(sp => sp.GetRequiredService<UiSoundPlayer>());

        // Alimentation (D-18)
        services.AddSingleton<IPowerService, WindowsPowerService>();

        // Manettes : un seul service, exposé comme IInputService et démarré/arrêté par l'hôte
        services.AddSingleton<SdlInputService>();
        services.AddSingleton<IInputService>(sp => sp.GetRequiredService<SdlInputService>());
        services.AddHostedService(sp => sp.GetRequiredService<SdlInputService>());
        // Disposition du clavier de Windows : les lettres du clavier dessiné de l'écran « Boutons » (étape 32)
        services.AddSingleton<IKeyboardLayout, WindowsKeyboardLayout>();

        // Interface
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IOverlayService, OverlayService>();
        services.AddSingleton<IWindowControl, MainWindowControl>();
        services.AddSingleton<ILaunchFilePicker, Win32LaunchFilePicker>();
        services.AddSingleton<IMediaPicker, Win32MediaPicker>();
        services.AddSingleton<EntryImageService>();
        services.AddSingleton<PowerMenu>();
        services.AddSingleton<ActiveUse>();
        services.AddSingleton<BackdropDirector>();
        services.AddSingleton<LaunchCoordinator>();
        services.AddSingleton<IShell, DesktopShell>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<IThemeStore, JsonThemeStore>();
        services.AddSingleton<IImageLoader, ImageLoader>();
        services.AddSingleton<FocusNavigator>();
        services.AddSingleton<InputRouter>();
        services.AddSingleton<KeyboardInput>();
        services.AddSingleton<ViewLocator>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();

        // Écrans : une instance neuve à chaque navigation (les onglets vivent avec l'écran principal)
        services.AddTransient<HomeShellViewModel>();
        services.AddTransient<HomeTabViewModel>();
        services.AddTransient<GamesTabViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<ConsolePickerViewModel>();
        services.AddTransient<ConsoleGamesViewModel>();
        services.AddTransient<Func<ConsoleGamesViewModel>>(provider => provider.GetRequiredService<ConsoleGamesViewModel>); // jeux en direct (étape 30)
        services.AddTransient<FirstRunWizardViewModel>();
        services.AddTransient<ButtonsViewModel>();
        services.AddTransient<ThemeEditorViewModel>();
        services.AddTransient<SteamGridDbSettingsViewModel>();
        services.AddTransient<ArtworkPickerViewModel>();

        return builder.Build();
    }

    private static HttpClient CreateHttpClient()
    {
        // Délais posés à chaque demande par le client (10 s pour l'API, 30 s pour une image)
        var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        var version = typeof(AppHost).Assembly.GetName().Version;
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"WinSU/{version?.ToString(3) ?? "0"}");
        return http;
    }

    /// <summary>
    /// Adresse de l'API SteamGridDB : <c>--sgdb-url</c> la remplace par un faux serveur local, pour les essais
    /// (<c>tools/dev/fake_sgdb.py</c>) ; seulement dans une version Debug.
    /// </summary>
    private static string? SteamGridDbAddress(string[] args)
    {
#if DEBUG
        var index = Array.IndexOf(args, "--sgdb-url");
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
#else
        return null;
#endif
    }
}
