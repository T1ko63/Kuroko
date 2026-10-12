using System.Net;
using Kuroko.Core.Hotkeys;
using Kuroko.Core.Providers;
using Kuroko.Core.Secrets;
using Tomlyn;
using Tomlyn.Model;

namespace Kuroko.Core.Config;

/// <summary>Reads and validates settings.toml. Never throws for bad content: problems come back as <see cref="ConfigIssue"/>s.</summary>
public static class SettingsLoader
{
    public const string FileName = "settings.toml";

    private static readonly string[] Themes = ["system", "light", "dark"];
    private static readonly string[] Positions = ["caret", "mouse", "fixed", "center"]; // "center" is the old name of "fixed"
    private static readonly string[] ResultPositions = ["fixed", "follow", "caret", "mouse", "center"]; // "center" is the old name of "fixed"
    private static readonly string[] PillPositions = ["follow", "caret", "mouse", "fixed", "card"];

    /// <summary>The places the result card can have (result_fixed_position).</summary>
    public static readonly string[] ResultSpots =
    [
        "top-third", "center", "bottom-third", "top-left", "bottom-left", "top-right", "bottom-right", "left", "right", "top", "bottom",
    ];
    private static readonly string[] Languages = ["auto", "de", "en"];
    private static readonly string[] Reasonings = ["off", "default", "low", "medium", "high"];

    private static readonly HashSet<string> TopLevelKeys = new(StringComparer.Ordinal)
    {
        "overlay_hotkey", "marker_start", "marker_end", "theme", "autostart", "overlay_position", "language",
        "idle_trim_seconds", "default_provider", "fallback_provider", "providers", "accent", "undo_hotkey", "undo_history",
        "appearance", "mods", "submaps", "result_copy_hotkey", "result_position", "result_font_size",
        "result_width", "result_max_height", "result_min_height", "result_fixed_position", "result_screen_margin",
        "overlay_fixed_position", "overlay_screen_margin", "overlay_width", "overlay_min_height", "overlay_max_height", "overlay_font_size",
        "pill_position", "pill_fixed_position", "pill_screen_margin", "pill_min_width", "pill_max_width", "pill_min_height", "pill_font_size", "pill_duration",
    };

    private static readonly HashSet<string> ProviderKeys = new(StringComparer.Ordinal)
    {
        "type", "base_url", "model", "api_key", "api_key_env", "timeout_seconds", "reasoning", "max_output_tokens", "fallback_provider",
    };

