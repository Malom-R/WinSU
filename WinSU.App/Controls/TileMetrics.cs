using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Transformation;

namespace WinSU.App.Controls;

/// <summary>
/// Mesures des tuiles (modèle : l'ancien onglet Apps), et espacements calculés à partir du facteur de grossissement du focus,
/// pour qu'une tuile grossie ne touche jamais ses voisines, son nom ni le bord de la zone qui défile.
/// <para>
/// Une tuile de côté S grossie d'un facteur k (depuis son centre) déborde de S·(k − 1)/2 de chaque côté ; ses équerres,
/// dessinées à o de la boîte et grossies avec elle, s'en éloignent de o·k. Débordement : D(S) = S·(k − 1)/2 + o·k.
/// Espace réservé de chaque côté : E = ⌈D(S max) + écart⌉, pour que l'écart minimal reste libre autour de la tuile la plus
/// grande (les consoles), sans descendre sous l'espacement de la disposition (15). La lueur de l'anneau des commandes, qui
/// s'estompe, a en plus sa marge au bord de la zone qui défile.
/// </para>
/// <para>
/// Identité de WinSU (étape 29, descriptif du thème Wii U) : la face (verre permanent, icône opaque par-dessus) a un rayon
/// de 18. La tuile choisie (29.14, d'après la Wii U) : quatre équerres autour de la face, son verre rempli de la couleur
/// du thème, son image réduite dans ce cadre. Grossissement 1,06 au focus, 1,03 au survol, 0,95 à l'appui.
/// </para>
/// Utilisé par Themes/Controls.axaml, les vues à tuiles ({x:Static}) et <see cref="Input.FocusNavigator"/>.
/// </summary>
public static class TileMetrics
{
    /// <summary>Facteur de grossissement de la tuile qui a le focus.</summary>
    public const double FocusScale = 1.06;

    /// <summary>Facteur de grossissement d'une tuile sous la souris (D-87).</summary>
    public const double HoverScale = 1.03;

    /// <summary>Facteur de la tuile pendant l'appui (Confirmer ou clic), avant son retour (D-87).</summary>
    public const double PressScale = 0.95;

    /// <summary>Côté des tuiles des jeux (la boîte d'un jeu).</summary>
    public const double GameSize = 160;

    /// <summary>Côté des tuiles de consoles : 10 % de plus que les jeux.</summary>
    public const double ConsoleSize = 176;

    /// <summary>Retrait de la face, de l'ombre et du liseré (ou du cadre de la console) dans la boîte de la tuile.</summary>
    public const double FaceInset = 4;

    /// <summary>Rayon de la face : verre de la tuile et icône posée dessus, au même rayon (étape 29).</summary>
    public const double FaceRadius = 18;

    /// <summary>Bord lumineux du verre de la tuile (Controls/TileGlass).</summary>
    public const double GlassStroke = 1.5;

    /// <summary>
    /// Boîte des icônes (icône Windows d'un programme, image .ico) : l'icône y est entière et centrée, entourée d'une
    /// zone de respiration neutre jusqu'au bord de la tuile (règle d'intégration des icônes, D-25).
    /// </summary>
    public const double IconSize = 90;

    /// <summary>Part de la face occupée par l'image d'une console, centrée (réduite de 10 %, D-24).</summary>
    public const double ConsoleArtScale = 0.9;

    /// <summary>Côté de la vignette de la console dans l'en-tête de son écran (face sans retrait).</summary>
    public const double ConsoleHeaderSize = 110;

    /// <summary>Signe d'une tuile « + », rapporté au côté de sa boîte (<see cref="AddGlyphSize"/>).</summary>
    public const double AddGlyphScale = 0.48;

    /// <summary>
    /// Épaisseur de l'anneau de focus des commandes (boutons, cartes, pastilles : <see cref="ControlRingBorder"/> +
    /// <see cref="OutlineSpread"/>) : celle du retrait de la face. Les tuiles, elles, ont les équerres de
    /// <see cref="TileCursor"/> (D-96).
    /// </summary>
    public const double RingThickness = FaceInset;

    /// <summary>
    /// Épaisseur des équerres de la tuile choisie : un « L » plein, relevé sur le curseur de la Wii U (≈ 5,4 % du côté de
    /// la tuile).
    /// </summary>
    public const double CursorThickness = 8;

    /// <summary>Jour entre la face de la tuile et ses équerres.</summary>
    public const double CursorGap = 1;

    /// <summary>
    /// Débord des équerres au dehors de la boîte de la tuile : elles longent la face (retrait de 4) à un jour de 1, donc
    /// 8 + 1 − 4 = 5.
    /// </summary>
    public const double CursorOutset = CursorThickness + CursorGap - FaceInset;

    /// <summary>Longueur de chaque bras d'une équerre, depuis le coin extérieur (≈ 15 % du côté, comme la Wii U).</summary>
    public const double CursorArm = 24;

    /// <summary>Rayon du coin extérieur des équerres : un coin serré, plus que celui de la face (Wii U).</summary>
    public const double CursorRadius = 7;

    /// <summary>
    /// Image de la tuile choisie (D-95) : réduite dans le cadre de la couleur du thème qui la remplit, comme la Wii U
    /// (relevé : l'image occupe ≈ 82 % du cadre).
    /// </summary>
    public const double SelectedArtScale = 0.82;

    /// <summary>Rayon de flou de la lueur de l'anneau (WinSU.FocusGlow dans Themes/Theme.axaml : 0 0 18 2).</summary>
    public const double GlowRadius = 18;

    /// <summary>Étalement de la lueur de l'anneau.</summary>
    public const double GlowSpread = 2;

