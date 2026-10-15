using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace WinSU.App.Controls;

/// <summary>
/// Une liste qui défile sous l'en-tête de son écran (étape 33, demande de l'utilisateur : Ajouter une console, recherche
/// SteamGridDB) : elle occupe toute la hauteur de l'écran, l'en-tête (titre en capsule, état) posé par-dessus ; sa marge
/// intérieure du haut grandit de la hauteur de l'en-tête, la première rangée commence donc dessous, et les rangées
/// précédentes, en défilant, passent sous l'en-tête jusqu'au bord de la fenêtre (atténuées : <see cref="Covers"/>).
/// <para>
/// <see cref="HeaderProperty"/> se pose sur la liste (<c>ctl:HeaderRoom.Header="{Binding #Header}"</c>). L'en-tête et la
/// liste partent du même haut ; la marge du haut donnée par le style de la liste reste l'écart sous l'en-tête. Une liste
/// (contrôle à gabarit) reçoit la place dans sa marge intérieure ; un panneau dans une zone qui défile (les Réglages,
/// étape 34 : sous la ligne de l'en-tête de chaque style), dans sa marge.
/// </para>
/// </summary>
public sealed class HeaderRoom
{
    public static readonly AttachedProperty<Control?> HeaderProperty =
        AvaloniaProperty.RegisterAttached<HeaderRoom, Control, Control?>("Header");

    /// <summary>La marge du haut du style de la liste, retenue à la première mise à jour (après l'application des styles).</summary>
    private static readonly AttachedProperty<double?> GapProperty =
        AvaloniaProperty.RegisterAttached<HeaderRoom, Control, double?>("Gap");

    /// <summary>Le gestionnaire de chaque liste, retrouvé pour le débrancher quand l'en-tête change.</summary>
    private static readonly ConditionalWeakTable<Control, EventHandler<SizeChangedEventArgs>> Handlers = [];

    static HeaderRoom()
    {
        HeaderProperty.Changed.AddClassHandler<Control>(OnHeaderChanged);
    }

    private HeaderRoom()
    {
    }

    public static Control? GetHeader(Control list) => list.GetValue(HeaderProperty);

    public static void SetHeader(Control list, Control? value) => list.SetValue(HeaderProperty, value);

    /// <summary>Marge intérieure du haut d'une liste sous un en-tête de <paramref name="headerHeight"/>, à l'écart <paramref name="gap"/>.</summary>
    public static double TopFor(double headerHeight, double gap) => Math.Max(0, headerHeight) + gap;

    private static void OnHeaderChanged(Control list, AvaloniaPropertyChangedEventArgs e)
    {
        var handler = Handlers.GetValue(list, l => (sender, _) =>
        {
            if (sender is Control header)
                Update(l, header);
        });
        if (e.OldValue is Control old)
            old.SizeChanged -= handler;
        // La première mise à jour vient de la première mise en page de l'en-tête (les styles de la liste sont alors posés),
        // ou tout de suite s'il est déjà mis en page
        if (e.NewValue is Control header)
        {
            header.SizeChanged += handler;
            if (header.Bounds.Height > 0)
                Update(list, header);
        }
    }

    private static void Update(Control list, Control header)
    {
        var current = list is TemplatedControl templated ? templated.Padding : list.Margin;
        var gap = list.GetValue(GapProperty) ?? current.Top;
        list.SetValue(GapProperty, gap);
        var top = TopFor(header.Bounds.Height + header.Margin.Top + header.Margin.Bottom, gap);
        if (Math.Abs(current.Top - top) <= 0.5)
            return;

        var room = new Thickness(current.Left, top, current.Right, current.Bottom);
        if (list is TemplatedControl control)
            control.Padding = room;
        else
            list.Margin = room;
    }
}