    /// <param name="getEnv">Reads an environment variable (api_key_env).</param>
    /// <param name="keyStore">Keys stored outside the file (Windows Credential Manager); asked only when neither api_key nor api_key_env gave a key.</param>
    /// <param name="fontExists">Whether a font family is installed, for [appearance] font; null skips that check.</param>
    /// <param name="keyLayout">Resolves hotkey keys written as a character; null uses <see cref="KeyLayouts.Current"/>.</param>
    public static SettingsLoadResult Parse(
        string toml, Func<string, string?> getEnv, IKeyStore? keyStore = null, Func<string, bool>? fontExists = null, IKeyLayout? keyLayout = null)
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
            return new SettingsLoadResult(null, issues);
        }

        var reader = new TomlReader(FileName, toml, issues);
        reader.WarnAboutDuplicateKeys();

        foreach (var key in root.Keys.Where(k => !TopLevelKeys.Contains(k)))
        {
            reader.Issue(reader.LineOf(key), $"unknown setting '{key}'", isError: false);
        }

        var mods = ReadMods(root, reader);
        var overlayHotkey = ReadHotkey(root, reader, "overlay_hotkey", AppSettings.DefaultOverlayHotkey, layout, mods);

        var markerStart = reader.Choice(root, "marker_start", "<<", null);
        var markerEnd = reader.Choice(root, "marker_end", ">>", null);
        if (markerStart.Length == 0 || markerEnd.Length == 0)
        {
            reader.Issue(reader.LineOf("marker_start"), "marker_start and marker_end must not be empty", isError: true);
        }

        var undoHotkey = ReadHotkey(root, reader, "undo_hotkey", AppSettings.DefaultUndoHotkey, layout, mods);
        var undoHistory = reader.Int(root, "undo_history", AppSettings.DefaultUndoHistory);
        if (undoHistory is < 0 or > AppSettings.MaxUndoHistory)
        {
            reader.Issue(reader.LineOf("undo_history"), $"undo_history must be between 0 and {AppSettings.MaxUndoHistory} (0 = off)", isError: true);
        }

        var resultCopyHotkey = ReadHotkey(root, reader, "result_copy_hotkey", AppSettings.DefaultResultCopyHotkey, layout, mods);
        if (HotkeyGesture.TryParse(resultCopyHotkey, layout, out var copyGesture, out _) && CollidesWithCapture(copyGesture))
        {
            // The app itself sends exactly these keys to read the selection; RegisterHotKey would also catch those.
            reader.Issue(reader.LineOf("result_copy_hotkey"),
                $"result_copy_hotkey: {copyGesture.ToString(layout)} is used by the app to read the selection (Ctrl+C, Ctrl+A, Ctrl+V); choose another hotkey", isError: true);
        }

        var resultPosition = reader.Choice(root, "result_position", AppSettings.DefaultResultPosition, ResultPositions);
        if (resultPosition == "center") resultPosition = "fixed";
        var resultFixedPosition = reader.Choice(root, "result_fixed_position", AppSettings.DefaultResultFixedPosition, ResultSpots);
        var resultScreenMargin = ReadNumber(root, reader, "result_screen_margin", AppSettings.DefaultResultScreenMargin, AppSettings.MinResultScreenMargin, AppSettings.MaxResultScreenMargin);
        var resultMinHeight = ReadNumber(root, reader, "result_min_height", AppSettings.DefaultResultMinHeight, AppSettings.MinResultMinHeight, AppSettings.MaxResultMinHeight);
        var resultFontSize = ReadNumber(root, reader, "result_font_size", AppSettings.DefaultResultFontSize, AppSettings.MinResultFontSize, AppSettings.MaxResultFontSize);
        var resultWidth = ReadNumber(root, reader, "result_width", AppSettings.DefaultResultWidth, AppSettings.MinResultWidth, AppSettings.MaxResultWidth);
        var resultMaxHeight = ReadNumber(root, reader, "result_max_height", AppSettings.DefaultResultMaxHeight, AppSettings.MinResultMaxHeight, AppSettings.MaxResultMaxHeight);
        if (resultMinHeight > resultMaxHeight)
        {
            reader.Issue(reader.LineOf("result_min_height"), "result_min_height must not be larger than result_max_height", isError: true);
        }

        var pillPosition = reader.Choice(root, "pill_position", AppSettings.DefaultPillPosition, PillPositions);
        var pillFixedPosition = reader.Choice(root, "pill_fixed_position", AppSettings.DefaultPillFixedPosition, ResultSpots);
        var pillScreenMargin = ReadNumber(root, reader, "pill_screen_margin", AppSettings.DefaultPillScreenMargin, AppSettings.MinResultScreenMargin, AppSettings.MaxResultScreenMargin);
        var pillMinWidth = ReadNumber(root, reader, "pill_min_width", AppSettings.DefaultPillMinWidth, AppSettings.MinPillWidth, AppSettings.MaxPillWidth);
        var pillMaxWidth = ReadNumber(root, reader, "pill_max_width", AppSettings.DefaultPillMaxWidth, AppSettings.MinPillWidth, AppSettings.MaxPillWidth);
        if (pillMinWidth > pillMaxWidth)
        {
            reader.Issue(reader.LineOf("pill_min_width"), "pill_min_width must not be larger than pill_max_width", isError: true);
        }

        var pillMinHeight = ReadNumber(root, reader, "pill_min_height", AppSettings.DefaultPillMinHeight, 0, AppSettings.MaxPillMinHeight);
        var pillFontSize = ReadNumber(root, reader, "pill_font_size", AppSettings.DefaultPillFontSize, AppSettings.MinResultFontSize, AppSettings.MaxResultFontSize);
        var pillDuration = ReadNumber(root, reader, "pill_duration", AppSettings.DefaultPillDuration, AppSettings.MinPillDuration, AppSettings.MaxPillDuration);

        var theme = reader.Choice(root, "theme", "system", Themes);
        var accent = reader.Choice(root, "accent", "none", null).Trim();
        if (!IsValidAccent(accent))
        {
            reader.Issue(reader.LineOf("accent"), "accent must be \"none\", \"system\" or a hex colour like \"#3B82F6\"", isError: true);
        }

        var position = reader.Choice(root, "overlay_position", "caret", Positions);
        if (position == "center") position = "fixed";
        var overlayFixedPosition = reader.Choice(root, "overlay_fixed_position", AppSettings.DefaultOverlayFixedPosition, ResultSpots);
        var overlayScreenMargin = ReadNumber(root, reader, "overlay_screen_margin", AppSettings.DefaultOverlayScreenMargin, AppSettings.MinResultScreenMargin, AppSettings.MaxResultScreenMargin);
        var overlayWidth = ReadNumber(root, reader, "overlay_width", AppSettings.DefaultOverlayWidth, AppSettings.MinOverlayWidth, AppSettings.MaxOverlayWidth);
        var overlayMinHeight = ReadNumber(root, reader, "overlay_min_height", AppSettings.DefaultOverlayMinHeight, 0, AppSettings.MaxOverlayHeight);
        var overlayMaxHeight = ReadNumber(root, reader, "overlay_max_height", AppSettings.DefaultOverlayMaxHeight, AppSettings.MinOverlayMaxHeight, AppSettings.MaxOverlayHeight);
        var overlayFontSize = ReadNumber(root, reader, "overlay_font_size", AppSettings.DefaultOverlayFontSize, AppSettings.MinResultFontSize, AppSettings.MaxResultFontSize);
        if (overlayMinHeight > overlayMaxHeight)
        {
            reader.Issue(reader.LineOf("overlay_min_height"), "overlay_min_height must not be larger than overlay_max_height", isError: true);
        }
        var language = reader.Choice(root, "language", "auto", Languages);
        var autostart = reader.Bool(root, "autostart", false);
        var idleTrim = reader.Int(root, "idle_trim_seconds", AppSettings.DefaultIdleTrimSeconds);
        if (idleTrim < 0)
        {
            reader.Issue(reader.LineOf("idle_trim_seconds"), "idle_trim_seconds must not be negative (0 = never)", isError: true);
        }

        var appearance = AppearanceLoader.Read(root, reader, fontExists);
        var submaps = ReadSubmaps(root, reader, layout, mods);

        var providers = new Dictionary<string, ProviderSettings>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetValue("providers", out var providersValue))
        {
            if (providersValue is TomlTable providerTable)
            {
                foreach (var (name, value) in providerTable)
                {
                    if (value is not TomlTable table)
                    {
                        reader.Issue(reader.LineOf(name), $"providers.{name} must be a table ([providers.{name}])", isError: true);
                        continue;
                    }

                    if (providers.ContainsKey(name))
                    {
                        reader.Issue(reader.LineOf(name), $"provider name '{name}' is used twice (names are not case-sensitive)", isError: true);
                        continue;
                    }

                    if (ReadProvider(name, table, reader, getEnv, keyStore) is { } provider) providers[name] = provider;
                }
            }
            else
            {
                reader.Issue(reader.LineOf("providers"), "providers must contain tables ([providers.name])", isError: true);
            }
        }

        ResolveFallbacks(root, providers, reader);

        var defaultProvider = reader.Choice(root, "default_provider", string.Empty, null);
        if (defaultProvider.Length == 0)
        {
            defaultProvider = providers.Keys.FirstOrDefault() ?? string.Empty;
        }
        else if (!providers.ContainsKey(defaultProvider))
        {
            reader.Issue(reader.LineOf("default_provider"), $"default_provider '{defaultProvider}' is not defined under [providers.*]", isError: true);
        }

        if (providers.Count == 0)
        {
            reader.Issue(null, "no provider configured: add a [providers.name] section", isError: false);
        }

        var settings = new AppSettings(
            overlayHotkey, markerStart, markerEnd, theme, autostart, position, language, idleTrim,
            defaultProvider.Length == 0 ? null : defaultProvider, providers, accent.ToLowerInvariant(), undoHotkey, undoHistory,
            resultCopyHotkey, resultPosition, resultFontSize, resultWidth, resultMaxHeight, resultMinHeight, resultFixedPosition, resultScreenMargin,
            overlayFixedPosition, overlayScreenMargin, overlayWidth, overlayMinHeight, overlayMaxHeight, overlayFontSize)
        {
            Appearance = appearance,
            Mods = mods,
            Submaps = submaps,
            PillPosition = pillPosition,
            PillFixedPosition = pillFixedPosition,
            PillScreenMargin = pillScreenMargin,
            PillMinWidth = pillMinWidth,
            PillMaxWidth = pillMaxWidth,
            PillMinHeight = pillMinHeight,
            PillFontSize = pillFontSize,
            PillDuration = pillDuration,
        };
        return new SettingsLoadResult(issues.Any(i => i.IsError) ? null : settings, issues);
    }

    /// <summary>"none", "system" (case-insensitive) or #RGB / #RRGGBB / #AARRGGBB.</summary>
    public static bool IsValidAccent(string accent)
        => accent.Equals("none", StringComparison.OrdinalIgnoreCase)
           || accent.Equals("system", StringComparison.OrdinalIgnoreCase)
           || ColorParser.TryParseAccentHex(accent, out _);

    /// <summary>A whole number or a decimal (15 or 14.5) within [min, max]; anything else is an error and the default stays.</summary>
    private static double ReadNumber(TomlTable root, TomlReader reader, string key, double fallback, double min, double max)
    {
        if (!root.TryGetValue(key, out var value)) return fallback;

        double? number = value switch { long whole => whole, double fraction => fraction, _ => null };
        if (number is { } n && n >= min && n <= max) return n;

        reader.Issue(reader.LineOf(key), $"{key} must be a number between {min} and {max}", isError: true);
        return fallback;
    }

    /// <summary>A key that belongs at the top of settings.toml (above the first [table]), e.g. result_font_size.</summary>
    public static bool IsGeneralSetting(string key) => key != "providers" && key != "appearance" && TopLevelKeys.Contains(key);

    /// <summary>Hint added to "unknown setting" when a general setting was written below a [table] header, where TOML puts it into that table.</summary>
    public static string MisplacedHint(string key)
        => IsGeneralSetting(key) ? " (this is a general setting: it must stand above the first [table] line of the file)" : string.Empty;

    /// <summary>Plain Ctrl+A / Ctrl+C / Ctrl+V: the keys the app simulates itself while reading or replacing text.</summary>
    public static bool CollidesWithCapture(HotkeyGesture gesture)
        => gesture.Modifiers == HotkeyModifiers.Ctrl && gesture.VirtualKey is 'A' or 'C' or 'V';

    /// <summary>The hotkey with its [mods] variables filled in, so everything that uses it later sees plain modifiers.</summary>
    /// <param name="line">Finds the line of a key; null looks it up in the whole file.</param>
    /// <param name="label">Names the setting in messages; <paramref name="key"/> when null.</param>
    private static string ReadHotkey(
        TomlTable root, TomlReader reader, string key, string fallback, IKeyLayout? layout, IReadOnlyDictionary<string, HotkeyModifiers> mods,
        Func<string, int?>? line = null, string? label = null)
    {
        line ??= k => reader.LineOf(k);
        label ??= key;
        var text = reader.Choice(root, key, fallback, null, line);
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

        var block = reader.Block("[mods]");
        int? Line(string key) => block is { } b
            ? reader.LineOf(key, b.Start, b.End) ?? reader.LineOf($"\"{key}\"", b.Start, b.End) ?? b.Start // a quoted name: "my-mod" = ...
            : reader.LineOf(key);

        foreach (var (name, raw) in table)
        {
            if (!HotkeyMods.IsValidName(name))
            {
                reader.Issue(Line(name), $"[mods] name '{name}' is not allowed: use letters, digits and _ (e.g. main, card_2){HotkeyGesture.DocsHint}", isError: true);
                continue;
            }

            if (mods.ContainsKey(name))
            {
                reader.Issue(Line(name), $"[mods] '{name}' is defined twice (names are not case-sensitive)", isError: true);
                continue;
            }

            if (raw is not string text)
            {
                reader.Issue(Line(name), $"[mods] {name} must be text such as \"Ctrl+Alt\"", isError: true);
                continue;
            }

            if (!HotkeyMods.TryParseValue(text, out var modifiers, out var error))
            {
                reader.Issue(Line(name), $"[mods] {name} = {error}.{HotkeyGesture.DocsHint}", isError: true);
                continue;
            }

            mods[name] = modifiers;
        }

        return mods;
    }

    private static readonly HashSet<string> SubmapFields = new(StringComparer.Ordinal) { "open", "label", "timeout", "keys" };

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
                reader.Issue(Line(key), $"[submaps.{name}] unknown setting '{key}'{MisplacedHint(key)}", isError: false);
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

    private static ProviderSettings? ReadProvider(string name, TomlTable table, TomlReader reader, Func<string, string?> getEnv, IKeyStore? keyStore)
    {
        // Lines are looked up inside this provider's own block, so an error in the third provider points at the third provider.
        var block = reader.Block($"[providers.{name}]");
        int? Line(string key) => block is { } b ? reader.LineOf(key, b.Start, b.End) ?? b.Start : reader.LineOf(name);
        void Error(string key, string message) => reader.Issue(Line(key), $"[providers.{name}] {message}", isError: true);

        foreach (var key in table.Keys.Where(k => !ProviderKeys.Contains(k)))
        {
            reader.Issue(Line(key), $"[providers.{name}] unknown setting '{key}'{MisplacedHint(key)}", isError: false);
        }

        var typeText = reader.Choice(table, "type", string.Empty, null, Line);
        ProviderType? type = typeText switch
        {
            "openai" => ProviderType.OpenAi,
            "gemini" => ProviderType.Gemini,
            "anthropic" => ProviderType.Anthropic,
            "openai-compatible" => ProviderType.OpenAiCompatible,
            _ => null,
        };
        if (type is null)
        {
            Error("type", typeText.Length == 0
                ? "type is missing (openai, gemini, anthropic or openai-compatible)"
                : $"unknown type '{typeText}' (use openai, gemini, anthropic or openai-compatible)");
            return null;
        }

        var model = reader.Choice(table, "model", string.Empty, null, Line);
        if (model.Length == 0)
        {
            Error("model", "model is missing");
            return null;
        }

        var baseUrl = reader.Choice(table, "base_url", DefaultBaseUrl(type.Value), null, Line).TrimEnd('/');
        var failed = false;
        string? apiKey = table.TryGetValue("api_key", out var keyValue) ? keyValue as string : null;
        var keyHasWrongType = keyValue is not null and not string;
        if (keyHasWrongType)
        {
            // Stays a warning as before (the provider just has no key); only the message now names the real problem.
            reader.Issue(Line("api_key"), $"[providers.{name}] api_key must be text (in quotes)", isError: false);
        }

        // Order: api_key in the file, then the environment variable, then the key store (Windows Credential Manager).
        var keySource = string.IsNullOrWhiteSpace(apiKey) ? ApiKeySource.None : ApiKeySource.File;
        var keyEnvName = reader.Choice(table, "api_key_env", string.Empty, null, Line);
        if (keySource == ApiKeySource.None && keyEnvName.Length > 0)
        {
            apiKey = getEnv(keyEnvName);
            if (!string.IsNullOrWhiteSpace(apiKey)) keySource = ApiKeySource.Environment;
        }

        if (keySource == ApiKeySource.None && keyStore is not null)
        {
            apiKey = keyStore.Read(name);
            if (!string.IsNullOrWhiteSpace(apiKey)) keySource = ApiKeySource.CredentialManager;
        }

        apiKey = keySource == ApiKeySource.None ? null : apiKey!.Trim();
        var storeHint = keyStore is null ? string.Empty : $" and no key is stored in the Windows Credential Manager ({KeyStoreNames.Target(name)})";
        if (apiKey is null && keyEnvName.Length > 0)
        {
            reader.Issue(Line("api_key_env"), $"[providers.{name}] environment variable {keyEnvName} is not set{storeHint}", isError: false);
        }
        else if (apiKey is null && type != ProviderType.OpenAiCompatible && !keyHasWrongType)
        {
            var what = keyStore is null ? "no api_key or api_key_env set" : "no api_key or api_key_env set and no key stored";
            reader.Issue(Line("api_key"), $"[providers.{name}] {what}", isError: false);
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            Error("base_url", $"base_url '{baseUrl}' is not a valid http(s) address");
            failed = true;
        }
        else if (uri.Scheme == "http" && apiKey is not null && !IsLocalAddress(uri))
        {
            // An API key must never travel unencrypted to a remote host.
            Error("base_url", "base_url uses http:// for a remote host; use https:// when an API key is configured");
            failed = true;
        }

        var timeout = reader.Int(table, "timeout_seconds", 30, Line);
        if (timeout is < 1 or > 600)
        {
            Error("timeout_seconds", "timeout_seconds must be between 1 and 600");
            failed = true;
        }

        var reasoning = reader.Choice(table, "reasoning", "off", Reasonings, Line);
        var maxTokens = reader.Int(table, "max_output_tokens", 0, Line);
        if (maxTokens < 0)
        {
            Error("max_output_tokens", "max_output_tokens must not be negative (0 = automatic)");
            failed = true;
        }

        // Missing: the global fallback_provider applies. Empty: this provider has no fallback. Checked against the other
        // providers in ResolveFallbacks, once all of them are read.
        string? fallback = table.ContainsKey("fallback_provider") ? reader.Choice(table, "fallback_provider", string.Empty, null, Line).Trim() : null;

        return failed ? null : new ProviderSettings(name, type.Value, baseUrl, model, apiKey, TimeSpan.FromSeconds(timeout), reasoning, maxTokens,
            keySource, keyEnvName.Length > 0 ? keyEnvName : null, fallback);
    }

    /// <summary>
    /// Sets the fallback provider each provider really uses: its own fallback_provider, otherwise the global one. The global
    /// one is not used by local providers (a text meant to stay on the PC must not go out just because the local server is
    /// down) nor by the global fallback provider itself; a local provider only falls back when its own block says so.
    /// Only one step is ever taken (see <see cref="ProviderRegistry.GetFallback"/>), so A -> B -> A needs no cycle check.
    /// </summary>
    private static void ResolveFallbacks(TomlTable root, Dictionary<string, ProviderSettings> providers, TomlReader reader)
    {
        var known = providers.Count == 0 ? "none defined" : string.Join(", ", providers.Keys);

        var global = reader.Choice(root, "fallback_provider", string.Empty, null).Trim();
        if (global.Length > 0)
        {
            if (providers.TryGetValue(global, out var target))
            {
                global = target.Name; // the spelling of the [providers.*] header
            }
            else
            {
                reader.Issue(reader.LineOf("fallback_provider"), $"fallback_provider '{global}' is not defined under [providers.*] (defined: {known})", isError: true);
                global = string.Empty;
            }
        }

        foreach (var (name, provider) in providers.ToList())
        {
            string? effective;
            if (provider.FallbackProvider is { } own)
            {
                var block = reader.Block($"[providers.{name}]");
                var line = block is { } b ? reader.LineOf("fallback_provider", b.Start, b.End) ?? b.Start : reader.LineOf(name);
                if (own.Length == 0)
                {
                    effective = null;
                }
                else if (own.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    reader.Issue(line, $"[providers.{name}] fallback_provider must name another provider, not '{own}' itself", isError: true);
                    continue;
                }
                else if (providers.TryGetValue(own, out var target))
                {
                    effective = target.Name;
                }
                else
                {
                    reader.Issue(line, $"[providers.{name}] fallback_provider '{own}' is not defined under [providers.*] (defined: {known})", isError: true);
                    continue;
                }
            }
            else
            {
                var usesGlobal = global.Length > 0 && !global.Equals(name, StringComparison.OrdinalIgnoreCase) && !IsLocalProvider(provider);
                effective = usesGlobal ? global : null;
            }

            providers[name] = provider with { FallbackProvider = effective };
        }
    }

    /// <summary>A provider whose server runs on this PC or in the local network (Ollama, LM Studio): the text stays local.</summary>
    public static bool IsLocalProvider(ProviderSettings provider)
        => Uri.TryCreate(provider.BaseUrl, UriKind.Absolute, out var uri) && IsLocalAddress(uri);

    public static string DefaultBaseUrl(ProviderType type) => type switch
    {
        ProviderType.OpenAi => "https://api.openai.com/v1",
        ProviderType.Gemini => "https://generativelanguage.googleapis.com/v1beta",
        ProviderType.Anthropic => "https://api.anthropic.com",
        _ => "http://localhost:11434/v1",
    };

    private static bool IsLocalAddress(Uri uri)
    {
        if (uri.IsLoopback || uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) || !uri.Host.Contains('.')) return true;
        if (!IPAddress.TryParse(uri.Host, out var ip)) return false;
        var bytes = ip.GetAddressBytes();
        return ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            && (bytes[0] == 10 || (bytes[0] == 192 && bytes[1] == 168) || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31));
    }
}
