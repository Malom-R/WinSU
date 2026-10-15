using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using WinSU.App.Input;
using WinSU.App.Navigation;
using WinSU.App.Services;
using WinSU.App.ViewModels;
using WinSU.App.ViewModels.Shell;
using WinSU.App.ViewModels.Wizard;
using WinSU.Core.Input;
using WinSU.Core.Settings;
using WinSU.App.Views;
using WinSU.Infrastructure.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace WinSU.App;

public partial class App : Application
{
    private readonly IServiceProvider? _services;
    private readonly SingleInstanceGuard? _instance;

    /// <summary>Constructeur du prévisualiseur XAML (aucun service).</summary>
    public App()
    {
    }

    public App(IServiceProvider services, SingleInstanceGuard instance)
    {
        _services = services;
        _instance = instance;
    }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (_services is not null && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DataTemplates.Add(_services.GetRequiredService<ViewLocator>());
            _services.GetRequiredService<ThemeService>().Attach(this);
            PressFeedback.Register(); // appui au clic sur les tuiles, cartes et boutons (étape 29)

            var window = _services.GetRequiredService<MainWindow>();
            desktop.MainWindow = window;
            _instance?.ListenForActivation(() => Dispatcher.UIThread.Post(() => window.BringToFront()));
            _services.GetRequiredService<ActiveUse>().Start(); // le fond animé suit la fenêtre et les lancements

            // Manette → même aiguillage que le clavier, sur le thread d'interface, seulement si WinSU est au premier plan.
            var router = _services.GetRequiredService<InputRouter>();
            PointerConfirm.Register(router); // 2e clic sur l'élément choisi = Confirmer (29.15)
            _services.GetRequiredService<IInputService>().ActionTriggered += (_, e) =>
                Dispatcher.UIThread.Post(() =>
                {
                    if (window.IsActive)
                        router.Route(e);
                });

            var navigation = _services.GetRequiredService<INavigationService>();
            if (_services.GetRequiredService<ISettingsStore>().Current.FirstRunCompleted)
                navigation.ResetTo<HomeShellViewModel>();
            else
                navigation.ResetTo<FirstRunWizardViewModel>();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
