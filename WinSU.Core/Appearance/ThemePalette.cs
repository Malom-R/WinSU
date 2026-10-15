namespace WinSU.Core.Appearance;

/// <summary>
/// Couleurs d'un thème enregistré, prêtes à appliquer (M14) : les jetons colorés de <c>Themes/Theme.axaml</c>, par nom
/// (« Accent » pour <c>WinSU.AccentBrush</c>…). Les jetons neutres (ombres, liserés blancs, motif du fond) restent
/// ceux de la base claire ou sombre.
/// </summary>
/// <param name="Colors">
/// Jetons unis, dont la sélection de l'étape 29 : FocusRing (anneau, uni), FocusGlow (sa lueur), Signature et
/// SignatureAlt (icônes générées : tuile « + », tuiles « Jeux » et « Apps »).
/// </param>
/// <param name="Stops">
/// Jetons en dégradé, arrêts dans l'ordre : Glass (clair : trois arrêts du reflet ; sombre : un seul), Control et
/// ControlHover (clair : haut et bas ; sombre : un seul), Gradient (dégradé du thème), RingGradient (décor du style Wii U : le
/// dégradé de l'ancien anneau), GlassFill (fond du verre des tuiles, cinq arrêts), SelectedFill (verre de la tuile
/// choisie : haut, milieu, bas), Cursor (équerres de la tuile choisie : haut gauche, haut droit, bas droit, bas gauche),
/// Lava (gouttes de la lampe).
/// </param>
public sealed record ThemePalette(ThemeBase Base, IReadOnlyDictionary<string, ThemeColor> Colors, IReadOnlyDictionary<string, IReadOnlyList<ThemeColor>> Stops)
{
    /// <summary>Contraste minimal du texte courant (D-38) ; celui du texte tertiaire et de l'accent sur le fond.</summary>
    public const double TextContrast = 4.5;
    public const double SecondaryContrast = 3.0;

    /// <summary>
    /// Écart de teinte entre deux arrêts du dégradé d'un thème monocouleur (34,5°, choisi à M14 ; gardé : les thèmes
    /// enregistrés ne changent pas, D-153).
    /// </summary>
    public const double GradientHueStep = 34.5;

    /// <summary>Couleur d'un nouveau thème monocouleur, et d'une couleur illisible du fichier : la lavande de « Prisme ».</summary>
    public const string DefaultColor = "#9D8CF5";

    /// <summary>Voile des fenêtres superposées (D-170) : le fond assombri de tant, teinté de l'accent de tant, à cette opacité.</summary>
    public const double ScrimDarken = 0.8;
    public const double ScrimTint = 0.25;
    public const double ScrimAlpha = 0xA6 / 255.0;

    private static readonly ThemeColor LightText = ThemeColor.Parse("#F5F3F8");
    private static readonly ThemeColor DarkText = ThemeColor.Parse("#212529");

    public ThemeColor this[string name] => Colors[name];

    /// <summary>Les couleurs d'un thème ; une couleur illisible du fichier est remplacée par celle par défaut.</summary>
    public static ThemePalette For(CustomTheme theme) =>
        theme.Mode == ThemeMode.Mono ? Mono(theme.Base, Read(theme.Color, DefaultColor)) : Multi(theme.Base, theme.Colors);

