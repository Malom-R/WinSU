using System.Globalization;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Layout;
using Avalonia.Media.Transformation;
using Avalonia.Threading;

namespace WinSU.App.Controls;

/// <summary>
/// Entrée en glissant depuis la droite (30.14, maquette de l'utilisateur) : un seul code pour les menus de tous les styles
/// (le panneau collé au bord droit) et, dans le Standard, la colonne des jeux quand la console change. Passe par des valeurs
/// locales et des transitions : Avalonia 12 n'anime pas <see cref="Visual.RenderTransform"/> par images clés.
/// </summary>
public static class SlideIn
{
    /// <summary>
    /// Fait arriver <paramref name="target"/> depuis <paramref name="distance"/> DIP sur sa droite, en
    /// <see cref="Motion.Enter"/> ; <paramref name="fade"/> : il apparaît aussi en fondu (quand il passe sous un autre
    /// élément en arrivant).
    /// </summary>
    public static void Play(Visual target, double distance, bool fade = false)
    {
        Hold(target, distance, fade);

        // Les transitions partent de la place de départ posée ci-dessus, à la prochaine image
        Dispatcher.UIThread.Post(() =>
        {
            target.Transitions =
            [
                new TransformOperationsTransition { Property = Visual.RenderTransformProperty, Duration = Motion.Enter, Easing = Motion.Soft },
                new DoubleTransition { Property = Visual.OpacityProperty, Duration = Motion.Enter, Easing = Motion.Soft },
            ];
            target.RenderTransform = TransformOperations.Identity;
            target.Opacity = 1;

            // Arrivé : ce qui dépend de la place à l'écran est recalculé, un glissement de RenderTransform ne refait pas la
            // mise en page (DF-16 : l'atténuation de Covers gardait la tuile du haut des jeux à 10 %, passée sous « Vos
            // consoles » au départ).
            if (target is Layoutable layoutable)
                DispatcherTimer.RunOnce(layoutable.InvalidateArrange, Motion.Enter);
        }, DispatcherPriority.Render);
    }

    /// <summary>
    /// Place <paramref name="target"/> à son point de départ, sans animation : à appeler avant son premier affichage quand
    /// <see cref="Play"/> ne peut partir qu'une fois sa taille connue (sinon il se montre une image à sa place finale).
    /// </summary>
    public static void Hold(Visual target, double distance, bool fade = false)
    {
        target.Transitions = null;
        target.RenderTransform = TransformOperations.Parse(string.Create(CultureInfo.InvariantCulture, $"translateX({distance}px)"));
        if (fade)
            target.Opacity = 0;
    }
}
