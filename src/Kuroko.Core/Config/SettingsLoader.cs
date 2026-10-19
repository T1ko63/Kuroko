using System.Globalization;
using System.Net;
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
    private static readonly string[] Reasonings = ["off", "default", "low", "medium", "high", "xhigh", "max"];
    private static readonly string[] ServiceTiers = ["standard", "fast", "ultrafast", "flex"];

    private static readonly HashSet<string> TopLevelKeys = new(StringComparer.Ordinal)
    {
        "marker_start", "marker_end", "theme", "autostart", "overlay_position", "language",
        "idle_trim_seconds", "default_provider", "fallback_provider", "providers", "accent", "undo_history",
        "appearance", "result_position", "result_font_size",
        "result_width", "result_max_height", "result_min_height", "result_fixed_position", "result_screen_margin",
        "overlay_fixed_position", "overlay_screen_margin", "overlay_width", "overlay_min_height", "overlay_max_height", "overlay_font_size",
        "pill_position", "pill_fixed_position", "pill_screen_margin", "pill_min_width", "pill_max_width", "pill_min_height", "pill_font_size", "pill_duration",
        "pill_error_duration",
    };

    /// <summary>Keys that moved to keybinds.toml. Still recognized, so they get a clear warning instead of "unknown setting".</summary>
    public static IReadOnlyList<string> MovedToKeybinds { get; } = ["overlay_hotkey", "undo_hotkey", "result_copy_hotkey", "mods", "submaps"];

    private static readonly HashSet<string> ProviderKeys = new(StringComparer.Ordinal)
    {
        "type", "base_url", "model", "api_key", "api_key_env", "timeout_seconds", "reasoning", "max_output_tokens", "fallback_provider",
        "service_tier",
    };

    /// <param name="getEnv">Reads an environment variable (api_key_env).</param>
    /// <param name="keyStore">Keys stored outside the file (Windows Credential Manager); asked only when neither api_key nor api_key_env gave a key.</param>
    /// <param name="fontExists">Whether a font family is installed, for [appearance] font; null skips that check.</param>
    public static SettingsLoadResult Parse(
        string toml, Func<string, string?> getEnv, IKeyStore? keyStore = null, Func<string, bool>? fontExists = null)
    {
        var issues = new List<ConfigIssue>();

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
            if (MovedToKeybinds.Contains(key))
            {
                // Read once, when keybinds.toml was created from them (KeybindsMigration); ignored since then.
                var what = key is "mods" or "submaps" ? $"[{key}]" : key;
                reader.Issue(reader.LineOf(key), $"{what} in settings.toml is no longer read; keybinds live in keybinds.toml", isError: false, show: true);
                continue;
            }

            reader.Issue(reader.LineOf(key), $"unknown setting '{key}'", isError: false);
        }

        var markerStart = reader.Choice(root, "marker_start", "<<", null);
        var markerEnd = reader.Choice(root, "marker_end", ">>", null);
        if (markerStart.Length == 0 || markerEnd.Length == 0)
        {
            reader.Issue(reader.LineOf("marker_start"), "marker_start and marker_end must not be empty", isError: true);
        }

        var undoHistory = reader.Int(root, "undo_history", AppSettings.DefaultUndoHistory);
        if (undoHistory is < 0 or > AppSettings.MaxUndoHistory)
        {
            reader.Issue(reader.LineOf("undo_history"), $"undo_history must be between 0 and {AppSettings.MaxUndoHistory} (0 = off)", isError: true);
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

        // 0 is allowed on its own: the error then stays until it is clicked or Esc is pressed.
        var pillErrorDuration = root.TryGetValue("pill_error_duration", out var errorDuration) && errorDuration is 0L or 0.0
            ? 0
            : ReadNumber(root, reader, "pill_error_duration", AppSettings.DefaultPillErrorDuration,
                AppSettings.MinPillErrorDuration, AppSettings.MaxPillErrorDuration, " (or 0: the error stays until you click it or press Esc)");

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
            markerStart, markerEnd, theme, autostart, position, language, idleTrim,
            defaultProvider.Length == 0 ? null : defaultProvider, providers, accent.ToLowerInvariant(), undoHistory,
            resultPosition, resultFontSize, resultWidth, resultMaxHeight, resultMinHeight, resultFixedPosition, resultScreenMargin,
            overlayFixedPosition, overlayScreenMargin, overlayWidth, overlayMinHeight, overlayMaxHeight, overlayFontSize)
        {
            Appearance = appearance,
            PillPosition = pillPosition,
            PillFixedPosition = pillFixedPosition,
            PillScreenMargin = pillScreenMargin,
            PillMinWidth = pillMinWidth,
            PillMaxWidth = pillMaxWidth,
            PillMinHeight = pillMinHeight,
            PillFontSize = pillFontSize,
            PillDuration = pillDuration,
            PillErrorDuration = pillErrorDuration,
        };
        return new SettingsLoadResult(issues.Any(i => i.IsError) ? null : settings, issues);
    }

    /// <summary>"none", "system" (case-insensitive) or #RGB / #RRGGBB / #AARRGGBB.</summary>
    public static bool IsValidAccent(string accent)
        => accent.Equals("none", StringComparison.OrdinalIgnoreCase)
           || accent.Equals("system", StringComparison.OrdinalIgnoreCase)
           || ColorParser.TryParseAccentHex(accent, out _);

    /// <summary>A whole number or a decimal (15 or 14.5) within [min, max]; anything else is an error and the default stays.</summary>
    /// <param name="hint">Added to the error message, e.g. a special value outside the range.</param>
    private static double ReadNumber(TomlTable root, TomlReader reader, string key, double fallback, double min, double max, string hint = "")
    {
        if (!root.TryGetValue(key, out var value)) return fallback;

        double? number = value switch { long whole => whole, double fraction => fraction, _ => null };
        if (number is { } n && n >= min && n <= max) return n;

        // Numbers as TOML writes them ("0.5"), not in the user's culture ("0,5").
        reader.Issue(reader.LineOf(key), string.Create(CultureInfo.InvariantCulture, $"{key} must be a number between {min} and {max}{hint}"), isError: true);
        return fallback;
    }

    /// <summary>A key that belongs at the top of settings.toml (above the first [table]), e.g. result_font_size.</summary>
    public static bool IsGeneralSetting(string key) => key != "providers" && key != "appearance" && TopLevelKeys.Contains(key);

    /// <summary>Hint added to "unknown setting" when a general setting was written below a [table] header, where TOML puts it into that table.</summary>
    public static string MisplacedHint(string key)
        => IsGeneralSetting(key) ? " (this is a general setting: it must stand above the first [table] line of the file)" : string.Empty;

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

        // service_tier: how fast OpenAI answers (and what it costs). "standard" sends nothing.
        var serviceTier = reader.Choice(table, "service_tier", "standard", ServiceTiers, Line);
        if (table.ContainsKey("service_tier") && type != ProviderType.OpenAi)
        {
            reader.Issue(Line("service_tier"), $"[providers.{name}] service_tier is only read for type = \"openai\"; it is ignored here", isError: false);
            serviceTier = "standard";
        }

        // flex answers more slowly: without its own timeout_seconds it gets a longer default.
        var timeout = reader.Int(table, "timeout_seconds", serviceTier == "flex" ? FlexTimeoutSeconds : DefaultTimeoutSeconds, Line);
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
            keySource, keyEnvName.Length > 0 ? keyEnvName : null, fallback, serviceTier);
    }

    /// <summary>timeout_seconds when a provider does not set it.</summary>
    public const int DefaultTimeoutSeconds = 30;

    /// <summary>timeout_seconds for service_tier = "flex" when a provider does not set it: flex answers more slowly.</summary>
    public const int FlexTimeoutSeconds = 120;

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
