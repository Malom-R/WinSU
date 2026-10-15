using System.Globalization;
using System.Reflection;
using Avalonia;
using Avalonia.Media;
using WinSU.App.Hosting;
using WinSU.App.Themes;
using WinSU.Core.Appearance;
using WinSU.Core.Catalog;
using WinSU.Core.Library;
using WinSU.Core.Playtime;
using WinSU.Core.Settings;
using WinSU.Infrastructure.Runtime;
using WinSU.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace WinSU.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        using var instance = SingleInstanceGuard.TryAcquire();
        if (instance is null)
        {
            SingleInstanceGuard.SignalExistingInstance();
            return 0;
        }

        var paths = DataPaths.Resolve(AppContext.BaseDirectory, dataDirOverride: ReadOption(args, "--data-dir"));
        paths.EnsureCreated();
        Log.Logger = LoggingSetup.CreateLogger(paths);
        var log = Log.ForContext(typeof(Program));
        RegisterGlobalExceptionHandlers();

        try
        {
            log.Information(
                "WinSU {Version} démarre ; données : {DataRoot} (portable : {IsPortable})",
                typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                paths.Root,
                paths.IsPortable);

            using var host = AppHost.Build(args, paths);
            var settings = host.Services.GetRequiredService<ISettingsStore>();
            settings.LoadAsync().GetAwaiter().GetResult();
            ApplyUiCulture(settings.Current.General.Language);
            host.Services.GetRequiredService<IConsoleCatalog>().LoadAsync().GetAwaiter().GetResult();
            host.Services.GetRequiredService<ILibraryService>().LoadAsync().GetAwaiter().GetResult();
            host.Services.GetRequiredService<IThemeStore>().LoadAsync().GetAwaiter().GetResult();
            host.Services.GetRequiredService<IPlaytimeService>().InitializeAsync().GetAwaiter().GetResult();

            host.StartAsync().GetAwaiter().GetResult();
            var exitCode = BuildAvaloniaApp(() => new App(host.Services, instance))
                .StartWithClassicDesktopLifetime(args);
            host.StopAsync().GetAwaiter().GetResult();

            log.Information("WinSU s'arrête (code {ExitCode})", exitCode);
            return exitCode;
        }
        catch (Exception ex)
        {
            log.Fatal(ex, "Arrêt sur une erreur non gérée");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    /// <summary>Utilisé par le prévisualiseur XAML (constructeur sans paramètre de <see cref="App"/>).</summary>
    public static AppBuilder BuildAvaloniaApp() => BuildAvaloniaApp(() => new App());

    private static AppBuilder BuildAvaloniaApp(Func<App> createApp) =>
        AppBuilder.Configure(createApp)
            .UsePlatformDetect()
            // Lettres absentes d'une police (la police des glyphes de boutons n'a que ses glyphes) : celles de DM Sans
            .With(new FontManagerOptions { FontFallbacks = [new FontFallback { FontFamily = new FontFamily(AppFonts.DmSansUri) }] })
            .LogToTrace();

    private static void RegisterGlobalExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Log.Fatal(e.ExceptionObject as Exception, "Exception non gérée (fin du processus : {IsTerminating})", e.IsTerminating);
            Log.CloseAndFlush();
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "Exception d'une tâche non observée");
            e.SetObserved();
        };
    }


    private static void ApplyUiCulture(string language)
    {
        CultureInfo culture;
        try
        {
            culture = CultureInfo.GetCultureInfo(language);
        }
        catch (CultureNotFoundException)
        {
            Log.Warning("Langue {Language} inconnue, anglais utilisé", language);
            culture = CultureInfo.GetCultureInfo("en");
        }

        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    private static string? ReadOption(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
