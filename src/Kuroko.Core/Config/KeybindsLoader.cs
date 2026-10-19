using Kuroko.Core.Hotkeys;
using Tomlyn;
using Tomlyn.Model;

namespace Kuroko.Core.Config;

/// <summary>
/// Reads and validates keybinds.toml: [mods], [app], [prompts] and [submaps.name]. Never throws for bad content: problems
/// come back as <see cref="ConfigIssue"/>s. Checks that need prompts.toml or settings.toml (prompt names, hotkeys used twice,
/// Ctrl+C/V/A) are made by <see cref="ConfigValidator"/>.
/// </summary>
public static class KeybindsLoader
{
    public const string FileName = "keybinds.toml";

    private static readonly HashSet<string> TopLevelKeys = new(StringComparer.Ordinal) { "mods", "app", "prompts", "submaps" };

    /// <summary>The keys of [app] and their defaults.</summary>
    public static IReadOnlyList<(string Key, string Default)> AppKeys { get; } =
    [
        ("overlay", KeybindSettings.DefaultOverlayHotkey),
        ("undo", KeybindSettings.DefaultUndoHotkey),
        ("result_copy", KeybindSettings.DefaultResultCopyHotkey),
    ];

    private static readonly HashSet<string> SubmapFields = new(StringComparer.Ordinal) { "open", "label", "timeout", "keys" };

    /// <param name="keyLayout">Resolves hotkey keys written as a character; null uses <see cref="KeyLayouts.Current"/>.</param>
    public static KeybindsLoadResult Parse(string toml, IKeyLayout? keyLayout = null)
    {
        var issues = new List<ConfigIssue>();
        var layout = keyLayout ?? KeyLayouts.Current;

        TomlTable root;
        try
        {
            root = TomlSerializer.Deserialize<TomlTable>(toml) ?? new TomlTable();
        }
        catch (TomlException ex)
        {
            // Syntax error: the caller keeps the last working configuration.
            issues.Add(new ConfigIssue(FileName, ex.Line > 0 ? ex.Line : null, ex.Message, IsError: true));
            return new KeybindsLoadResult(null, issues);
        }

        var reader = new TomlReader(FileName, toml, issues);
        reader.WarnAboutDuplicateKeys();

        foreach (var key in root.Keys.Where(k => !TopLevelKeys.Contains(k)))
        {
            reader.Issue(reader.LineOf(key), $"unknown entry '{key}' (keybinds.toml has [mods], [app], [prompts] and [submaps.name])", isError: false);
        }

        var mods = ReadMods(root, reader);
        var app = ReadApp(root, reader, layout, mods);
        var prompts = ReadPrompts(root, reader, layout, mods);
        var submaps = ReadSubmaps(root, reader, layout, mods);

        var keybinds = new KeybindSettings
        {
            Overlay = app["overlay"],
            Undo = app["undo"],
            ResultCopy = app["result_copy"],
            Prompts = prompts,
            Mods = mods,
            Submaps = submaps,
        };
        return new KeybindsLoadResult(issues.Any(i => i.IsError) ? null : keybinds, issues);
    }

    /// <summary>Plain Ctrl+A / Ctrl+C / Ctrl+V: the keys the app simulates itself while reading or replacing text.</summary>
    public static bool CollidesWithCapture(HotkeyGesture gesture)
        => gesture.Modifiers == HotkeyModifiers.Ctrl && gesture.VirtualKey is 'A' or 'C' or 'V';

    /// <summary>Lines of a table header such as [app]: <paramref name="key"/> inside it, also quoted; the header when not found.</summary>
    private static Func<string, int?> LinesIn(TomlReader reader, string header)
    {
        var block = reader.Block(header);
        return key => block is { } b
            ? reader.LineOf(key, b.Start, b.End) ?? reader.LineOf($"\"{key}\"", b.Start, b.End) ?? reader.LineOf($"'{key}'", b.Start, b.End) ?? b.Start
            : reader.LineOf(key);
    }

    /// <summary>The hotkey with its [mods] variables filled in, so everything that uses it later sees plain modifiers.</summary>
    /// <param name="label">Names the hotkey in messages.</param>
    private static string ReadHotkey(
        TomlTable table, TomlReader reader, string key, string fallback, IKeyLayout? layout, IReadOnlyDictionary<string, HotkeyModifiers> mods,
        Func<string, int?> line, string label)
    {
        var text = reader.Choice(table, key, fallback, null, line);
        if (!HotkeyGesture.TryParse(text, mods, layout, out var gesture, out var error))
        {
            reader.Issue(line(key), $"{label}: {error}", isError: true);
            return text;
        }

        if (gesture.AltGrWarning(layout) is { } warning)
        {
            reader.Issue(line(key), $"{label}: {warning}", isError: false, show: true);
        }

        return HotkeyMods.TryExpand(text, mods, out var expanded, out _) ? expanded : text;
    }

