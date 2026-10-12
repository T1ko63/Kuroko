using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Kuroko.Core.Hotkeys;

/// <summary>Modifier flags. Values intentionally match the Win32 MOD_* constants.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 0x1,
    Ctrl = 0x2,
    Shift = 0x4,
    Win = 0x8,
}

/// <summary>
/// A parsed hotkey such as "Ctrl+Alt+K". Pure data, no OS calls, so it can be validated
/// (duplicates, missing modifiers) in the config layer and unit-tested.
/// The key is a virtual-key code: written as a name (K, F5, Space, Num1, Oem102), as a hex code (0xE2),
/// or as the character the key types on the keyboard layout (ü, &lt;, #), resolved through an <see cref="IKeyLayout"/>.
/// </summary>
public readonly record struct HotkeyGesture(HotkeyModifiers Modifiers, int VirtualKey)
{
    /// <summary>Where the messages below are explained; appended to the ones that need more than one line.</summary>
    public const string DocsHint = " See docs/keybindings.md.";

    // Named keys; the first name of a code is the one ToString shows.
    private static readonly (string Name, int Code)[] Names =
    [
        ("Space", 0x20), ("Enter", 0x0D), ("Return", 0x0D), ("Tab", 0x09), ("Esc", 0x1B), ("Escape", 0x1B), ("Backspace", 0x08),
        ("Insert", 0x2D), ("Delete", 0x2E), ("Del", 0x2E),
        ("Home", 0x24), ("End", 0x23), ("PageUp", 0x21), ("PageDown", 0x22),
        ("Left", 0x25), ("Up", 0x26), ("Right", 0x27), ("Down", 0x28),
        ("PrintScreen", 0x2C), ("Pause", 0x13), ("ScrollLock", 0x91), ("CapsLock", 0x14), ("NumLock", 0x90),
        ("Menu", 0x5D), ("Apps", 0x5D),
        ("Num0", 0x60), ("Num1", 0x61), ("Num2", 0x62), ("Num3", 0x63), ("Num4", 0x64),
        ("Num5", 0x65), ("Num6", 0x66), ("Num7", 0x67), ("Num8", 0x68), ("Num9", 0x69),
        ("NumMultiply", 0x6A), ("NumAdd", 0x6B), ("NumSubtract", 0x6D), ("NumDecimal", 0x6E), ("NumDivide", 0x6F),
        ("BrowserBack", 0xA6), ("BrowserForward", 0xA7), ("BrowserRefresh", 0xA8), ("BrowserStop", 0xA9),
        ("BrowserSearch", 0xAA), ("BrowserFavorites", 0xAB), ("BrowserHome", 0xAC),
        ("VolumeMute", 0xAD), ("VolumeDown", 0xAE), ("VolumeUp", 0xAF),
        ("MediaNext", 0xB0), ("MediaPrev", 0xB1), ("MediaStop", 0xB2), ("MediaPlayPause", 0xB3),
        ("LaunchMail", 0xB4), ("LaunchMediaSelect", 0xB5), ("LaunchApp1", 0xB6), ("LaunchApp2", 0xB7),
    ];

    // Keys whose character depends on the layout. ToString shows that character when the layout has one, else this name.
    private static readonly (string Name, int Code)[] OemNames =
    [
        ("Oem1", 0xBA), ("OemPlus", 0xBB), ("Plus", 0xBB), ("OemComma", 0xBC), ("OemMinus", 0xBD), ("OemPeriod", 0xBE),
        ("Oem2", 0xBF), ("Oem3", 0xC0), ("Oem4", 0xDB), ("Oem5", 0xDC), ("Oem6", 0xDD), ("Oem7", 0xDE), ("Oem8", 0xDF),
        ("Oem102", 0xE2),
    ];

    private static readonly Dictionary<string, int> NamedKeys = Names.Concat(OemNames)
        .ToDictionary(n => n.Name, n => n.Code, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<int, string> DisplayNames = Names.Concat(OemNames)
        .GroupBy(n => n.Code).ToDictionary(g => g.Key, g => g.First().Name);

    private static readonly HashSet<int> LayoutKeys = OemNames.Select(n => n.Code).ToHashSet();

    private static readonly Dictionary<string, HotkeyModifiers> ModifierNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ctrl"] = HotkeyModifiers.Ctrl, ["Control"] = HotkeyModifiers.Ctrl, ["Strg"] = HotkeyModifiers.Ctrl,
        ["Alt"] = HotkeyModifiers.Alt,
        ["Shift"] = HotkeyModifiers.Shift, ["Umschalt"] = HotkeyModifiers.Shift,
        ["Win"] = HotkeyModifiers.Win, ["Windows"] = HotkeyModifiers.Win,
    };

    // Codes that are not keyboard keys of their own: mouse buttons and the modifier keys.
    private static readonly Dictionary<int, string> MouseButtons = new()
    {
        [0x01] = "the left mouse button", [0x02] = "the right mouse button", [0x04] = "the middle mouse button",
        [0x05] = "mouse button X1", [0x06] = "mouse button X2",
    };

    private static readonly Dictionary<int, string> ModifierKeys = new()
    {
        [0x10] = "Shift", [0x11] = "Ctrl", [0x12] = "Alt", [0x5B] = "Win", [0x5C] = "Win",
        [0xA0] = "Shift", [0xA1] = "Shift", [0xA2] = "Ctrl", [0xA3] = "Ctrl", [0xA4] = "Alt", [0xA5] = "Alt",
    };

    public static HotkeyGesture Parse(string text) => Parse(text, KeyLayouts.Current);

    public static HotkeyGesture Parse(string text, IKeyLayout? layout)
        => TryParse(text, layout, out var gesture, out var error)
            ? gesture
            : throw new FormatException(error);

    public static HotkeyGesture Parse(string text, IReadOnlyDictionary<string, HotkeyModifiers>? mods, IKeyLayout? layout)
        => TryParse(text, mods, layout, out var gesture, out var error)
            ? gesture
            : throw new FormatException(error);

    /// <summary>
    /// Fills in modifier variables ("$card+S", see <see cref="HotkeyMods"/>) and parses the result. Errors in the
    /// expanded text name both what was written and what it became.
    /// </summary>
    public static bool TryParse(
        string? text, IReadOnlyDictionary<string, HotkeyModifiers>? mods, IKeyLayout? layout,
        out HotkeyGesture gesture, [NotNullWhen(false)] out string? error)
    {
        gesture = default;
        if (!HotkeyMods.UsesVariables(text)) return TryParse(text, layout, out gesture, out error);
        if (!HotkeyMods.TryExpand(text!, mods, out var expanded, out error)) return false;
        if (TryParse(expanded, layout, out gesture, out error)) return true;

        var hint = error.EndsWith(DocsHint, StringComparison.Ordinal) ? DocsHint : string.Empty;
        error = $"{error[..^hint.Length].TrimEnd('.')} (written as '{text}', which is '{expanded}' with [mods] filled in).{hint}";
        return false;
    }

    /// <summary>Parses against <see cref="KeyLayouts.Current"/>.</summary>
    public static bool TryParse(string? text, out HotkeyGesture gesture, [NotNullWhen(false)] out string? error)
        => TryParse(text, KeyLayouts.Current, out gesture, out error);

    /// <param name="layout">Resolves keys written as a character (ü, &lt;); null allows only names and codes.</param>
    public static bool TryParse(string? text, IKeyLayout? layout, out HotkeyGesture gesture, [NotNullWhen(false)] out string? error)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Hotkey is empty.";
            return false;
        }

        if (!TrySplit(text.Trim(), out var modifierParts, out var keyName))
        {
            error = $"Hotkey '{text}' is malformed.";
            return false;
        }

        var modifiers = HotkeyModifiers.None;
        foreach (var part in modifierParts)
        {
            if (!ModifierNames.TryGetValue(part, out var modifier))
            {
                error = $"Unknown modifier '{part}' in hotkey '{text}'.";
                return false;
            }

            if (modifiers.HasFlag(modifier))
            {
                error = $"Modifier '{part}' appears twice in hotkey '{text}'.";
                return false;
            }

            modifiers |= modifier;
        }

        if (ModifierNames.ContainsKey(keyName))
        {
            error = $"Hotkey '{text}' has no key: '{keyName}' is a modifier, and a modifier alone cannot be a hotkey.{DocsHint}";
            return false;
        }

        if (!TryParseKey(keyName, text, layout, out var vk, out error)) return false;

        var isFunctionKey = vk is >= 0x70 and <= 0x87;
        if (modifiers == HotkeyModifiers.None && !isFunctionKey)
        {
            error = $"Hotkey '{text}' needs at least one modifier (Ctrl, Alt, Shift or Win).";
            return false;
        }

        gesture = new HotkeyGesture(modifiers, vk);
        error = null;
        return true;
    }

    /// <summary>Splits "Ctrl+Alt+K" into modifiers and key; a trailing "++" makes "+" the key ("Ctrl+Alt++").</summary>
    private static bool TrySplit(string text, out string[] modifiers, out string key)
    {
        modifiers = [];
        key = string.Empty;
        string modifierText;
        if (text.EndsWith('+'))
        {
            var rest = text[..^1].TrimEnd();
            if (rest.Length == 0)
            {
                key = "+";
                return true;
            }

            if (!rest.EndsWith('+')) return false;
            key = "+";
            modifierText = rest[..^1];
        }
        else
        {
            var last = text.LastIndexOf('+');
            key = text[(last + 1)..].Trim();
            if (last < 0) return true;
            modifierText = text[..last];
        }

        modifiers = modifierText.Split('+', StringSplitOptions.TrimEntries);
        return modifiers.All(m => m.Length > 0) && key.Length > 0;
    }

    private static bool TryParseKey(string name, string text, IKeyLayout? layout, out int vk, [NotNullWhen(false)] out string? error)
    {
        vk = 0;
        error = null;
        if (name.Length == 1 && char.IsAsciiLetter(name[0]))
        {
            vk = char.ToUpperInvariant(name[0]);
            return true;
        }

        if (name.Length == 1 && char.IsAsciiDigit(name[0]))
        {
            vk = name[0];
            return true;
        }

        if ((name[0] is 'F' or 'f') && int.TryParse(name.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n is >= 1 and <= 24)
        {
            vk = 0x70 + n - 1;
            return true;
        }

        if (name.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && name.Length > 2)
        {
            return TryParseCode(name, text, out vk, out error);
        }

        if (NamedKeys.TryGetValue(name, out vk)) return true;

        if (name.Length == 1 && !char.IsControl(name[0]))
        {
            return TryResolveCharacter(name[0], text, layout, out vk, out error);
        }

        error = $"Unknown key '{name}' in hotkey '{text}'.{DocsHint}";
        return false;
    }

    private static bool TryParseCode(string name, string text, out int vk, [NotNullWhen(false)] out string? error)
    {
        error = null;
        if (name.Length > 4 || !int.TryParse(name.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out vk))
        {
            vk = 0;
            error = $"Key code '{name}' in hotkey '{text}' is not a hex code like 0xE2.{DocsHint}";
            return false;
        }

        if (vk is < 0x01 or > 0xFE)
        {
            error = $"Key code '{name}' in hotkey '{text}' is out of range: key codes go from 0x01 to 0xFE.{DocsHint}";
            return false;
        }

        if (MouseButtons.TryGetValue(vk, out var button))
        {
            error = $"Key code '{name}' in hotkey '{text}' is {button}: only keyboard keys can be hotkeys.{DocsHint}";
            return false;
        }

        if (ModifierKeys.TryGetValue(vk, out var modifier))
        {
            error = $"Key code '{name}' in hotkey '{text}' is the {modifier} key: a modifier alone cannot be a hotkey.{DocsHint}";
            return false;
        }

        return true;
    }

    /// <summary>"ü", "&lt;", "#": the key that types the character on its own, without Shift or AltGr.</summary>
    private static bool TryResolveCharacter(char c, string text, IKeyLayout? layout, out int vk, [NotNullWhen(false)] out string? error)
    {
        vk = 0;
        error = null;
        if (layout is null)
        {
            error = $"'{c}' in hotkey '{text}' cannot be resolved: no keyboard layout is available. Write the key's code instead, e.g. 0xBA.{DocsHint}";
            return false;
        }

        // Ü means the ü key, as K means the k key.
        var lower = char.ToLowerInvariant(c);
        var layer = KeyLayer.Other;
        var found = (lower != c && layout.TryFindKey(lower, out vk, out layer) && layer == KeyLayer.Base)
                    || layout.TryFindKey(c, out vk, out layer);
        if (!found)
        {
            error = $"No key on the active keyboard layout ({layout.Name}) types '{c}' (hotkey '{text}'). Write the key's name or code instead.{DocsHint}";
            return false;
        }

        if (layer == KeyLayer.Base) return true;

        var key = KeyName(vk, layout);
        var how = layer switch
        {
            KeyLayer.Shift => $"Shift+{key}",
            KeyLayer.AltGr => $"AltGr+{key}",
            _ => $"typed with extra modifiers on the key {key}",
        };
        error = $"'{c}' in hotkey '{text}' is {how} on the active keyboard layout ({layout.Name}). A hotkey names the key, not the character: "
                + $"write '{key}' and add the modifiers you want (e.g. Ctrl+Shift+{key}).{DocsHint}";
        vk = 0;
        return false;
    }

    /// <summary>
    /// The character a Ctrl+Alt hotkey takes away: Windows treats Ctrl+Alt like AltGr, so Ctrl+Alt+Q on a German layout
    /// is also how '@' is typed, and a registered hotkey swallows it. Null when nothing is lost.
    /// </summary>
    public string? SwallowedAltGrCharacter(IKeyLayout? layout)
    {
        // Only plain Ctrl+Alt: Shift+AltGr characters are rare and not checked.
        if (layout is null || Modifiers != (HotkeyModifiers.Ctrl | HotkeyModifiers.Alt)) return null;
        var text = layout.CharOf(VirtualKey, KeyLayer.AltGr);
        return string.IsNullOrEmpty(text) || text.Any(char.IsControl) ? null : text;
    }

    /// <summary>Warning text for <see cref="SwallowedAltGrCharacter"/>, or null.</summary>
    public string? AltGrWarning(IKeyLayout? layout)
        => SwallowedAltGrCharacter(layout) is { } swallowed
            ? $"{ToString(layout)} is also AltGr+{KeyName(VirtualKey, layout)}, which types '{swallowed}' on the active keyboard layout ({layout!.Name}); "
              + $"while Kuroko runs, '{swallowed}' cannot be typed with AltGr. Choose another key or other modifiers.{DocsHint}"
            : null;

    /// <summary>Canonical text, keys shown as the character of <see cref="KeyLayouts.Current"/> where they have one.</summary>
    public override string ToString() => ToString(KeyLayouts.Current);

    public string ToString(IKeyLayout? layout)
    {
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(HotkeyModifiers.Ctrl)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(KeyName(VirtualKey, layout));
        return string.Join('+', parts);
    }

    private static string KeyName(int vk, IKeyLayout? layout)
    {
        if (vk is >= 0x41 and <= 0x5A or >= 0x30 and <= 0x39) return ((char)vk).ToString();
        if (vk is >= 0x70 and <= 0x87) return $"F{vk - 0x70 + 1}";
        if (!LayoutKeys.Contains(vk) && DisplayNames.TryGetValue(vk, out var name)) return name;
        if (LayoutCharacter(vk, layout) is { } character) return character.ToString();
        return DisplayNames.TryGetValue(vk, out name) ? name : $"0x{vk:X2}";
    }

    /// <summary>The key's own character, if typing it back finds the same key (so ToString round-trips).</summary>
    private static char? LayoutCharacter(int vk, IKeyLayout? layout)
    {
        if (layout?.CharOf(vk, KeyLayer.Base) is not { Length: 1 } text) return null;
        var c = text[0];
        if (char.IsControl(c) || char.IsWhiteSpace(c) || char.IsAsciiLetterOrDigit(c)) return null;
        return layout.TryFindKey(c, out var back, out var layer) && back == vk && layer == KeyLayer.Base ? c : null;
    }
}
