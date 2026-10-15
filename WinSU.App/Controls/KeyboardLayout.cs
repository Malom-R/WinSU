using Avalonia.Input;

namespace WinSU.App.Controls;

/// <summary>
/// Une touche du clavier dessiné : sa place (en largeurs de touche), son code de balayage quand elle écrit un caractère
/// (la lettre vient alors de la disposition de Windows, <see cref="WinSU.Core.Input.IKeyboardLayout"/>), sinon son
/// étiquette : un texte tel quel (« Ctrl », « F5 », « 7 ») ou une clé de texte traduite (« win_… »).
/// </summary>
public sealed record KeySlot(PhysicalKey Key, double X, double Y, double Width, double Height, int ScanCode, string Label);

/// <summary>
/// Plan du clavier dessiné de l'écran « Boutons » (étape 32, D-130) : un clavier complet ISO de 105 touches (celui des
/// claviers AZERTY), pavé numérique compris, rangé par positions physiques (<see cref="PhysicalKey"/>, la même place quelle
/// que soit la disposition). Mesures en largeurs de touche ; le dessin est mis à l'échelle de sa zone.
/// </summary>
public static class KeyboardLayout
{
    /// <summary>Largeur d'une touche dans le dessin, en DIP, espace compris.</summary>
    public const double Unit = 44;

    /// <summary>Espace entre deux touches, en DIP.</summary>
    public const double Gap = 4;

    public const double Columns = 22.5;

    public const double Rows = 6.5;

    public static IReadOnlyList<KeySlot> Keys { get; } = Build();

