using System.Collections.ObjectModel;
using WinSU.Core.Input;

namespace WinSU.Core.Settings;

/// <summary>
/// Contenu de <c>settings.json</c>. Traité comme immuable : on le modifie avec <c>with</c> via
/// <see cref="ISettingsStore.UpdateAsync"/>, jamais en affectant une propriété d'une instance partagée.
/// Chaque section a ses valeurs par défaut : une section ou une propriété absente du fichier les garde.
/// </summary>
/// <remarks>
/// Les propriétés sont en <c>set</c> et non en <c>init</c> : avec des <c>init</c>, la sérialisation
/// générée de System.Text.Json reconstruit l'objet en passant toutes les propriétés, et les absentes
/// du JSON perdent leur valeur par défaut (vérifié par les tests).
/// </remarks>
public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public bool FirstRunCompleted { get; set; }
    public GeneralSettings General { get; set; } = new();
    public AppearanceSettings Appearance { get; set; } = new();
    public AudioSettings Audio { get; set; } = new();
    public InputSettings Input { get; set; } = new();
    public LaunchSettings Launch { get; set; } = new();
    public OnlineSettings Online { get; set; } = new();
}

public sealed record GeneralSettings
{
    /// <summary>Code de langue de l'interface ("fr", "en"). Pris en compte au démarrage.</summary>
    public string Language { get; set; } = "fr";

    // Pas de « démarrer avec Windows » (D-20) : l'utilisateur s'en charge lui-même.
    public bool StartFullscreen { get; set; } = true;
}

/// <summary>Style du menu (E7) : disposition de l'écran principal et des écrans qui en ont une ; repli Standard.</summary>
public enum UiStyle
{
    Standard,

    /// <summary>Accueil façon Switch (M15, référence Style_Maquette/Switch.png).</summary>
    Switch,

    /// <summary>Grilles en pages façon Wii U (M16, référence Style_Maquette/Wii_U.png).</summary>
    WiiU,
}

/// <summary>
/// Police de l'interface (étape 25, D-71). Depuis 30.10 (D-106), les polices de l'application iiSU d'origine (Cal Sans
/// à Gamenum) donnent DM Sans : elles restent pour relire les anciens fichiers. Étape 32 (D-129) : des polices libres.
/// Ne jamais retirer une valeur ; les nouvelles à la fin.
/// </summary>
public enum AppFont
{
    /// <summary>DM Sans, texte et titres (D-106).</summary>
    DmSans,
    CalSans,
    Arctainium,
    ConsoleSans,
    Gamemaster,
    Gamenum,

    // Polices libres (SIL OFL 1.1) de l'étape 32 (D-129), ajoutées à la fin : très lisibles, puis « jeu / techno »
    Inter,
    AtkinsonHyperlegibleNext,
    Lexend,
    Exo2,
    ChakraPetch,
    Oxanium,
}

public enum ThemePreference
{
    System,
    Light,
    Dark,

    /// <summary>Thème enregistré (M14) : <see cref="AppearanceSettings.CustomThemeId"/>.</summary>
    Custom,
}

public sealed record AppearanceSettings
{
    public UiStyle Style { get; set; } = UiStyle.Standard;
    public ThemePreference Theme { get; set; } = ThemePreference.System;

    /// <summary>Identifiant du thème enregistré quand <see cref="Theme"/> vaut <see cref="ThemePreference.Custom"/> ; thème absent : Système.</summary>
    public string? CustomThemeId { get; set; }
    public bool ShowBorders { get; set; } = true;

    /// <summary>Fond animé (lampe à lave, M10) : les gouttes bougent tant que l'utilisateur se sert de WinSU.</summary>
    public bool AnimatedBackground { get; set; } = true;

    /// <summary>Balayage de lumière sur la tuile qui prend le focus (étape 29, D-90), activé par défaut.</summary>
    public bool TileSheen { get; set; } = true;

    /// <summary>
    /// Vitesse du balayage (étape 29) : 1 = normale ; de <see cref="MinTileSheenSpeed"/> (deux fois plus lent) à
    /// <see cref="MaxTileSheenSpeed"/> (deux fois plus rapide).
    /// </summary>
    public double TileSheenSpeed { get; set; } = 1;

    public const double MinTileSheenSpeed = 0.5;
    public const double MaxTileSheenSpeed = 2;

    /// <summary>Style des glyphes de boutons (M12) : Xbox par défaut (D-42 : plus de choix « Automatique »).</summary>
    public ButtonStyle ButtonStyle { get; set; } = ButtonStyle.Xbox;

    /// <summary>Glyphes des boutons en couleurs (A vert, ✕ bleu…), coupé par défaut (D-34).</summary>
    public bool ColoredGlyphs { get; set; }

