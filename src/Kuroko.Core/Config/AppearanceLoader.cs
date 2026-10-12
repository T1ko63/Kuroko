using Tomlyn.Model;

namespace Kuroko.Core.Config;

/// <summary>
/// Reads the [appearance] block. A bad value never fails the file: it is reported (as a warning that is shown to the
/// user, with file and line), the default is used for that value, and everything else stays as configured.
/// </summary>
internal static class AppearanceLoader
{
    private static readonly HashSet<string> Keys = new(StringComparer.Ordinal) {
        "placeholder", "selection_style", "selection_opacity", "transparency", "radius", "blur", "border", "border_width", "separator", "separator_width", "font", "custom_corners", "dark", "light",
    };

    private static readonly HashSet<string> ColorKeys = new(StringComparer.Ordinal) { "background", "foreground", "selection", "border_color", "separator_color" };

    /// <param name="fontExists">Whether a font family is installed; null skips the check (Kuroko.Core cannot ask Windows itself).</param>
    public static AppearanceSettings Read(TomlTable root, TomlReader reader, Func<string, bool>? fontExists = null)
    {
        if (!root.TryGetValue("appearance", out var value)) return AppearanceSettings.Default;
        if (value is not TomlTable table)
        {
            Warn(reader, reader.LineOf("appearance"), "appearance must be a table ([appearance]); using the defaults");
            return AppearanceSettings.Default;
        }

        var block = reader.Block("[appearance]");
        int? Line(string key) => block is { } b ? reader.LineOf(key, b.Start, b.End) ?? b.Start : reader.LineOf(key);

        foreach (var key in table.Keys.Where(k => !Keys.Contains(k)))
        {
            Warn(reader, Line(key), $"[appearance] unknown setting '{key}'{SettingsLoader.MisplacedHint(key)}");
        }

        var transparency = ReadInt(table, "transparency", AppearanceSettings.DefaultTransparency, 0, 100, reader, Line);
        var radius = ReadInt(table, "radius", AppearanceSettings.DefaultRadius, 0, AppearanceSettings.MaxRadius, reader, Line);
        var blur = ReadBlur(table, reader, Line);
        var border = ReadBool(table, "border", AppearanceSettings.DefaultBorder, reader, Line);
        var customCorners = ReadBool(table, "custom_corners", AppearanceSettings.DefaultCustomCorners, reader, Line);
        var dark = ReadColors("dark", table, AppearanceSettings.DefaultDark, reader);
        var light = ReadColors("light", table, AppearanceSettings.DefaultLight, reader);
        var placeholder = ReadText(table, "placeholder", AppearanceSettings.DefaultPlaceholder, reader, Line);
        var separator = ReadBool(table, "separator", AppearanceSettings.DefaultSeparator, reader, Line);
        var borderWidth = ReadInt(table, "border_width", AppearanceSettings.DefaultLineWidth, 0, AppearanceSettings.MaxLineWidth, reader, Line);
        var separatorWidth = ReadInt(table, "separator_width", AppearanceSettings.DefaultLineWidth, 0, AppearanceSettings.MaxLineWidth, reader, Line);
        var font = ReadFont(table, reader, Line, fontExists);
        var selectionStyle = ReadSelectionStyle(table, reader, Line);
        var selectionOpacity = ReadInt(table, "selection_opacity", AppearanceSettings.DefaultSelectionOpacity, 0, 100, reader, Line);
        return new AppearanceSettings(transparency, radius, blur, dark, light, border, customCorners)
        {
            Placeholder = placeholder,
            Separator = separator,
            BorderWidth = borderWidth,
            SeparatorWidth = separatorWidth,
            Font = font,
            SelectionStyle = selectionStyle,
            SelectionOpacity = selectionOpacity,
        };
    }

    private static void Warn(TomlReader reader, int? line, string message) => reader.Issue(line, message, isError: false, show: true);

    private static int ReadInt(TomlTable table, string key, int fallback, int min, int max, TomlReader reader, Func<string, int?> line)
    {
        if (!table.TryGetValue(key, out var value)) return fallback;
        if (value is not long number)
        {
            Warn(reader, line(key), $"[appearance] {key} must be a whole number between {min} and {max}; using {fallback}");
            return fallback;
        }

        if (number < min || number > max)
        {
            Warn(reader, line(key), $"[appearance] {key} must be between {min} and {max} (is {number}); using {fallback}");
            return fallback;
        }

        return (int)number;
    }

    private static bool ReadBool(TomlTable table, string key, bool fallback, TomlReader reader, Func<string, int?> line)
    {
        if (!table.TryGetValue(key, out var value)) return fallback;
        if (value is bool flag) return flag;

        Warn(reader, line(key), $"[appearance] {key} must be true or false; using {(fallback ? "true" : "false")}");
        return fallback;
    }

    private static string ReadText(TomlTable table, string key, string fallback, TomlReader reader, Func<string, int?> line)
    {
        if (!table.TryGetValue(key, out var value)) return fallback;
        if (value is string text) return text;

        Warn(reader, line(key), $"[appearance] {key} must be a text in quotes; using \"{fallback}\"");
        return fallback;
    }