    /// <summary>
    /// Bordure des commandes (boutons, cartes, pastilles) : au focus, elle prend la couleur de l'anneau, et un contour net
    /// la prolonge au dehors (<see cref="OutlineSpread"/>, WinSU.FocusOutline) jusqu'à <see cref="RingThickness"/>,
    /// sans que rien ne bouge (bloc 4 de l'étape 29).
    /// </summary>
    public const double ControlRingBorder = 2;

    /// <summary>Contour net au dehors de la bordure d'une commande qui a le focus : 4 − 2.</summary>
    public const double OutlineSpread = RingThickness - ControlRingBorder;

    /// <summary>Écart minimal qui reste libre entre une tuile grossie (équerres comprises) et ce qui l'entoure.</summary>
    public const double Clearance = 4;

    /// <summary>
    /// Espacement de la disposition (v1.5 : 15 de chaque côté), gardé quand le grossissement et les équerres en demandent
    /// moins (étape 29 : on ne change pas la disposition).
    /// </summary>
    public const double LayoutRoom = 15;

    /// <summary>
    /// Largeur visée d'une case des grilles du style Switch (Jeux, jeux d'une console) : celle des 6 colonnes posées
    /// sur les deux tiers d'une fenêtre de 1293 de large (M15). La grille occupe désormais toute la largeur, avec la même
    /// marge des deux côtés, et le nombre de colonnes en découle (D-69 : 9 dans cette fenêtre).
    /// </summary>
    public const double SwitchGridCell = 136;

    /// <summary>Débordement D(S) d'une tuile de côté S grossie, équerres comprises, de chaque côté.</summary>
    public static double Overflow(double size) => (size * (FocusScale - 1) / 2) + (CursorOutset * FocusScale);

    /// <summary>
    /// Espace E réservé de chaque côté d'une tuile : ⌈D(plus grande tuile) + écart⌉, au moins l'espacement de la
    /// disposition (15 aujourd'hui : le calcul donne 10).
    /// </summary>
    public static double Room { get; } = Math.Max(LayoutRoom, Math.Ceiling(Overflow(ConsoleSize) + Clearance));

    /// <summary>Entre la tuile et son nom.</summary>
    public static double LabelSpacing => Room;

    /// <summary>Marge intérieure d'un élément de grille (la boîte de l'élément contient la tuile grossie et ses équerres).</summary>
    public static Thickness ItemPadding { get; } = new(Room);

    /// <summary>Portée de la lueur de l'anneau autour de lui : flou et étalement (20).</summary>
    public static double GlowExtent => GlowRadius + GlowSpread;

    /// <summary>Marge intérieure d'une grille : la lueur reste visible au bord de la zone qui défile.</summary>
    public static Thickness ListPadding { get; } = new(GlowExtent);

    /// <summary>Marge de défilement autour d'un élément qui prend le focus (pour la lueur).</summary>
    public static double ScrollRoom => GlowExtent;

    /// <summary>Position des équerres autour de la boîte de la tuile (elles débordent de <see cref="CursorOutset"/>).</summary>
    public static Thickness CursorMargin { get; } = new(-CursorOutset);

    /// <summary>Rayon de la face, du verre, de l'ombre et de l'icône.</summary>
    public static CornerRadius FaceCornerRadius { get; } = new(FaceRadius);

    /// <summary>Position de la face, de l'ombre et du liseré dans la boîte de la tuile.</summary>
    public static Thickness FaceMargin { get; } = new(FaceInset);

    /// <summary>Côté de l'image d'une console : 90 % de la face (176 − 2 × 4 = 168, donc 151,2).</summary>
    public static double ConsoleArtSize { get; } = (ConsoleSize - (2 * FaceInset)) * ConsoleArtScale;

    /// <summary>
    /// Côté de l'image d'une console dans la boîte d'un jeu (<see cref="GameSize"/>) : style Wii U, où la tuile d'une console
    /// prend la boîte d'un jeu pour avoir, une fois mise à l'échelle de la page, le rayon et le liseré des cadres vides (DF-6).
    /// </summary>
    public static double ConsoleArtSizeInGameBox { get; } = (GameSize - (2 * FaceInset)) * ConsoleArtScale;

    /// <summary>Côté de l'image de la console dans l'en-tête de son écran : 90 % de la vignette (99), même passe-partout.</summary>
    public static double ConsoleHeaderArtSize { get; } = ConsoleHeaderSize * ConsoleArtScale;

    /// <summary>
    /// Taille du signe d'une tuile « + » de côté <paramref name="box"/>, arrondie : 77 dans la boîte d'un jeu, 84 dans celle
    /// d'une console (D-186 : le même rapport partout).
    /// </summary>
    public static double AddGlyphSize(double box) => Math.Round(box * AddGlyphScale);

    /// <summary>Transformation de la tuile qui a le focus (animée par la transition de Panel.tileArt).</summary>
    public static ITransform FocusTransform { get; } = Scale(FocusScale);

    /// <summary>Transformation d'une tuile sous la souris.</summary>
    public static ITransform HoverTransform { get; } = Scale(HoverScale);

    /// <summary>Transformation d'une tuile pendant l'appui.</summary>
    public static ITransform PressTransform { get; } = Scale(PressScale);

    /// <summary>Transformation de l'image de la tuile choisie, réduite dans son cadre (D-95).</summary>
    public static ITransform SelectedArtTransform { get; } = Scale(SelectedArtScale);

    private static ITransform Scale(double factor) =>
        TransformOperations.Parse(string.Create(CultureInfo.InvariantCulture, $"scale({factor})"));
}