    /// <summary>
    /// Monocouleur (D-39) : la couleur teinte discrètement le fond et le verre de la base, et donne l'accent, le dégradé
    /// (la couleur, puis deux teintes voisines), l'anneau de focus et les gouttes de la lampe.
    /// </summary>
    public static ThemePalette Mono(ThemeBase themeBase, ThemeColor color)
    {
        var light = themeBase == ThemeBase.Light;
        var (backgroundTint, surfaceTint) = light ? (0.10, 0.06) : (0.12, 0.08);
        ThemeColor Tint(string hex, double amount) => ThemeColor.Parse(hex).Mix(color with { A = ThemeColor.Parse(hex).A }, amount);

        var colors = new Dictionary<string, ThemeColor>
        {
            ["Background"] = Tint(light ? "#EAEAEA" : "#101017", backgroundTint),
            ["Surface"] = Tint(light ? "#F9F9F9" : "#15161A", surfaceTint),
            ["SurfaceAlt"] = Tint(light ? "#DEE2E6" : "#25262D", surfaceTint),
            ["GlassOpaque"] = Tint(light ? "#F9F9F9" : "#15161A", surfaceTint),
            ["Dialog"] = Tint(light ? "#F2F5F9" : "#0E0E13", surfaceTint),
        };
        var stops = new Dictionary<string, IReadOnlyList<ThemeColor>>
        {
            ["Glass"] = light
                ? [Tint("#F5F6F8", surfaceTint), Tint("#EBEDF1", surfaceTint), Tint("#E4E7EC", surfaceTint)]
                : [Tint("#F215161A", surfaceTint)],
            ["Control"] = light ? [Tint("#F7F8FA", surfaceTint), Tint("#E1E3E7", surfaceTint)] : [Tint("#F218191D", surfaceTint)],
            ["ControlHover"] = light ? [Tint("#FFFFFF", surfaceTint), Tint("#ECEDEF", surfaceTint)] : [Tint("#F21F2025", surfaceTint)],
        };

        var (hue, saturation, lightness) = color.ToHsl();
        var vivid = Math.Clamp(lightness, 0.45, 0.65);
        IReadOnlyList<ThemeColor> gradient =
        [
            ThemeColor.FromHsl(hue, saturation, vivid),
            ThemeColor.FromHsl(hue - GradientHueStep, saturation, vivid),
            ThemeColor.FromHsl(hue - 2 * GradientHueStep, saturation, vivid),
        ];
        IReadOnlyList<ThemeColor> lava = [.. gradient, ThemeColor.FromHsl(hue - 8, Math.Min(1, saturation + 0.08), vivid - 0.03)];

        var accent = Readable(color, colors["Background"], light);
        return Complete(themeBase, colors, stops, accent, gradient, lava);
    }

    /// <summary>Multicouleur : chaque couleur est celle choisie ; les surfaces suivent le verre, les textes restent calculés.</summary>
    public static ThemePalette Multi(ThemeBase themeBase, ThemeColors chosen)
    {
        var defaults = new ThemeColors();
        var light = themeBase == ThemeBase.Light;
        var glass = Read(chosen.Glass, defaults.Glass);
        var colors = new Dictionary<string, ThemeColor>
        {
            ["Background"] = Read(chosen.Background, defaults.Background),
            ["Surface"] = glass,
            ["SurfaceAlt"] = light ? glass.Darken(0.08) : glass.Lighten(0.08),
            ["GlassOpaque"] = glass,
            ["Dialog"] = light ? glass.Lighten(0.2) : glass.Darken(0.2),
        };
        var stops = new Dictionary<string, IReadOnlyList<ThemeColor>>
        {
            ["Glass"] = light ? [glass.Lighten(0.3), glass, glass.Darken(0.03)] : [glass.WithAlpha(0.95)],
            ["Control"] = light ? [glass.Lighten(0.4), glass.Darken(0.06)] : [glass.Lighten(0.03).WithAlpha(0.95)],
            ["ControlHover"] = light ? [glass.Lighten(0.7), glass.Lighten(0.1)] : [glass.Lighten(0.08).WithAlpha(0.95)],
        };
        IReadOnlyList<ThemeColor> gradient =
            [Read(chosen.Gradient1, defaults.Gradient1), Read(chosen.Gradient2, defaults.Gradient2), Read(chosen.Gradient3, defaults.Gradient3)];
        IReadOnlyList<ThemeColor> lava =
            [Read(chosen.Lava1, defaults.Lava1), Read(chosen.Lava2, defaults.Lava2), Read(chosen.Lava3, defaults.Lava3), Read(chosen.Lava4, defaults.Lava4)];
        return Complete(themeBase, colors, stops, Read(chosen.Accent, defaults.Accent), gradient, lava);
    }

    /// <summary>Les couleurs multicouleur qui donnent ce même rendu (passage de mono à multi dans l'éditeur).</summary>
    public ThemeColors ToChosenColors() => new()
    {
        Background = this["Background"].ToHex(),
        Glass = this["GlassOpaque"].ToHex(),
        Accent = this["Accent"].ToHex(),
        Gradient1 = Stops["Gradient"][0].ToHex(),
        Gradient2 = Stops["Gradient"][1].ToHex(),
        Gradient3 = Stops["Gradient"][2].ToHex(),
        Lava1 = Stops["Lava"][0].ToHex(),
        Lava2 = Stops["Lava"][1].ToHex(),
        Lava3 = Stops["Lava"][2].ToHex(),
        Lava4 = Stops["Lava"][3].ToHex(),
    };

