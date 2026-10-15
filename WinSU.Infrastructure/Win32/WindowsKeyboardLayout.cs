using System.Globalization;
using System.Runtime.InteropServices;
using WinSU.Core.Input;

namespace WinSU.Infrastructure.Win32;

/// <summary>
/// Disposition du clavier en vigueur dans Windows (celle du fil qui appelle, le fil d'interface) : la touche d'un code de
/// balayage → sa touche virtuelle → le caractère qu'elle écrit sans modificateur. Une touche morte (^ ¨ en AZERTY) donne
/// son accent. N'écrit rien dans l'état du clavier (indicateur 0x4 de ToUnicodeEx, Windows 10 1607 et plus).
/// </summary>
public sealed partial class WindowsKeyboardLayout : IKeyboardLayout
{
    private const uint MapVkVscToVkEx = 3;
    private const uint DontChangeKeyboardState = 0x4;

    public string? CharacterOf(int scanCode)
    {
        var layout = GetKeyboardLayout(0);
        var virtualKey = MapVirtualKeyExW((uint)scanCode, MapVkVscToVkEx, layout);
        if (virtualKey == 0)
            return null;

        var state = new byte[256];
        var buffer = new char[8];
        var count = ToUnicodeEx(virtualKey, (uint)scanCode, state, buffer, buffer.Length, DontChangeKeyboardState, layout);
        if (count == 0)
            return null;

        // count < 0 : touche morte, son accent est dans le premier caractère
        var text = count < 0 ? buffer[0].ToString() : new string(buffer, 0, count);
        return string.IsNullOrWhiteSpace(text) || char.IsControl(text[0]) ? null : text.ToUpper(CultureInfo.CurrentCulture);
    }

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetKeyboardLayout(uint threadId);

    [LibraryImport("user32.dll")]
    private static partial uint MapVirtualKeyExW(uint code, uint mapType, IntPtr layout);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int ToUnicodeEx(
        uint virtualKey,
        uint scanCode,
        [In] byte[] keyState,
        [Out] char[] buffer,
        int bufferSize,
        uint flags,
        IntPtr layout);
}
