using Avalonia;

namespace WinSU.App.Controls;

/// <summary>
/// Mesures de la grille en pages du style Wii U (M16), relevées sur Style_Maquette/Wii_U.png : cadres carrés de 190 px,
/// pas de 243 px en largeur et de 226 px en hauteur, première colonne de la page suivante 46 px plus loin qu'une colonne
/// ordinaire. Tout est proportionnel au côté du cadre, calculé d'après la place disponible (<see cref="PageGridLayout"/>).
/// Utilisé par <see cref="PagedGridPanel"/> et les vues du style (Views/WiiU, {x:Static}).
/// </summary>
public static class PageGridMetrics
{
    /// <summary>Colonnes et rangées d'une page couchée ; debout (portrait, étape 25), les deux s'échangent (3 × 5).</summary>
    public const int Columns = 5;

    public const int Rows = 3;

    public const int PerPage = Columns * Rows;

    /// <summary>Pas horizontal rapporté au côté du cadre (243 / 190).</summary>
    public const double PitchXRatio = 1.28;

    /// <summary>Pas vertical rapporté au côté du cadre (226 / 190).</summary>
    public const double PitchYRatio = 1.19;

    /// <summary>Écart supplémentaire entre deux pages, rapporté au côté du cadre (46 / 190).</summary>
    public const double PageGapRatio = 0.24;

    /// <summary>
    /// Retrait de la face (verre et icône) dans le cadre, rapporté au côté du cadre : un élément est une tuile de 160 mise
    /// à l'échelle de son cadre (étape 29 : son verre est le cadre, son icône le remplit) ; les cadres vides ont la même
    /// face.
    /// </summary>
    public const double FaceInsetRatio = TileMetrics.FaceInset / TileMetrics.GameSize;

    /// <summary>Rayon de la face, rapporté au côté du cadre (18 sur 160).</summary>
    public const double FaceRadiusRatio = TileMetrics.FaceRadius / TileMetrics.GameSize;

    /// <summary>Bord du verre, rapporté au côté du cadre (1 sur 160).</summary>
    public const double GlassStrokeRatio = TileMetrics.GlassStroke / TileMetrics.GameSize;

    /// <summary>Au-dessus de la première rangée : l'emplacement choisi grossi et ses équerres y dépassent.</summary>
    public const double TopRoom = 22;

    /// <summary>Sous la dernière rangée : même place qu'au-dessus.</summary>
    public const double BottomRoom = 22;

    /// <summary>Gardé à gauche et à droite de la page affichée pour les flèches ‹ ›.</summary>
    public const double SideRoom = 96;

    /// <summary>
    /// Avant la première colonne : la tuile choisie grossie et sa lueur restent visibles ; au-delà, la page précédente est
    /// cachée (seule la suivante dépasse, à droite, comme dans la référence).
    /// </summary>
    public const double ClipRoom = 24;

    /// <summary>Hauteur de la barre du haut des écrans du style (logo, titre, heure).</summary>
    public const double TopBarHeight = 96;

    /// <summary>Hauteur gardée en bas des écrans du style : points de pagination et icônes.</summary>
    public const double BottomBarHeight = 150;

    /// <summary>Place de la grille dans un écran du style : toute la largeur (la page suivante dépasse jusqu'au bord).</summary>
    public static Thickness GridMargin { get; } = new(0, TopBarHeight - TopRoom, 0, BottomBarHeight);

    /// <summary>Flèches ‹ › : à mi-hauteur de la grille.</summary>
    public static Thickness ArrowsMargin { get; } = new(24, TopBarHeight, 24, BottomBarHeight);

    /// <summary>Points de pagination : juste sous la grille, au-dessus de la capsule des icônes.</summary>
    public static Thickness DotsMargin { get; } = new(0, 0, 0, BottomBarHeight - 14);

    /// <summary>
    /// Page des Réglages (tableau déroulant commun, étape 32) : toute la hauteur (étape 34 : elle passe sous la barre du haut,
    /// ce qui y passe est atténué ; Border.settingsTop).
    /// </summary>
    public static Thickness SettingsMargin { get; } = new(40, 0, 40, 0);
}
