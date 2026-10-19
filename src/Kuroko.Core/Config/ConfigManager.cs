using System.Text;
using Kuroko.Core.Prompts;
using Kuroko.Core.Secrets;

namespace Kuroko.Core.Config;

/// <summary>
/// The settings, prompts and keybinds that are active together. <see cref="Prompts"/> carry the hotkey keybinds.toml gives
/// them (<see cref="PromptDefinition.Hotkey"/>), so everything that lists or runs prompts sees one list.
/// </summary>
public sealed record ConfigSnapshot(AppSettings Settings, IReadOnlyList<PromptDefinition> Prompts, KeybindSettings Keybinds);

/// <summary>Outcome of (re)reading the config files.</summary>
public sealed record ReloadResult(
    ConfigSnapshot Snapshot, bool SettingsChanged, bool PromptsChanged, IReadOnlyList<ConfigIssue> Issues, bool KeybindsChanged = false)
{
    public bool HasErrors => Issues.Any(i => i.IsError);

    public bool Changed => SettingsChanged || PromptsChanged || KeybindsChanged;
}

/// <summary>
/// Owns the active configuration. Reading never throws and never makes things worse: a file with a syntax or
/// validation error is rejected as a whole and the last working version of it stays active, while the other
/// files can still be applied (as long as they fit together).
/// </summary>
public sealed class ConfigManager
{
    private readonly Func<string, string?> _getEnv;
    private readonly IKeyStore? _keyStore;
    private readonly Func<string, bool>? _fontExists;
    private readonly object _gate = new();
    private ConfigSnapshot _current = new(AppSettings.Fallback, BuiltInPrompts.All, KeybindSettings.Default);
    private IReadOnlyList<PromptDefinition> _prompts = BuiltInPrompts.All; // as read from prompts.toml, without hotkeys
    private string? _settingsText;
    private string? _promptsText;
    private string? _keybindsText;
    private IReadOnlyDictionary<string, int> _promptLines = new Dictionary<string, int>();

    /// <param name="fontExists">Whether a font family is installed, for [appearance] font; null skips that check.</param>
    public ConfigManager(string directory, Func<string, string?> getEnv, IKeyStore? keyStore = null, Func<string, bool>? fontExists = null)
    {
        Directory = directory;
        _getEnv = getEnv;
        _keyStore = keyStore;
        _fontExists = fontExists;
    }

    public string Directory { get; }

    public string SettingsPath => Path.Combine(Directory, SettingsLoader.FileName);

    public string PromptsPath => Path.Combine(Directory, PromptsLoader.FileName);

    public string KeybindsPath => Path.Combine(Directory, KeybindsLoader.FileName);

    public ConfigSnapshot Current => Volatile.Read(ref _current);