    /// <summary>[app]: overlay, undo and result_copy. A missing key keeps its default.</summary>
    private static Dictionary<string, Keybind> ReadApp(TomlTable root, TomlReader reader, IKeyLayout? layout, IReadOnlyDictionary<string, HotkeyModifiers> mods)
    {
        var result = AppKeys.ToDictionary(k => k.Key, k => new Keybind(k.Default));
        if (!root.TryGetValue("app", out var value)) return result;
        if (value is not TomlTable table)
        {
            reader.Issue(reader.LineOf("app"), "app must be a table: [app] with lines like overlay = \"Ctrl+Shift+Space\"", isError: true);
            return result;
        }

        var line = LinesIn(reader, "[app]");
        foreach (var key in table.Keys.Where(k => AppKeys.All(a => a.Key != k)))
        {
            reader.Issue(line(key), $"[app] unknown hotkey '{key}' (known: {string.Join(", ", AppKeys.Select(a => a.Key))})", isError: false);
        }

        foreach (var (key, fallback) in AppKeys)
        {
            if (!table.ContainsKey(key)) continue;
            result[key] = new Keybind(ReadHotkey(table, reader, key, fallback, layout, mods, line, $"[app] {key}"), line(key));
        }

        return result;
    }

    /// <summary>[prompts]: prompt name = hotkey. "" means no hotkey. Whether each name is a prompt is checked against prompts.toml later.</summary>
    private static List<PromptKeybind> ReadPrompts(TomlTable root, TomlReader reader, IKeyLayout? layout, IReadOnlyDictionary<string, HotkeyModifiers> mods)
    {
        var prompts = new List<PromptKeybind>();
        if (!root.TryGetValue("prompts", out var value)) return prompts;
        if (value is not TomlTable table)
        {
            reader.Issue(reader.LineOf("prompts"), "prompts must be a table: [prompts] with lines like Correction = \"Ctrl+Alt+K\"", isError: true);
            return prompts;
        }

        var line = LinesIn(reader, "[prompts]");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, raw) in table)
        {
            if (!names.Add(name.Trim()))
            {
                reader.Issue(line(name), $"[prompts] '{name}' is listed twice (prompt names are not case-sensitive)", isError: true);
                continue;
            }

            if (raw is not string text)
            {
                reader.Issue(line(name), $"[prompts] '{name}' must be a hotkey in quotes, e.g. \"Ctrl+Alt+K\"", isError: true);
                continue;
            }

            if (text.Trim().Length == 0) continue; // "" = no hotkey, as an empty hotkey in prompts.toml used to mean

            var hotkey = ReadHotkey(table, reader, name, string.Empty, layout, mods, line, $"[prompts] '{name}'");
            prompts.Add(new PromptKeybind(name.Trim(), hotkey.Trim(), line(name)));
        }

        return prompts;
    }

    /// <summary>[mods]: name = "Ctrl+Alt". Names are letters, digits and _, not case-sensitive; values are modifiers only.</summary>
    private static Dictionary<string, HotkeyModifiers> ReadMods(TomlTable root, TomlReader reader)
    {
        var mods = new Dictionary<string, HotkeyModifiers>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetValue("mods", out var value)) return mods;
        if (value is not TomlTable table)
        {
            reader.Issue(reader.LineOf("mods"), "mods must be a table: [mods] with lines like main = \"Ctrl\"", isError: true);
            return mods;
        }

        var line = LinesIn(reader, "[mods]");
        foreach (var (name, raw) in table)
        {
            if (!HotkeyMods.IsValidName(name))
            {
                reader.Issue(line(name), $"[mods] name '{name}' is not allowed: use letters, digits and _ (e.g. main, card_2){HotkeyGesture.DocsHint}", isError: true);
                continue;
            }

            if (mods.ContainsKey(name))
            {
                reader.Issue(line(name), $"[mods] '{name}' is defined twice (names are not case-sensitive)", isError: true);
                continue;
            }

            if (raw is not string text)
            {
                reader.Issue(line(name), $"[mods] {name} must be text such as \"Ctrl+Alt\"", isError: true);
                continue;
            }

            if (!HotkeyMods.TryParseValue(text, out var modifiers, out var error))
            {
                reader.Issue(line(name), $"[mods] {name} = {error}.{HotkeyGesture.DocsHint}", isError: true);
                continue;
            }

            mods[name] = modifiers;
        }

        return mods;
    }

    /// <summary>
    /// [submaps.name]: open (hotkey), label (pill text, the name when missing), timeout (ms) and keys (key = target). Prompt
    /// targets are checked against prompts.toml later (<see cref="ConfigValidator"/>); "/command" targets are checked here.
    /// </summary>
    private static List<SubmapDefinition> ReadSubmaps(TomlTable root, TomlReader reader, IKeyLayout? layout, IReadOnlyDictionary<string, HotkeyModifiers> mods)
    {
        var submaps = new List<SubmapDefinition>();
        if (!root.TryGetValue("submaps", out var value)) return submaps;
        if (value is not TomlTable all)
        {
            reader.Issue(reader.LineOf("submaps"), "submaps must contain tables: [submaps.name] with open, label, timeout and keys", isError: true);
            return submaps;
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, raw) in all)
        {
            var block = reader.Block($"[submaps.{name}]") ?? reader.Block($"[submaps.\"{name}\"]");
            int? Line(string key) => block is { } b ? reader.LineOf(key, b.Start, b.End) ?? b.Start : reader.LineOf(name);
            void Error(int? line, string message) => reader.Issue(line, $"[submaps.{name}] {message}", isError: true);

            if (raw is not TomlTable table)
            {
                reader.Issue(reader.LineOf(name), $"submaps.{name} must be a table ([submaps.{name}])", isError: true);
                continue;
            }

            if (!IsValidSubmapName(name))
            {
                Error(block?.Start ?? reader.LineOf(name), $"name '{name}' is not allowed: use letters, digits, _ and -.{HotkeyGesture.DocsHint}");
                continue;
            }

            if (!names.Add(name))
            {
                Error(block?.Start, $"name '{name}' is used twice (names are not case-sensitive)");
                continue;
            }

            foreach (var key in table.Keys.Where(k => !SubmapFields.Contains(k)))
            {
                reader.Issue(Line(key), $"[submaps.{name}] unknown setting '{key}'", isError: false);
            }

            var errorsBefore = reader.ErrorCount;
            var open = string.Empty;
            if (!table.ContainsKey("open"))
            {
                Error(block?.Start, "open is missing: the hotkey that opens the submap, e.g. open = \"Ctrl+Alt+T\"");
            }
            else
            {
                open = ReadHotkey(table, reader, "open", string.Empty, layout, mods, Line, $"[submaps.{name}] open");
            }

            var label = reader.Choice(table, "label", name, null, Line).Trim();
            if (label.Length == 0)
            {
                Error(Line("label"), "label must not be empty: it is the text the pill shows while the submap is open (leave it out to show the name)");
            }

            var timeout = reader.Int(table, "timeout", SubmapDefinition.DefaultTimeoutMs, Line);
            if (timeout is < SubmapDefinition.MinTimeoutMs or > SubmapDefinition.MaxTimeoutMs)
            {
                Error(Line("timeout"), $"timeout must be between {SubmapDefinition.MinTimeoutMs} and {SubmapDefinition.MaxTimeoutMs} (milliseconds)");
            }

            var keys = ReadSubmapKeys(name, table, reader, layout, block, Line);
            if (reader.ErrorCount == errorsBefore) submaps.Add(new SubmapDefinition(name, open, label, timeout, keys, block?.Start));
        }

        return submaps;
    }

    private static List<SubmapKey> ReadSubmapKeys(
        string name, TomlTable table, TomlReader reader, IKeyLayout? layout, (int Start, int End)? block, Func<string, int?> line)
    {
        var keys = new List<SubmapKey>();
        void Error(int? at, string message) => reader.Issue(at, $"[submaps.{name}] {message}", isError: true);

        if (!table.TryGetValue("keys", out var value) || value is not TomlTable entries || entries.Count == 0)
        {
            Error(table.ContainsKey("keys") ? line("keys") : block?.Start,
                "keys must list at least one key and what it runs, e.g. keys = { P = \"Professioneller\", U = \"/undo\" }");
            return keys;
        }

        // keys = { … } stands on one line; a [submaps.name.keys] table has one line per key.
        var keysBlock = reader.Block($"[submaps.{name}.keys]");
        int? KeyLine(string key) => keysBlock is { } kb
            ? reader.LineOf(key, kb.Start, kb.End) ?? reader.LineOf($"\"{key}\"", kb.Start, kb.End) ?? kb.Start
            : line("keys");

        var seen = new Dictionary<int, string>();
        foreach (var (key, raw) in entries)
        {
            if (!HotkeyGesture.TryParseSubmapKey(key, layout, out var vk, out var error))
            {
                Error(KeyLine(key), error);
                continue;
            }

            if (seen.TryGetValue(vk, out var other))
            {
                Error(KeyLine(key), $"key '{key}' is listed twice (also as '{other}')");
                continue;
            }

            seen[vk] = key;
            var target = (raw as string)?.Trim() ?? string.Empty;
            if (target.Length == 0 || target == "/")
            {
                Error(KeyLine(key), $"key '{key}' must name a prompt from prompts.toml or a command such as \"/undo\"");
                continue;
            }

            if (target.StartsWith('/') && !CommandNames.IsKnown(target[1..]))
            {
                Error(KeyLine(key), $"key '{key}': unknown command '{target}' (commands: {string.Join(", ", CommandNames.All.Select(c => "/" + c))})");
                continue;
            }

            keys.Add(new SubmapKey(key, vk, target, KeyLine(key)));
        }

        return keys;
    }

    private static bool IsValidSubmapName(string name) => name.Length > 0 && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');
}