    /// <summary>
    /// A font name or a comma-separated list of them. An unknown name falls back silently in WPF, so it is reported here:
    /// when no name of the list is installed the default is used, otherwise the list stays (WPF takes the next name).
    /// </summary>
    private static string ReadFont(TomlTable table, TomlReader reader, Func<string, int?> line, Func<string, bool>? fontExists)
    {
        var font = ReadText(table, "font", AppearanceSettings.DefaultFont, reader, line);
        var names = font.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (names.Length == 0)
        {
            Warn(reader, line("font"), $"[appearance] font must name a font; using \"{AppearanceSettings.DefaultFont}\"");
            return AppearanceSettings.DefaultFont;
        }

        // The default list is not checked: Segoe UI Variable only exists on Windows 11, and Segoe UI stands behind it.
        var list = string.Join(", ", names);
        if (fontExists is null || list == AppearanceSettings.DefaultFont) return list;

        var missing = names.Where(n => !fontExists(n)).ToList();
        if (missing.Count == names.Length)
        {
            Warn(reader, line("font"), $"[appearance] font: {Quote(missing)} is not installed; using \"{AppearanceSettings.DefaultFont}\"");
            return AppearanceSettings.DefaultFont;
        }

        if (missing.Count > 0)
        {
            Warn(reader, line("font"), $"[appearance] font: {Quote(missing)} is not installed; the next font in the list is used");
        }

        return list;
    }

    private static string Quote(IEnumerable<string> names) => string.Join(", ", names.Select(n => $"\"{n}\""));

    private static SelectionStyle ReadSelectionStyle(TomlTable table, TomlReader reader, Func<string, int?> line)
    {
        if (!table.TryGetValue("selection_style", out var value)) return AppearanceSettings.DefaultSelectionStyle;
        switch ((value as string)?.Trim().ToLowerInvariant())
        {
            case "fill": return SelectionStyle.Fill;
            case "border": return SelectionStyle.Border;
            default:
                Warn(reader, line("selection_style"), "[appearance] selection_style must be \"fill\" or \"border\"; using \"fill\"");
                return AppearanceSettings.DefaultSelectionStyle;
        }
    }

    private static BlurMode ReadBlur(TomlTable table, TomlReader reader, Func<string, int?> line)
    {
        if (!table.TryGetValue("blur", out var value)) return AppearanceSettings.DefaultBlur;
        switch ((value as string)?.Trim().ToLowerInvariant())
        {
            case "acrylic": return BlurMode.Acrylic;
            case "mica": return BlurMode.Mica;
            case "micaalt": return BlurMode.MicaAlt;
            case "blur": return BlurMode.Blur;
            case "none": return BlurMode.None;
            default:
                Warn(reader, line("blur"), "[appearance] blur must be \"acrylic\", \"mica\", \"micaalt\", \"blur\" or \"none\"; using \"acrylic\"");
                return AppearanceSettings.DefaultBlur;
        }
    }

    private static ThemeColors ReadColors(string theme, TomlTable appearance, ThemeColors defaults, TomlReader reader)
    {
        if (!appearance.TryGetValue(theme, out var value)) return defaults;

        var header = $"[appearance.{theme}]";
        if (value is not TomlTable table)
        {
            Warn(reader, reader.LineOf(theme), $"{header} must be a table; using the defaults");
            return defaults;
        }

        var block = reader.Block(header);
        int? Line(string key) => block is { } b ? reader.LineOf(key, b.Start, b.End) ?? b.Start : reader.LineOf(key);

        foreach (var key in table.Keys.Where(k => !ColorKeys.Contains(k)))
        {
            Warn(reader, Line(key), $"{header} unknown setting '{key}'{SettingsLoader.MisplacedHint(key)}");
        }

        var background = ReadColor(table, "background", defaults.Background, header, reader, Line);
        var foreground = ReadColor(table, "foreground", defaults.Foreground, header, reader, Line);
        var selection = ReadColor(table, "selection", defaults.Selection, header, reader, Line);
        var border = ReadOptionalColor(table, "border_color", header, reader, Line);
        var separator = ReadOptionalColor(table, "separator_color", header, reader, Line);
        return new ThemeColors(background, foreground, selection, border, separator);
    }

    /// <summary>A colour that is derived when missing: an invalid one is reported and derived as well.</summary>
    private static Rgba? ReadOptionalColor(TomlTable table, string key, string header, TomlReader reader, Func<string, int?> line)
    {
        if (!table.TryGetValue(key, out var value)) return null;
        if (!ColorParser.TryParse(value as string, out var color, out var hasAlpha))
        {
            Warn(reader, line(key), $"{header} {key} is not a valid colour (expected #RRGGBB or rgb(r, g, b)); derived from foreground");
            return null;
        }

        if (hasAlpha) Warn(reader, line(key), $"{header} {key}: alpha ignored (use #RRGGBB or rgb())");
        return Rgba.Opaque(color.R, color.G, color.B);
    }

    /// <summary>An opaque colour. An alpha part (#AARRGGBB, rgba()) is ignored and reported: opacity has its own settings.</summary>
    private static Rgba ReadColor(TomlTable table, string key, Rgba fallback, string header, TomlReader reader, Func<string, int?> line)
    {
        if (!table.TryGetValue(key, out var value)) return fallback;

        if (!ColorParser.TryParse(value as string, out var color, out var hasAlpha))
        {
            Warn(reader, line(key), $"{header} {key} is not a valid colour (expected #RRGGBB or rgb(r, g, b)); using {Show(fallback)}");
            return fallback;
        }

        if (hasAlpha)
        {
            // Opacity has exactly one place each: [appearance] transparency for the window, selection_opacity for the selected row.
            var where = key == "selection" ? "the row's opacity is set by [appearance] selection_opacity" : "the window's transparency is set by transparency";
            Warn(reader, line(key), $"{header} {key}: alpha ignored (use #RRGGBB or rgb(); {where})");
        }

        return Rgba.Opaque(color.R, color.G, color.B);
    }

    private static string Show(Rgba color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}
