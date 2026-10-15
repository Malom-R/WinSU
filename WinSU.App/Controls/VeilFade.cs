using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Threading;

namespace WinSU.App.Controls;

/// <summary>
/// Un voile qui assombrit l'écran, puis son contenu qui apparaît (D-182, fenêtre « Jeu en cours ») ; à la sortie, l'inverse :
/// le contenu disparaît, puis le voile s'éclaircit et se retire. Pour l'œil seulement : ce que le voile bloque (clics,
/// entrées) se règle à part, tout de suite. Voile en <see cref="Motion.Page"/>, contenu en <see cref="Motion.Select"/> ;
/// une demande qui arrive pendant l'autre repart de là où en est le fondu.
/// </summary>
public sealed class VeilFade(Control veil, Control content)
{
    private int _version;

    public void Show()
    {
        var version = ++_version;
        if (!veil.IsVisible)
        {
            Jump(veil, 0);
            Jump(content, 0);
            veil.IsVisible = true;
        }

        // Les transitions partent de l'opacité posée ci-dessus, à la prochaine image
        Dispatcher.UIThread.Post(() =>
        {
            if (version == _version)
                FadeTo(veil, 1, Motion.Page);
        }, DispatcherPriority.Render);
        DispatcherTimer.RunOnce(() =>
        {
            if (version == _version)
                FadeTo(content, 1, Motion.Select);
        }, Motion.Page);
    }

    public void Hide()
    {
        var version = ++_version;
        if (!veil.IsVisible)
            return;

        FadeTo(content, 0, Motion.Select);
        DispatcherTimer.RunOnce(() =>
        {
            if (version == _version)
                FadeTo(veil, 0, Motion.Page);
        }, Motion.Select);
        DispatcherTimer.RunOnce(() =>
        {
            if (version == _version)
                veil.IsVisible = false;
        }, Motion.Select + Motion.Page);
    }

    private static void FadeTo(Control control, double opacity, TimeSpan duration)
    {
        control.Transitions = [new DoubleTransition { Property = Visual.OpacityProperty, Duration = duration, Easing = Motion.Soft }];
        control.Opacity = opacity;
    }

    private static void Jump(Control control, double opacity)
    {
        control.Transitions = null;
        control.Opacity = opacity;
    }
}