    /// <summary>Luminosité de l'anneau et des icônes générées avant leur mise au contraste : une couleur franche.</summary>
    public const double VividLightness = 0.6;

    /// <summary>Opacité de la lueur de l'anneau (#AA, comme le magenta par défaut).</summary>
    public const double GlowAlpha = 0xAA / 255.0;

    /// <summary>Fond du verre des tuiles sur une base sombre (celui de Themes/Theme.axaml) : neutre, la lampe transparaît.</summary>
    private static readonly string[] DarkGlassFill = ["#1AFFFFFF", "#14FFFFFF", "#38000000", "#14FFFFFF", "#38FFFFFF"];

    /// <summary>Textes (D-38), déclinaisons de l'accent, anneau de focus et lueurs.</summary>
    private static ThemePalette Complete(
        ThemeBase themeBase,
        Dictionary<string, ThemeColor> colors,
        Dictionary<string, IReadOnlyList<ThemeColor>> stops,
        ThemeColor accent,
        IReadOnlyList<ThemeColor> gradient,
        IReadOnlyList<ThemeColor> lava)
    {
        // Le texte se lit sur le fond, sur les surfaces et sur le verre des panneaux.
        ThemeColor[] grounds = [colors["Background"], colors["Surface"], colors["GlassOpaque"], colors["Dialog"]];
        var text = BestText(grounds);
        colors["Text"] = text;
        colors["TextMuted"] = Faded(text, colors["Surface"], grounds, TextContrast, 0.45);
        colors["TextTertiary"] = Faded(text, colors["Surface"], grounds, SecondaryContrast, 0.6);

        accent = accent with { A = 255 };
        colors["Accent"] = accent;
        colors["AccentHover"] = accent.Darken(0.12);
        colors["AccentStrong"] = accent.Darken(0.45);
        colors["AccentFaded"] = accent.WithAlpha(0x1A / 255.0);
        colors["AccentTint"] = accent.WithAlpha(0x24 / 255.0);
        colors["AccentGlow"] = accent.WithAlpha(0x4D / 255.0);
        colors["SwitchOn"] = accent;
        // Voile des fenêtres superposées (D-170) : le fond du thème assombri et teinté de son accent, sans motif.
        colors["Scrim"] = colors["Background"].Darken(ScrimDarken).Mix(accent, ScrimTint).WithAlpha(ScrimAlpha);

        // Blanc sur l'accent, sauf s'il devient illisible (accent très clair).
        colors["OnAccent"] = ThemeColor.Contrast(ThemeColor.White, accent) >= SecondaryContrast
            || ThemeColor.Contrast(ThemeColor.White, accent) >= ThemeColor.Contrast(ThemeColor.Black, accent)
            ? ThemeColor.White
            : DarkText;

        // Sélection (étape 29, D-85) : anneau uni, la couleur principale du thème rendue franche puis détachée du fond (le
        // magenta des thèmes Clair et Sombre n'est pas tiré du thème) ; sa lueur.
        var ring = Readable(Vivid(gradient[0]), colors["Background"], themeBase == ThemeBase.Light);
        colors["FocusRing"] = ring;
        colors["FocusGlow"] = ring.WithAlpha(GlowAlpha);
        // Icônes générées (tuile « + », tuile « Jeux ») : la teinte opposée à celle de l'anneau et à la teinte suivante du
        // dégradé, pour ressortir dans le verre du focus, qui est de la couleur du thème (DF-2, étape 35 : vert → magenta,
        // comme le « + » magenta des thèmes Clair et Sombre) ; assez foncées pour porter un glyphe blanc.
        colors["Signature"] = UnderWhite(Opposite(ring));
        colors["SignatureAlt"] = UnderWhite(Opposite(Vivid(gradient[1])));
        // Décor (capsule, coin, pastille du style Wii U) : l'anneau d'avant l'étape 29, le dégradé éclairci dans l'autre sens
        // (cyan en bas à gauche, violet en haut à droite).
        stops["RingGradient"] = [gradient[2].Lighten(0.45), gradient[1].Lighten(0.45), gradient[0].Lighten(0.45)];
        // Fond du verre des tuiles, de haut en bas (voile, bas du reflet, bande sombre, remontée, bas) : en clair, teinté de
        // l'accent (rien ne transparaît d'un panneau presque blanc) ; en sombre, neutre, comme Themes/Theme.axaml.
        stops["GlassFill"] = themeBase == ThemeBase.Light
            ? [accent.WithAlpha(0.22), accent.WithAlpha(0.18), accent.Darken(0.12).WithAlpha(0.45), accent.WithAlpha(0.25), accent.WithAlpha(0.15)]
            : [.. DarkGlassFill.Select(ThemeColor.Parse)];
        // Tuile choisie (29.14, d'après la Wii U) : son verre rempli de l'accent (plus sombre au milieu, plus clair en bas),
        // et quatre équerres, une couleur par coin comme le curseur de la Wii U : les gouttes de la lampe à lave, telles
        // quelles, dans le sens des aiguilles d'une montre depuis le haut à gauche (29.15, demande de l'utilisateur).
        stops["SelectedFill"] = [accent, accent.Darken(0.05), accent.Lighten(0.15)];
        stops["Cursor"] = [.. lava.Select(c => c with { A = 255 })];
        stops["Gradient"] = [.. gradient.Select(c => c with { A = 255 })];
        stops["Lava"] = [.. lava.Select(c => c with { A = 255 })];
        return new ThemePalette(themeBase, colors, stops);
    }

