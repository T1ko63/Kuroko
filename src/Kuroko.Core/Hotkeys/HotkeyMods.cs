using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Kuroko.Core.Hotkeys;

/// <summary>
/// Modifier variables ([mods] in keybinds.toml): a name for a modifier combination, used in hotkeys as "$name"
/// ("$card+S" with card = "Ctrl+Alt" is Ctrl+Alt+S). Every hotkey goes through <see cref="TryExpand"/> before it is parsed.
/// </summary>
public static class HotkeyMods
{
    public static IReadOnlyDictionary<string, HotkeyModifiers> None { get; } =
        new Dictionary<string, HotkeyModifiers>(StringComparer.OrdinalIgnoreCase);

    // The one table of modifier names: hotkeys (HotkeyGesture) and [mods] values are both read through it.
    private static readonly Dictionary<string, HotkeyModifiers> ModifierNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ctrl"] = HotkeyModifiers.Ctrl, ["Control"] = HotkeyModifiers.Ctrl, ["Strg"] = HotkeyModifiers.Ctrl,
        ["Alt"] = HotkeyModifiers.Alt,
        ["Shift"] = HotkeyModifiers.Shift, ["Umschalt"] = HotkeyModifiers.Shift,
        ["Win"] = HotkeyModifiers.Win, ["Windows"] = HotkeyModifiers.Win,
    };

    /// <summary>A modifier written in any of its spellings: Ctrl (Control, Strg), Alt, Shift (Umschalt), Win (Windows).</summary>
    public static bool TryGetModifier(string name, out HotkeyModifiers modifier) => ModifierNames.TryGetValue(name, out modifier);

    public static bool IsModifier(string name) => ModifierNames.ContainsKey(name);

    /// <summary>Letters, digits and underscore.</summary>
    public static bool IsValidName(string name) => name.Length > 0 && name.All(IsNameChar);

    private static bool IsNameChar(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';

    /// <summary>The value of a [mods] entry: one or more modifiers joined with +, nothing else.</summary>
    public static bool TryParseValue(string value, out HotkeyModifiers modifiers, [NotNullWhen(false)] out string? error)
    {
        modifiers = HotkeyModifiers.None;
        var parts = value.Split('+', StringSplitOptions.TrimEntries);
        if (string.IsNullOrWhiteSpace(value) || parts.Any(p => p.Length == 0))
        {
            error = $"\"{value}\" is not a modifier combination like \"Ctrl+Alt\"";
            return false;
        }

        foreach (var part in parts)
        {
            if (part.StartsWith('$'))
            {
                error = $"\"{value}\" uses the variable '{part}'; a variable holds modifiers only, not other variables";
                return false;
            }

            if (!TryGetModifier(part, out var modifier))
            {
                error = $"\"{value}\": '{part}' is not a modifier (Ctrl, Alt, Shift or Win); a variable holds modifiers only, the key comes in the hotkey";
                return false;
            }

            if (modifiers.HasFlag(modifier))
            {
                error = $"\"{value}\": modifier '{part}' appears twice";
                return false;
            }

            modifiers |= modifier;
        }

        error = null;
        return true;
    }

    /// <summary>"Ctrl+Alt+Shift+Win" order, as <see cref="HotkeyGesture.ToString()"/> writes modifiers.</summary>
    public static string ToText(HotkeyModifiers modifiers)
    {
        var parts = new List<string>(4);
        if (modifiers.HasFlag(HotkeyModifiers.Ctrl)) parts.Add("Ctrl");
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        return string.Join('+', parts);
    }

    /// <summary>Whether the text uses a variable at all.</summary>
    public static bool UsesVariables(string? text) => text?.Contains('$') == true;

    /// <summary>Replaces every "$name" by its modifiers; text without '$' comes back unchanged.</summary>
    public static bool TryExpand(string text, IReadOnlyDictionary<string, HotkeyModifiers>? mods, out string expanded, [NotNullWhen(false)] out string? error)
    {
        expanded = text;
        error = null;
        if (!UsesVariables(text)) return true;

        var result = new StringBuilder(text.Length + 16);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '$')
            {
                result.Append(text[i]);
                continue;
            }

            var end = i + 1;
            while (end < text.Length && IsNameChar(text[end])) end++;
            var name = text[(i + 1)..end];
            if (name.Length == 0)
            {
                error = $"'$' in hotkey '{text}' must be followed by a variable name (letters, digits, _).{HotkeyGesture.DocsHint}";
                return false;
            }

            if (mods is null || !mods.TryGetValue(name, out var modifiers))
            {
                var known = mods is { Count: > 0 }
                    ? $"Defined in [mods] of keybinds.toml: {string.Join(", ", mods.Keys.Order(StringComparer.OrdinalIgnoreCase).Select(k => "$" + k))}."
                    : "No variables are defined: add them to a [mods] table in keybinds.toml.";
                error = $"Unknown variable '${name}' in hotkey '{text}'. {known}{HotkeyGesture.DocsHint}";
                return false;
            }

            result.Append(ToText(modifiers));
            i = end - 1;
        }

        expanded = result.ToString();
        return true;
    }
}
