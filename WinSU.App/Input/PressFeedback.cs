using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WinSU.App.Controls;

namespace WinSU.App.Input;

/// <summary>
/// Appui (étape 29, D-87, descriptif du thème Wii U) : Confirmer (<see cref="FocusNavigator.ActivateFocused"/>) ou un clic
/// sur une tuile, une carte ou un bouton de nos styles le fait rétrécir à <see cref="TileMetrics.PressScale"/> en 80 ms
/// (courbe d'entrée), puis revenir avec le rebond du site à sa valeur de style (grossi au focus) ; l'éclair blanc d'une
/// tuile (<c>Border.tileFlash</c>) passe de 50 % à rien en 200 ms. Une tuile rétrécit par sa boîte (<c>Panel.tileArt</c>),
/// le reste par lui-même. Le rétrécissement passe par des valeurs locales (transitions et transformation) rendues aux
/// styles à la fin : Avalonia 12 n'anime pas <see cref="Visual.RenderTransform"/> par images clés.
/// </summary>
public static class PressFeedback
{
    public const double FlashOpacity = 0.5;

    /// <summary>Boutons qui réagissent à l'appui (classes de nos styles).</summary>
    private static readonly string[] Buttons = ["menu", "chip", "round", "wiiuIcon", "tileButton"];

    /// <summary>Listes dont les éléments réagissent à l'appui : grilles de tuiles, rangées de cartes.</summary>
    private static readonly string[] Lists = ["tiles", "cards"];

    /// <summary>Appui en cours par élément : un nouvel appui remplace le précédent (ses minuteries ne font plus rien).</summary>
    private static readonly ConditionalWeakTable<Visual, object> Running = new();

    private static bool _registered;

    /// <summary>Les clics : un appui du bouton gauche sur un élément de liste ou un bouton (même déjà traité).</summary>
    public static void Register()
    {
        if (_registered)
            return;

        _registered = true;
        InputElement.PointerPressedEvent.AddClassHandler<ListBoxItem>(OnPointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
        InputElement.PointerPressedEvent.AddClassHandler<Button>(OnPointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>Un bouton porte l'une des classes qui réagissent à l'appui.</summary>
    public static bool IsPressableButton(IEnumerable<string> classes) => classes.Any(Buttons.Contains);

    /// <summary>Une liste porte l'une des classes dont les éléments réagissent à l'appui.</summary>
    public static bool IsPressableList(IEnumerable<string> classes) => classes.Any(Lists.Contains);

    public static bool IsPressable(Control control) => control switch
    {
        ListBoxItem item => ItemsControl.ItemsControlFromItemContainer(item) is { } list && IsPressableList(list.Classes),
        Button button => IsPressableButton(button.Classes),
        _ => false,
    };

    /// <summary>Joue l'appui sur l'élément s'il y réagit (sinon rien).</summary>
    public static void Play(Control control)
    {
        if (!IsPressable(control))
            return;

        var art = control.GetVisualDescendants().OfType<Panel>().FirstOrDefault(p => p.Classes.Contains("tileArt"));
        Visual target = (Visual?)art ?? control;
        Shrink(target);
        if (art?.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("tileFlash")) is { } flash)
            _ = FlashAsync(flash);
    }

    private static void OnPointerPressed(Control control, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
            Play(control);
    }

    private static void Shrink(Visual target)
    {
        var token = new object();
        Running.AddOrUpdate(target, token);
        target.Transitions = [new TransformOperationsTransition { Property = Visual.RenderTransformProperty, Duration = Motion.PressIn, Easing = new CubicEaseIn() }];
        target.RenderTransform = TileMetrics.PressTransform;
        DispatcherTimer.RunOnce(() =>
        {
            if (!IsCurrent(target, token))
                return;

            // Retour à la valeur des styles (grossi s'il a le focus), avec le rebond du site
            target.Transitions = [new TransformOperationsTransition { Property = Visual.RenderTransformProperty, Duration = Motion.Release, Easing = Motion.Spring }];
            target.ClearValue(Visual.RenderTransformProperty);
            DispatcherTimer.RunOnce(() =>
            {
                if (!IsCurrent(target, token))
                    return;

                target.ClearValue(Animatable.TransitionsProperty);
                Running.Remove(target);
            }, Motion.Release);
        }, Motion.PressIn);
    }

    private static bool IsCurrent(Visual target, object token) => Running.TryGetValue(target, out var current) && ReferenceEquals(current, token);

    private static async Task FlashAsync(Border flash)
    {
        var animation = new Animation
        {
            Duration = Motion.Flash,
            Easing = new CubicEaseOut(),
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(Visual.OpacityProperty, FlashOpacity) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(Visual.OpacityProperty, 0d) } },
            },
        };
        await animation.RunAsync(flash);
    }
}
