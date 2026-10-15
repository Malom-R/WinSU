using Avalonia.Animation.Easings;

namespace WinSU.App.Controls;

/// <summary>
/// Les vitesses de l'interface, les mêmes pour tous les styles (30.11). La référence est la sélection des styles Switch et
/// Wii U : une tuile est choisie en <see cref="Select"/> (équerres, verre rempli, image réduite), puis finit de grossir avec
/// un petit rebond (<see cref="Grow"/>). Les files du Standard glissent d'un cran au même tempo (<see cref="Slide"/>).
/// Les styles (axaml) y renvoient par <c>{x:Static ctl:Motion.…}</c> ; aucune durée ne s'écrit ailleurs.
/// </summary>
public static class Motion
{
    /// <summary>Courbe douce du site (glissements, fondus, apparitions).</summary>
    public static Easing Soft { get; } = new SplineEasing(0.25, 0.45, 0.5, 1);

    /// <summary>Courbe à petit rebond du site (tuile qui grossit, retour après un appui).</summary>
    public static Easing Spring { get; } = new SplineEasing(0.34, 1.56, 0.64, 1);

    /// <summary>
    /// Une tuile ou une commande devient la choisie : équerres, verre rempli, image réduite dans son cadre ; aussi
    /// l'apparition d'une boîte de dialogue.
    /// </summary>
    public static readonly TimeSpan Select = TimeSpan.FromSeconds(0.12);

    /// <summary>
    /// Une file du Standard glisse d'un cran (accueil en diagonale, colonnes des Jeux) : au tempo de la sélection, si bien
    /// que la tuile arrive au point fixe au moment où son image finit de se réduire.
    /// </summary>
    public static readonly TimeSpan Slide = Select;

    /// <summary>La tuile choisie grossit (avec <see cref="Spring"/>).</summary>
    public static readonly TimeSpan Grow = TimeSpan.FromSeconds(0.3);

    /// <summary>Une commande (bouton, carte, interrupteur, curseur) grossit ou change de couleur au focus.</summary>
    public static readonly TimeSpan Control = TimeSpan.FromSeconds(0.2);

    /// <summary>
    /// Un panneau arrive en glissant depuis la droite (<see cref="SlideIn"/>) : les menus, et dans le Standard la colonne des
    /// jeux quand la console change (30.14).
    /// </summary>
    public static readonly TimeSpan Enter = TimeSpan.FromSeconds(0.22);

    /// <summary>La grille du Wii U passe d'une page à l'autre.</summary>
    public static readonly TimeSpan Page = TimeSpan.FromSeconds(0.35);

    /// <summary>Une nouvelle vue apparaît (<see cref="PageFade"/>).</summary>
    public static readonly TimeSpan PageFadeIn = TimeSpan.FromSeconds(0.15);

    /// <summary>La bannière héro passe d'une image à l'autre.</summary>
    public static readonly TimeSpan HeroFade = TimeSpan.FromSeconds(0.4);

    /// <summary>Appui (Confirmer ou clic) : la tuile se resserre…</summary>
    public static readonly TimeSpan PressIn = TimeSpan.FromMilliseconds(80);

    /// <summary>… puis revient, avec <see cref="Spring"/>.</summary>
    public static readonly TimeSpan Release = TimeSpan.FromMilliseconds(300);

    /// <summary>L'éclair blanc de l'appui s'efface.</summary>
    public static readonly TimeSpan Flash = TimeSpan.FromMilliseconds(200);

    /// <summary>Le balayage de lumière sur la tuile choisie, à la vitesse normale (le réglage le divise).</summary>
    public static readonly TimeSpan Sheen = TimeSpan.FromSeconds(0.7);
}