    private static List<KeySlot> Build()
    {
        var keys = new List<KeySlot>();

        void Named(PhysicalKey key, double x, double y, string label, double width = 1, double height = 1) =>
            keys.Add(new(key, x, y, width, height, 0, label));

        void Writes(PhysicalKey key, double x, double y, int scanCode, string fallback) =>
            keys.Add(new(key, x, y, 1, 1, scanCode, fallback));

        // Rangée des fonctions
        Named(PhysicalKey.Escape, 0, 0, "win_key_escape");
        PhysicalKey[] functions = [PhysicalKey.F1, PhysicalKey.F2, PhysicalKey.F3, PhysicalKey.F4, PhysicalKey.F5, PhysicalKey.F6,
            PhysicalKey.F7, PhysicalKey.F8, PhysicalKey.F9, PhysicalKey.F10, PhysicalKey.F11, PhysicalKey.F12];
        for (var i = 0; i < functions.Length; i++)
            Named(functions[i], 2 + i + (i / 4 * 0.5), 0, $"F{i + 1}"); // par groupes de quatre
        Named(PhysicalKey.PrintScreen, 15.25, 0, "win_kb_print");
        Named(PhysicalKey.ScrollLock, 16.25, 0, "win_kb_scroll");
        Named(PhysicalKey.Pause, 17.25, 0, "Pause");

        // Chiffres
        Writes(PhysicalKey.Backquote, 0, 1.5, 0x29, "`");
        PhysicalKey[] digits = [PhysicalKey.Digit1, PhysicalKey.Digit2, PhysicalKey.Digit3, PhysicalKey.Digit4, PhysicalKey.Digit5,
            PhysicalKey.Digit6, PhysicalKey.Digit7, PhysicalKey.Digit8, PhysicalKey.Digit9, PhysicalKey.Digit0];
        for (var i = 0; i < digits.Length; i++)
            Writes(digits[i], 1 + i, 1.5, 0x02 + i, ((i + 1) % 10).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Writes(PhysicalKey.Minus, 11, 1.5, 0x0C, "-");
        Writes(PhysicalKey.Equal, 12, 1.5, 0x0D, "=");
        Named(PhysicalKey.Backspace, 13, 1.5, "win_key_backspace", width: 2);

        // Lettres (codes de balayage de la rangée du haut, du milieu, du bas)
        Named(PhysicalKey.Tab, 0, 2.5, "win_key_tab", width: 1.5);
        Row([PhysicalKey.Q, PhysicalKey.W, PhysicalKey.E, PhysicalKey.R, PhysicalKey.T, PhysicalKey.Y, PhysicalKey.U, PhysicalKey.I,
            PhysicalKey.O, PhysicalKey.P], 1.5, 2.5, 0x10, "QWERTYUIOP");
        Writes(PhysicalKey.BracketLeft, 11.5, 2.5, 0x1A, "[");
        Writes(PhysicalKey.BracketRight, 12.5, 2.5, 0x1B, "]");
        Named(PhysicalKey.Enter, 13.75, 2.5, "win_key_enter", width: 1.25, height: 2);

        Named(PhysicalKey.CapsLock, 0, 3.5, "win_kb_caps", width: 1.75);
        Row([PhysicalKey.A, PhysicalKey.S, PhysicalKey.D, PhysicalKey.F, PhysicalKey.G, PhysicalKey.H, PhysicalKey.J, PhysicalKey.K,
            PhysicalKey.L], 1.75, 3.5, 0x1E, "ASDFGHJKL");
        Writes(PhysicalKey.Semicolon, 10.75, 3.5, 0x27, ";");
        Writes(PhysicalKey.Quote, 11.75, 3.5, 0x28, "'");
        Writes(PhysicalKey.Backslash, 12.75, 3.5, 0x2B, "\\");

        Named(PhysicalKey.ShiftLeft, 0, 4.5, "win_kb_shift", width: 1.25);
        Writes(PhysicalKey.IntlBackslash, 1.25, 4.5, 0x56, "<");
        Row([PhysicalKey.Z, PhysicalKey.X, PhysicalKey.C, PhysicalKey.V, PhysicalKey.B, PhysicalKey.N, PhysicalKey.M], 2.25, 4.5, 0x2C, "ZXCVBNM");
        Writes(PhysicalKey.Comma, 9.25, 4.5, 0x33, ",");
        Writes(PhysicalKey.Period, 10.25, 4.5, 0x34, ".");
        Writes(PhysicalKey.Slash, 11.25, 4.5, 0x35, "/");
        Named(PhysicalKey.ShiftRight, 12.25, 4.5, "win_kb_shift", width: 2.75);

        Named(PhysicalKey.ControlLeft, 0, 5.5, "Ctrl", width: 1.25);
        Named(PhysicalKey.MetaLeft, 1.25, 5.5, "Win", width: 1.25);
        Named(PhysicalKey.AltLeft, 2.5, 5.5, "Alt", width: 1.25);
        Named(PhysicalKey.Space, 3.75, 5.5, "win_key_space", width: 6.25);
        Named(PhysicalKey.AltRight, 10, 5.5, "Alt Gr", width: 1.25);
        Named(PhysicalKey.MetaRight, 11.25, 5.5, "Win", width: 1.25);
        Named(PhysicalKey.ContextMenu, 12.5, 5.5, "win_key_menu", width: 1.25);
        Named(PhysicalKey.ControlRight, 13.75, 5.5, "Ctrl", width: 1.25);

        // Déplacement
        Named(PhysicalKey.Insert, 15.25, 1.5, "win_key_insert");
        Named(PhysicalKey.Home, 16.25, 1.5, "win_key_home");
        Named(PhysicalKey.PageUp, 17.25, 1.5, "win_key_page_up");
        Named(PhysicalKey.Delete, 15.25, 2.5, "win_key_delete");
        Named(PhysicalKey.End, 16.25, 2.5, "win_key_end");
        Named(PhysicalKey.PageDown, 17.25, 2.5, "win_key_page_down");
        Named(PhysicalKey.ArrowUp, 16.25, 4.5, "↑");
        Named(PhysicalKey.ArrowLeft, 15.25, 5.5, "←");
        Named(PhysicalKey.ArrowDown, 16.25, 5.5, "↓");
        Named(PhysicalKey.ArrowRight, 17.25, 5.5, "→");

        // Pavé numérique
        Named(PhysicalKey.NumLock, 18.5, 1.5, "win_kb_numlock");
        Named(PhysicalKey.NumPadDivide, 19.5, 1.5, "/");
        Named(PhysicalKey.NumPadMultiply, 20.5, 1.5, "*");
        Named(PhysicalKey.NumPadSubtract, 21.5, 1.5, "−");
        Named(PhysicalKey.NumPad7, 18.5, 2.5, "7");
        Named(PhysicalKey.NumPad8, 19.5, 2.5, "8");
        Named(PhysicalKey.NumPad9, 20.5, 2.5, "9");
        Named(PhysicalKey.NumPadAdd, 21.5, 2.5, "+", height: 2);
        Named(PhysicalKey.NumPad4, 18.5, 3.5, "4");
        Named(PhysicalKey.NumPad5, 19.5, 3.5, "5");
        Named(PhysicalKey.NumPad6, 20.5, 3.5, "6");
        Named(PhysicalKey.NumPad1, 18.5, 4.5, "1");
        Named(PhysicalKey.NumPad2, 19.5, 4.5, "2");
        Named(PhysicalKey.NumPad3, 20.5, 4.5, "3");
        Named(PhysicalKey.NumPadEnter, 21.5, 4.5, "win_key_enter", height: 2);
        Named(PhysicalKey.NumPad0, 18.5, 5.5, "0", width: 2);
        Named(PhysicalKey.NumPadDecimal, 20.5, 5.5, ".");
        return keys;

        void Row(PhysicalKey[] row, double x, double y, int firstScanCode, string fallback)
        {
            for (var i = 0; i < row.Length; i++)
                Writes(row[i], x + i, y, firstScanCode + i, fallback[i].ToString());
        }
    }
}
