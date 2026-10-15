using Avalonia;
using static WinSU.App.Controls.PageGridMetrics;

namespace WinSU.App.Controls;

/// <summary>
/// Géométrie d'une grille en pages (style Wii U, M16) pour une taille donnée : pages de 5 × 3 (couchées) ou de 3 × 5
/// (debout, étape 25 : fenêtre en portrait), côté des cadres, position de la page affichée (centrée), place de chaque
/// emplacement ; et règles de déplacement d'un élément à l'autre, d'une page à l'autre. Les emplacements sont numérotés
/// dans l'ordre des éléments : page après page, rangée après rangée ; une page en compte toujours 15.
/// </summary>
public readonly record struct PageGridLayout(double Frame, double Left, double Top, int Columns = PageGridMetrics.Columns)
{
    public int Rows => PerPage / Columns;

    public double PitchX => Frame * PitchXRatio;

    public double PitchY => Frame * PitchYRatio;

    /// <summary>D'une page à la suivante : ses colonnes, plus l'écart entre les pages.</summary>
    public double Stride => (Columns * PitchX) + (Frame * PageGapRatio);

    public double PageWidth => ((Columns - 1) * PitchX) + Frame;

    public double PageHeight => ((Rows - 1) * PitchY) + Frame;

    /// <summary>Rayon de la face d'un emplacement : celui d'une tuile mise à l'échelle du cadre (étape 29).</summary>
    public double FaceRadius => Frame * FaceRadiusRatio;

    /// <summary>
    /// Pages de 5 × 3 ou de 3 × 5, celles qui donnent les plus grands cadres (5 × 3 sur un écran couché, 3 × 5 debout) :
    /// le plus grand cadre qui laisse tenir les rangées en hauteur, et la page avec la place des flèches en largeur ; la
    /// page est centrée.
    /// </summary>
    public static PageGridLayout Compute(Size size)
    {
        var wide = Compute(size, PageGridMetrics.Columns);
        var tall = Compute(size, PageGridMetrics.Rows);
        return tall.Frame > wide.Frame ? tall : wide;
    }

    private static PageGridLayout Compute(Size size, int columns)
    {
        var rows = PerPage / columns;
        var byHeight = (size.Height - TopRoom - BottomRoom) / (((rows - 1) * PitchYRatio) + 1);
        var byWidth = (size.Width - (2 * SideRoom)) / (((columns - 1) * PitchXRatio) + 1);
        var frame = Math.Max(0, Math.Min(byHeight, byWidth));
        var pageWidth = ((columns - 1) * frame * PitchXRatio) + frame;
        var pageHeight = ((rows - 1) * frame * PitchYRatio) + frame;
        var top = TopRoom + Math.Max(0, (size.Height - TopRoom - BottomRoom - pageHeight) / 2);
        return new PageGridLayout(frame, (size.Width - pageWidth) / 2, top, columns);
    }

    public static int PageCount(int count) => Math.Max(1, (count + PerPage - 1) / PerPage);

    /// <summary>
    /// Emplacements dessinés pour <paramref name="count"/> éléments : toutes les pages occupées, plus la page vide qui suit
    /// (la colonne qui dépasse à droite) seulement quand la dernière est pleine (30.13, D-121 : « le second set n'apparaît
    /// que quand le premier est rempli »).
    /// </summary>
    public static int DrawnSlots(int count) => (PageCount(count) + (count > 0 && count % PerPage == 0 ? 1 : 0)) * PerPage;

    public static int PageOf(int slot) => slot / PerPage;

    /// <summary>Cadre d'un emplacement quand la page affichée est <paramref name="pagePosition"/> (fractionnaire pendant le glissement).</summary>
    public Rect FrameRect(int slot, double pagePosition)
    {
        var inPage = slot % PerPage;
        var x = Left + ((PageOf(slot) - pagePosition) * Stride) + (inPage % Columns * PitchX);
        var y = Top + (inPage / Columns * PitchY);
        return new Rect(x, y, Frame, Frame);
    }

    /// <summary>
    /// Face d'un emplacement (verre et icône) : celle d'une tuile de 160 mise à l'échelle du cadre ; un cadre vide montre
    /// le même verre à la même place.
    /// </summary>
    public Rect FaceRect(int slot, double pagePosition) => FrameRect(slot, pagePosition).Deflate(Frame * FaceInsetRatio);

    /// <summary>
    /// Épaisseur du bord du verre : celle d'une tuile mise à l'échelle du cadre ; les cadres vides ont ainsi le même bord
    /// que les tuiles.
    /// </summary>
    public double GlassStrokeThickness => Frame * GlassStrokeRatio;

    /// <summary>Partie visible : la page affichée et ce qui dépasse à droite ; rien de la page précédente.</summary>
    public Rect VisibleArea(Size size)
    {
        var left = Math.Max(0, Left - ClipRoom);
        return new Rect(left, 0, Math.Max(0, size.Width - left), size.Height);
    }

    /// <summary>
    /// Gauche (<paramref name="step"/> −1) ou Droite (+1) : l'élément voisin de la même rangée ; au bord de la page, la page
    /// voisine sur la même rangée (son dernier élément si cette rangée y est vide). −1 s'il n'y a rien de ce côté.
    /// </summary>
    public static int Neighbour(int index, int count, int step, int columns = PageGridMetrics.Columns)
    {
        if (index < 0 || index >= count)
            return -1;

        var page = PageOf(index);
        var row = index % PerPage / columns;
        var column = index % columns;
        if (step > 0)
        {
            if (column < columns - 1 && index + 1 < count)
                return index + 1;

            return page + 1 < PageCount(count) ? Math.Min(((page + 1) * PerPage) + (row * columns), count - 1) : -1;
        }

        if (column > 0)
            return index - 1;

        return page > 0 ? ((page - 1) * PerPage) + (row * columns) + columns - 1 : -1;
    }

    /// <summary>
    /// LB/RB : sur la page voisine de <paramref name="currentPage"/>, l'élément à la place de <paramref name="index"/> (le
    /// premier sans élément de départ, le dernier de la page si cette place est vide). −1 s'il n'y a pas de page de ce côté.
    /// </summary>
    public static int Turn(int currentPage, int index, int count, int offset)
    {
        var page = currentPage + offset;
        if (count == 0 || page < 0 || page >= PageCount(count))
            return -1;

        var slot = index >= 0 ? index % PerPage : 0;
        return Math.Min((page * PerPage) + slot, count - 1);
    }
}
