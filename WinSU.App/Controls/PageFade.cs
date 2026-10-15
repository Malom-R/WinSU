using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Threading;

namespace WinSU.App.Controls;

/// <summary>
/// Fondu d'entrée d'un nouvel écran (D-74) : posé sur un <see cref="ContentControl"/> (page de la fenêtre, page ou onglet
/// de l'écran principal), il cache chaque nouvelle vue et la fait apparaître en 0,15 s, le temps qu'elle se construise
/// (listes remplies, focus posé, images chargées), au lieu de la montrer en chantier pendant une ou deux images.
/// </summary>
public sealed class PageFade
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<PageFade, ContentControl, bool>("IsEnabled");

    static PageFade() => IsEnabledProperty.Changed.AddClassHandler<ContentControl>((host, e) =>
    {
        if (e.NewValue is true)
            WatchView(host, (presenter, _) => FadeIn(presenter));
    });

    private PageFade()
    {
    }

    public static bool GetIsEnabled(ContentControl control) => control.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(ContentControl control, bool value) => control.SetValue(IsEnabledProperty, value);

    /// <summary>
    /// Appelle <paramref name="changed"/> chaque fois que <paramref name="host"/> change de vue, aussitôt : le présentateur
    /// crée la vue dès que son contenu change, avant la mise en page et le premier affichage de la nouvelle vue.
    /// </summary>
    public static void WatchView(ContentControl host, Action<ContentPresenter, Control?> changed)
    {
        ContentPresenter? watched = null;

        void OnPresenterPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ContentPresenter.ChildProperty && sender is ContentPresenter presenter)
                changed(presenter, e.NewValue as Control);
        }

        void Watch()
        {
            // Le présentateur n'existe qu'une fois le gabarit appliqué (première mise en page).
            if (host.Presenter is not { } presenter || presenter == watched)
                return;

            if (watched is not null)
                watched.PropertyChanged -= OnPresenterPropertyChanged;
            watched = presenter;
            presenter.PropertyChanged += OnPresenterPropertyChanged;
            if (presenter.Child is { } child)
                changed(presenter, child);
        }

        host.TemplateApplied += (_, _) => Watch();
        Watch();
    }

    private static void FadeIn(ContentPresenter presenter)
    {
        // Sans transition le temps de cacher la vue, puis fondu jusqu'à l'opacité pleine (courbe douce du site).
        presenter.Transitions = null;
        presenter.Opacity = 0;
        Dispatcher.UIThread.Post(() =>
        {
            presenter.Transitions =
            [
                new DoubleTransition { Property = Visual.OpacityProperty, Duration = Motion.PageFadeIn, Easing = Motion.Soft },
            ];
            presenter.Opacity = 1;
        }, DispatcherPriority.Loaded);
    }
}