    /// <summary>La même teinte, plus saturée (un gris reste gris), à une luminosité franche.</summary>
    private static ThemeColor Vivid(ThemeColor color)
    {
        var (hue, saturation, _) = color.ToHsl();
        return ThemeColor.FromHsl(hue, Math.Min(1, saturation * 1.25), VividLightness);
    }

    /// <summary>La teinte opposée (à 180°), même saturation et même luminosité.</summary>
    private static ThemeColor Opposite(ThemeColor color)
    {
        var (hue, saturation, lightness) = color.ToHsl();
        return ThemeColor.FromHsl(hue + 180, saturation, lightness);
    }

    /// <summary>La couleur foncée jusqu'à porter un glyphe blanc (3:1, grand glyphe).</summary>
    private static ThemeColor UnderWhite(ThemeColor color)
    {
        var result = color with { A = 255 };
        for (var step = 0; step < 20 && ThemeColor.Contrast(ThemeColor.White, result) < SecondaryContrast; step++)
            result = result.Darken(0.05);
        return result;
    }

    /// <summary>Le texte clair ou sombre de la base qui se lit le mieux partout ; au pire, blanc ou noir purs.</summary>
    private static ThemeColor BestText(IReadOnlyList<ThemeColor> grounds)
    {
        double Worst(ThemeColor text) => grounds.Min(ground => ThemeColor.Contrast(text, ground));
        var best = new[] { LightText, DarkText }.MaxBy(Worst);
        return Worst(best) >= TextContrast ? best : new[] { ThemeColor.White, ThemeColor.Black }.MaxBy(Worst);
    }

    /// <summary>Le texte mélangé à la surface autant que possible, sans passer sous le contraste demandé.</summary>
    private static ThemeColor Faded(ThemeColor text, ThemeColor surface, IReadOnlyList<ThemeColor> grounds, double contrast, double maxAmount)
    {
        var result = text;
        for (var step = 1; step <= 20; step++)
        {
            var candidate = text.Mix(surface, maxAmount * step / 20);
            if (grounds.Any(ground => ThemeColor.Contrast(candidate, ground) < contrast))
                break;
            result = candidate;
        }

        return result;
    }

    /// <summary>L'accent éclairci (base sombre) ou foncé (base claire) jusqu'à se détacher du fond (3:1).</summary>
    private static ThemeColor Readable(ThemeColor color, ThemeColor background, bool light)
    {
        var result = color with { A = 255 };
        for (var step = 0; step < 20 && ThemeColor.Contrast(result, background) < SecondaryContrast; step++)
            result = light ? result.Darken(0.05) : result.Lighten(0.05);
        return result;
    }

    private static ThemeColor Read(string? hex, string fallback) => ThemeColor.TryParse(hex, out var color) ? color : ThemeColor.Parse(fallback);
}
