using System.Globalization;
using Kuroko.Core.Hotkeys;
using static Kuroko.Platform.Native.NativeMethods;

namespace Kuroko.Platform.Hotkeys;

/// <summary>
/// A Windows keyboard layout (HKL), asked through VkKeyScanEx and ToUnicodeEx like AutoHotkey does.
/// A snapshot: the layout active when <see cref="Active"/> was called, so a config load stays consistent.
/// </summary>
public sealed class Win32KeyLayout : IKeyLayout
{
    private const int VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12;
    private const int VK_LSHIFT = 0xA0, VK_LCONTROL = 0xA2, VK_RMENU = 0xA5;
    private const byte Down = 0x80;

    private readonly nint _hkl;

    private Win32KeyLayout(nint hkl)
    {
        _hkl = hkl;
        Name = NameOf(hkl);
    }

    /// <summary>The layout of the window in front (what the user types with right now), else this thread's.</summary>
    public static Win32KeyLayout Active()
    {
        var foreground = GetForegroundWindow();
        var hkl = foreground != 0 ? GetKeyboardLayout(GetWindowThreadProcessId(foreground, out _)) : 0;
        return new Win32KeyLayout(hkl != 0 ? hkl : GetKeyboardLayout(0));
    }

    public string Name { get; }

    /// <summary>The raw handle, for the log (e.g. 0x04070407 = German).</summary>
    public string Handle => $"0x{(long)_hkl & 0xFFFFFFFF:X8}";

    public bool TryFindKey(char character, out int virtualKey, out KeyLayer layer)
    {
        var result = VkKeyScanEx(character, _hkl);
        if (result == -1)
        {
            virtualKey = 0;
            layer = KeyLayer.Other;
            return false;
        }

        virtualKey = result & 0xFF;
        layer = ((result >> 8) & 0xFF) switch
        {
            0 => KeyLayer.Base,
            1 => KeyLayer.Shift,
            6 => KeyLayer.AltGr, // Ctrl+Alt
            _ => KeyLayer.Other,
        };
        return true;
    }

    public string? CharOf(int virtualKey, KeyLayer layer)
    {
        var state = new byte[256];
        switch (layer)
        {
            case KeyLayer.Base:
                break;
            case KeyLayer.Shift:
                state[VK_SHIFT] = state[VK_LSHIFT] = Down;
                break;
            case KeyLayer.AltGr:
                state[VK_CONTROL] = state[VK_LCONTROL] = state[VK_MENU] = state[VK_RMENU] = Down;
                break;
            default:
                return null;
        }

        var scanCode = MapVirtualKeyEx((uint)virtualKey, MAPVK_VK_TO_VSC, _hkl);
        var buffer = new char[8];
        var count = ToUnicodeEx((uint)virtualKey, scanCode, state, buffer, buffer.Length, TOUNICODE_NO_STATE_CHANGE, _hkl);
        return count switch
        {
            < 0 => buffer[0].ToString(), // dead key (^, ´ on German): its own character
            0 => null,
            _ => new string(buffer, 0, count),
        };
    }

    private static string NameOf(nint hkl)
    {
        var language = (int)((long)hkl & 0xFFFF);
        try
        {
            return CultureInfo.GetCultureInfo(language).EnglishName;
        }
        catch (CultureNotFoundException)
        {
            return $"0x{(long)hkl & 0xFFFFFFFF:X8}";
        }
    }
}