    /// <summary>Police de l'interface (étape 25, D-71) : DM Sans, la seule depuis 30.10 (D-106).</summary>
    public AppFont Font { get; set; } = AppFont.DmSans;
}

public sealed record AudioSettings
{
    public bool UiSounds { get; set; } = true;
    public double UiVolume { get; set; } = 0.8;
}

public enum InputProfile
{
    /// <summary>Disposition standard (<see cref="InputDefaults.Controller"/>) : assistant passé ou « Rétablir par défaut ».</summary>
    Default,

    /// <summary>Liaisons enregistrées dans <see cref="InputSettings.Bindings"/> (assistant, écran « Boutons »).</summary>
    Custom,
}

public sealed record InputSettings
{
    private static readonly IReadOnlyDictionary<InputAction, IReadOnlyList<InputBinding>> NoBindings =
        ReadOnlyDictionary<InputAction, IReadOnlyList<InputBinding>>.Empty;

    public InputProfile Profile { get; set; } = InputProfile.Default;
    public string? ControllerName { get; set; }

    /// <summary>Liaisons de la manette (plusieurs par action), utilisées en profil <see cref="InputProfile.Custom"/>.</summary>
    public IReadOnlyDictionary<InputAction, IReadOnlyList<InputBinding>> Bindings { get; set; } = NoBindings;

    /// <summary>
    /// Touches du clavier (plusieurs par action, noms de l'énumération <c>Key</c> d'Avalonia), ou null pour les touches
    /// par défaut (<see cref="InputDefaults.Keyboard"/>). Absent des fichiers d'avant M11.
    /// </summary>
    public IReadOnlyDictionary<InputAction, IReadOnlyList<string>>? Keys { get; set; }

    public int RepeatDelayMs { get; set; } = 400;
    public int RepeatIntervalMs { get; set; } = 80;
    public double AxisThreshold { get; set; } = 0.6;

    // Méthodes et non propriétés : la sérialisation écrirait une propriété calculée dans settings.json.

    /// <summary>Liaisons de la manette en vigueur : disposition standard ou liaisons enregistrées.</summary>
    public IReadOnlyDictionary<InputAction, IReadOnlyList<InputBinding>> GetControllerBindings() =>
        Profile == InputProfile.Custom ? Bindings : InputDefaults.Controller;

    /// <summary>Touches du clavier en vigueur.</summary>
    public IReadOnlyDictionary<InputAction, IReadOnlyList<string>> GetKeys() => Keys ?? InputDefaults.Keyboard;

    /// <summary>Égalité par contenu : deux mappings identiques sont égaux même s'ils sont dans des dictionnaires distincts.</summary>
    public bool Equals(InputSettings? other) =>
        other is not null &&
        Profile == other.Profile &&
        ControllerName == other.ControllerName &&
        RepeatDelayMs == other.RepeatDelayMs &&
        RepeatIntervalMs == other.RepeatIntervalMs &&
        AxisThreshold.Equals(other.AxisThreshold) &&
        BindingsEqual(Bindings, other.Bindings) &&
        (Keys is null ? other.Keys is null : other.Keys is not null && BindingsEqual(Keys, other.Keys));

    public override int GetHashCode() =>
        HashCode.Combine(Profile, ControllerName, RepeatDelayMs, RepeatIntervalMs, AxisThreshold, Bindings.Count, Keys?.Count);

    private static bool BindingsEqual<T>(
        IReadOnlyDictionary<InputAction, IReadOnlyList<T>> left,
        IReadOnlyDictionary<InputAction, IReadOnlyList<T>> right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left.Count != right.Count)
            return false;

        foreach (var (action, bindings) in left)
        {
            if (!right.TryGetValue(action, out var others) || !bindings.SequenceEqual(others))
                return false;
        }

        return true;
    }
}

public sealed record LaunchSettings
{
    /// <summary>
    /// Pendant un jeu : WinSU se réduit (par défaut) ; sinon il reste derrière le jeu, en veille (D-181 : en pause, priorité
    /// basse, mémoire rendue ; Réglages → Système, « Veille derrière le jeu »).
    /// </summary>
    public bool MinimizeWhileRunning { get; set; } = true;

    public int ShellOnlyGraceSeconds { get; set; } = 10;
}

/// <summary>Services en ligne (étape 31) : seulement ceux que l'utilisateur a réglés.</summary>
public sealed record OnlineSettings
{
    /// <summary>
    /// Clé d'API SteamGridDB de l'utilisateur (D-115 : son compte, Préférences → API), ou null. Jamais écrite au journal.
    /// Sans clé, WinSU n'envoie rien à SteamGridDB et l'option n'apparaît pas.
    /// </summary>
    public string? SteamGridDbKey { get; set; }
}