    /// <summary>
    /// First start: creates missing files with defaults, then reads all three. keybinds.toml is created first, from the
    /// hotkeys of an older settings.toml and prompts.toml when there are any (<see cref="KeybindsMigration"/>).
    /// </summary>
    public ReloadResult Load()
    {
        var issues = new List<ConfigIssue>();
        try
        {
            issues.AddRange(KeybindsMigration.EnsureExists(Directory));
            DefaultSettings.EnsureExists(SettingsPath, _getEnv);
            DefaultPrompts.EnsureExists(PromptsPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            issues.Add(new ConfigIssue(Directory, null, $"cannot create the default config files: {ex.Message}", IsError: true));
        }

        var result = Reload(force: true, createKeybinds: false); // already tried just above
        return issues.Count == 0 ? result : result with { Issues = issues.Concat(result.Issues).ToList() };
    }

    /// <param name="force">Parse again even if a file is byte-identical to the last applied one (e.g. to pick up a new environment variable).</param>
    public ReloadResult Reload(bool force = false) => Reload(force, createKeybinds: true);

    private ReloadResult Reload(bool force, bool createKeybinds)
    {
        lock (_gate)
        {
            var issues = new List<ConfigIssue>();
            var old = _current;

            // A keybinds.toml deleted while the app runs (or not created at start because an old file had a syntax error)
            // is written again, as at the first start.
            if (createKeybinds && !File.Exists(KeybindsPath))
            {
                try
                {
                    issues.AddRange(KeybindsMigration.EnsureExists(Directory));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    issues.Add(new ConfigIssue(KeybindsLoader.FileName, null, $"cannot be created: {ex.Message}", IsError: true));
                }
            }

            var settingsText = ReadFile(SettingsPath, SettingsLoader.FileName, issues);
            var promptsText = ReadFile(PromptsPath, PromptsLoader.FileName, issues);
            var keybindsText = ReadFile(KeybindsPath, KeybindsLoader.FileName, issues);

            AppSettings? newSettings = null;
            if (settingsText is not null)
            {
                if (!force && settingsText == _settingsText)
                {
                    newSettings = old.Settings; // unchanged: keep the very same object so nothing is flagged as changed
                }
                else
                {
                    var parsed = SettingsLoader.Parse(settingsText, _getEnv, _keyStore, _fontExists);
                    issues.AddRange(parsed.Issues);
                    newSettings = parsed.Settings;
                }
            }

            IReadOnlyList<PromptDefinition>? newPrompts = null;
            IReadOnlyDictionary<string, int> newLines = _promptLines;
            if (promptsText is not null)
            {
                if (!force && promptsText == _promptsText)
                {
                    newPrompts = _prompts;
                }
                else
                {
                    var parsed = PromptsLoader.Parse(promptsText);
                    issues.AddRange(parsed.Issues);
                    newPrompts = parsed.Prompts;
                    newLines = parsed.Lines;
                }
            }

            KeybindSettings? newKeybinds = null;
            if (keybindsText is not null)
            {
                if (!force && keybindsText == _keybindsText)
                {
                    newKeybinds = old.Keybinds;
                }
                else
                {
                    var parsed = KeybindsLoader.Parse(keybindsText);
                    issues.AddRange(parsed.Issues);
                    newKeybinds = parsed.Keybinds;
                }
            }

            // Which of the new files go live together? All, if they fit; otherwise as many as still fit, each missing one
            // replaced by its last working version.
            var cross = ConfigValidator.Validate(newSettings ?? old.Settings, newPrompts ?? _prompts, newKeybinds ?? old.Keybinds, newLines);
            issues.AddRange(cross);

            var (useSettings, usePrompts, useKeybinds) = (newSettings is not null, newPrompts is not null, newKeybinds is not null);
            if (cross.Any(i => i.IsError))
            {
                (useSettings, usePrompts, useKeybinds) = BestFit(newSettings, newPrompts, newLines, newKeybinds, old);
            }

            var settings = useSettings ? newSettings! : old.Settings;
            var prompts = usePrompts ? newPrompts! : _prompts;
            var keybinds = useKeybinds ? newKeybinds! : old.Keybinds;
            var settingsChanged = useSettings && !ReferenceEquals(settings, old.Settings);
            var promptsChanged = usePrompts && !ReferenceEquals(prompts, _prompts);
            var keybindsChanged = useKeybinds && !ReferenceEquals(keybinds, old.Keybinds);

            if (useSettings) _settingsText = settingsText;
            if (usePrompts)
            {
                _promptsText = promptsText;
                _promptLines = newLines;
            }

            if (useKeybinds) _keybindsText = keybindsText;

            // A new list only when prompts or their hotkeys changed: the overlay redraws its rows when the list is another one.
            var withHotkeys = promptsChanged || keybindsChanged
                ?prompts.Select(p => p with { Hotkey = keybinds.HotkeyOf(p.Name) }).ToList()
                : old.Prompts;
            _prompts = prompts;

            var snapshot = new ConfigSnapshot(settings, withHotkeys, keybinds);
            Volatile.Write(ref _current, snapshot);
            return new ReloadResult(snapshot, settingsChanged, promptsChanged, issues, keybindsChanged);
        }
    }

    /// <summary>
    /// The largest set of new files that fits the last working versions of the others. Among sets of the same size,
    /// settings.toml is kept first, then keybinds.toml, then prompts.toml.
    /// </summary>
    private (bool Settings, bool Prompts, bool Keybinds) BestFit(
        AppSettings? settings, IReadOnlyList<PromptDefinition>? prompts, IReadOnlyDictionary<string, int> lines, KeybindSettings? keybinds,
        ConfigSnapshot old)
    {
        // Bits: 4 = settings, 2 = keybinds, 1 = prompts; the full set already failed.
        foreach (var mask in new[] { 6, 5, 3, 4, 2, 1 })
        {
            var (s, k, p) = ((mask & 4) != 0, (mask & 2) != 0, (mask & 1) != 0);
            if ((s && settings is null) || (k && keybinds is null) || (p && prompts is null)) continue;

            var issues = ConfigValidator.Validate(
                s ? settings! : old.Settings, p ? prompts! : _prompts, k ? keybinds! : old.Keybinds, p ? lines : _promptLines);
            if (!issues.Any(i => i.IsError)) return (s, p, k);
        }

        return (false, false, false);
    }

    /// <summary>Reads a file the way editors leave it: shared access, a few retries for half-written saves.</summary>
    private static string? ReadFile(string path, string name, List<ConfigIssue> issues)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                return reader.ReadToEnd();
            }
            catch (FileNotFoundException)
            {
                issues.Add(new ConfigIssue(name, null, "file not found; the previous configuration stays active", IsError: true));
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                issues.Add(new ConfigIssue(name, null, "file not found; the previous configuration stays active", IsError: true));
                return null;
            }
            catch (IOException) when (attempt < 5)
            {
                Thread.Sleep(40); // the editor may still hold the file
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                issues.Add(new ConfigIssue(name, null, $"cannot be read ({ex.Message}); the previous configuration stays active", IsError: true));
                return null;
            }
        }
    }
}
