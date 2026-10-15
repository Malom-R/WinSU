using Avalonia;

namespace WinSU.App.Controls;

/// <summary>
/// Mesures du style Standard 2.0 (étape 30), relevées sur les maquettes de l'utilisateur (Sprite-0001 et 0002, 2551 × 1445
/// pixels = sa fenêtre agrandie à 200 % : un pixel de maquette = un demi-DIP), dans la fenêtre de référence
/// (<see cref="UiScale"/> : 1280 × 730 chez l'utilisateur). Utilisé par Themes/Standard.axaml et les vues du style.
/// </summary>
public static class StandardMetrics
{
    /// <summary>
    /// Ligne de l'en-tête (sous la capsule WinSU, date, heure) : les Réglages commencent dessous et ce qui passe au-dessus
    /// est atténué (étape 34).
    /// </summary>
    public const double PageTop = 96;

    /// <summary>Bord gauche de la bannière héro de l'accueil : le bord des onglets sur la maquette (≈ 420 DIP).</summary>
    public const double HeroLeft = 420;

    /// <summary>
    /// Pas de la file diagonale de l'accueil, d'un récent au suivant (plus ancien) : vers le bas à gauche, à 45° comme sur la
    /// maquette ; une tuile de 160 et un écart de 12 : 4 tuiles se voient (D-103 : « 4 »).
    /// </summary>
    public const double DiagonalStep = 172;

    /// <summary>Le point fixe de la file (la tuile choisie), en part de la page : le milieu de la diagonale de la maquette.</summary>
    public const double DiagonalAnchorX = 0.74;

    /// <summary>Voir <see cref="DiagonalAnchorX"/>.</summary>
    public const double DiagonalAnchorY = 0.53;

    /// <summary>Portrait : le point fixe plus au centre de la page étroite (les tuiles plus récentes restent visibles).</summary>
    public const double PortraitAnchorX = 0.56;

    /// <summary>Voir <see cref="PortraitAnchorX"/>.</summary>
    public const double PortraitAnchorY = 0.5;

    /// <summary>Portrait : la bannière commence sous les onglets, plus à gauche (la page n'a que ≈ 730 de large).</summary>
    public static Thickness PortraitHeroMargin { get; } = new(170, 0, 0, 0);

    /// <summary>Zone de la bannière héro : de <see cref="HeroLeft"/> au bord droit, sur toute la hauteur.</summary>
    public static Thickness HeroMargin { get; } = new(HeroLeft, 0, 0, 0);

    /// <summary>Déplacement d'un récent au suivant.</summary>
    public static Vector DiagonalVector { get; } = new(-DiagonalStep, DiagonalStep);

    // Logo du jeu choisi (étape 33, D-140, maquette Sprite-0010) : sous les onglets, à gauche de la file diagonale.

    /// <summary>Largeur de la place du logo : celle de la capsule du logo, de la date et de l'heure (≈ 416).</summary>
    public const double LogoZoneWidth = 416;

    /// <summary>Taille du logo au plus : 20 % de plus que dans les autres styles (320 × 190 ; retour de l'utilisateur).</summary>
    public const double LogoMaxWidth = 384;

    /// <summary>Voir <see cref="LogoMaxWidth"/>.</summary>
    public const double LogoMaxHeight = 228;

    /// <summary>
    /// Place du logo : du bord gauche de la capsule (13) ; de 16 sous les onglets (leur bas à ≈ 307) à 16 au-dessus de la
    /// barre d'aide (son haut à ≈ 665 dans la fenêtre de 730). Le logo y est centré.
    /// </summary>
    public static Thickness LogoZoneMargin { get; } = new(13, 323, 0, 81);

    // Jeux (D-104, maquette Sprite-0002) : colonne des consoles au bord droit, la console choisie toujours en bas ; à sa
    // gauche, la colonne de ses jeux, le « + » en face d'elle et les jeux au-dessus.

    /// <summary>Largeur d'une colonne (la tuile grossie et ses équerres y tiennent).</summary>
    public const double ColumnWidth = 240;

    /// <summary>
    /// D'où arrivent les jeux d'une console quand elle devient la choisie (30.14) : une colonne plus à droite, de derrière
    /// celle des consoles, en fondu (<see cref="SlideIn"/>).
    /// </summary>
    public const double GamesSlideDistance = ColumnWidth;

    /// <summary>Hauteur du point fixe des deux colonnes (la console choisie, le « + »), en part de la page : en bas.</summary>
    public const double ColumnAnchorY = 0.84;

    /// <summary>D'une console à la suivante (tuile de 176 et un écart de 24).</summary>
    public const double ConsolePitch = TileMetrics.ConsoleSize + 24;

    /// <summary>D'un jeu au suivant, vers le haut (tuile de 160 et un écart de 24).</summary>
    public const double GamePitch = TileMetrics.GameSize + 24;

    /// <summary>
    /// Colonne des consoles, plus large vers la gauche pour y loger la flèche vers les jeux (la zone d'une liste rogne ce
    /// qui la dépasse) : de x 898 à 1258 dans la page de 1280.
    /// </summary>
    public const double ConsoleColumnWidth = 360;

    /// <summary>Le point fixe de la colonne des consoles : ses tuiles centrées à ≈ 1138 (maquette : x 1071 à 1205).</summary>
    public const double ConsoleAnchorX = 240 / ConsoleColumnWidth;

    public static Thickness ConsoleColumnMargin { get; } = new(0, 0, 22, 0);

    /// <summary>« Vos consoles », accroché en haut et centré sur les tuiles des consoles (largeur 220).</summary>
    public static Thickness ConsolesHeaderMargin { get; } = new(0, 0, 32, 0);

    /// <summary>Colonne des jeux : centrée à ≈ 818 (maquette : tuiles de x 743 à 893).</summary>
    public static Thickness GameColumnMargin { get; } = new(0, 0, 342, 0);

    public static Vector ConsoleVector { get; } = new(0, ConsolePitch);

    public static Vector GameVector { get; } = new(0, -GamePitch);
}
