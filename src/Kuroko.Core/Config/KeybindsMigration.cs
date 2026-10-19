using Tomlyn;
using Tomlyn.Model;

namespace Kuroko.Core.Config;

/// <summary>
/// Creates keybinds.toml when it is missing. From older versions it takes the hotkeys over: overlay_hotkey, undo_hotkey,
/// result_copy_hotkey, [mods] and [submaps] from settings.toml and the hotkey of every prompt in prompts.toml. Like the
/// folder move from the old product names (<see cref="LegacyConfigMigration"/>) it never overwrites: settings.toml and
/// prompts.toml stay exactly as they are (the old keys are ignored from now on, with a warning), and an existing
/// keybinds.toml is never touched. Without older files (a new install) it writes <see cref="DefaultKeybinds.Create"/>.
/// </summary>
public static class KeybindsMigration
{
    private static readonly (string Old, string New)[] AppKeys =
    [
        ("overlay_hotkey", "overlay"), ("undo_hotkey", "undo"), ("result_copy_hotkey", "result_copy"),
    ];

    /// <summary>Writes keybinds.toml into <paramref name="directory"/> if it does not exist yet.</summary>
    /// <returns>What was done, for the log (and, when hotkeys were taken over, once for the user); empty if the file existed.</returns>
    public static IReadOnlyList<ConfigIssue> EnsureExists(string directory)
    {
        var path = Path.Combine(directory, KeybindsLoader.FileName);
        if (File.Exists(path)) return [];

        var settingsPath = Path.Combine(directory, SettingsLoader.FileName);
        var promptsPath = Path.Combine(directory, PromptsLoader.FileName);
        var (text, notes) = Create(
            File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null,
            File.Exists(promptsPath) ? File.ReadAllText(promptsPath) : null);
        if (text is null) return notes;

        try
        {
            DefaultKeybinds.Write(path, text);
        }
        catch (IOException) when (File.Exists(path))
        {
            return []; // written by someone else just now: theirs stays
        }

        return notes;
    }

    /// <summary>The text of a new keybinds.toml from the old files (null when there is none).</summary>
    /// <returns>Null text when an old file has a syntax error: its hotkeys cannot be read yet, so nothing is written.</returns>
    public static (string? Text, List<ConfigIssue> Notes) Create(string? settingsText, string? promptsText)
    {
        var notes = new List<ConfigIssue>();
        if (settingsText is null && promptsText is null)
        {
            notes.Add(Note("created with the default hotkeys", show: false));
            return (DefaultKeybinds.Create(), notes);
        }

        if (!TryParse(settingsText, SettingsLoader.FileName, notes, out var settings)
            || !TryParse(promptsText, PromptsLoader.FileName, notes, out var prompts))
        {
            return (null, notes);
        }

        var taken = new List<string>();
        var skipped = new List<string>();

        var app = new List<(string Key, string Hotkey)>();
        foreach (var (key, fallback) in KeybindsLoader.AppKeys)
        {
            var old = AppKeys.First(k => k.New == key).Old;
            if (settings?.TryGetValue(old, out var value) == true && value is string hotkey)
            {
                app.Add((key, hotkey));
                taken.Add(old);
            }
            else
            {
                if (settings?.ContainsKey(old) == true) skipped.Add(old);
                app.Add((key, fallback));
            }
        }

        var mods = new List<(string Name, string Value)>();
        if (settings?.TryGetValue("mods", out var modsValue) == true)
        {
            if (modsValue is TomlTable table)
            {
                foreach (var (name, value) in table)
                {
                    if (DefaultKeybinds.Value(value) is { } written) mods.Add((name, written));
                    else skipped.Add($"[mods] {name}");
                }

                taken.Add("[mods]");
            }
            else
            {
                skipped.Add("mods");
            }
        }

        var submaps = new List<(string Name, IReadOnlyList<(string Field, string Value)> Fields)>();
        if (settings?.TryGetValue("submaps", out var submapsValue) == true)
        {
            if (submapsValue is TomlTable all)
            {
                foreach (var (name, raw) in all)
                {
                    if (raw is not TomlTable submap)
                    {
                        skipped.Add($"submaps.{name}");
                        continue;
                    }

                    var fields = new List<(string Field, string Value)>();
                    foreach (var (field, value) in submap)
                    {
                        if (DefaultKeybinds.Value(value) is { } written) fields.Add((field, written));
                        else skipped.Add($"[submaps.{name}] {field}");
                    }

                    submaps.Add((name, fields));
                }

                if (submaps.Count > 0) taken.Add(submaps.Count == 1 ? "1 submap" : $"{submaps.Count} submaps");
            }
            else
            {
                skipped.Add("submaps");
            }
        }

        IReadOnlyList<(string Prompt, string Hotkey)> promptHotkeys = DefaultKeybinds.PromptHotkeys;
        if (prompts is not null)
        {
            var list = new List<(string Prompt, string Hotkey)>();
            if (prompts.TryGetValue("prompt", out var promptValue) && promptValue is TomlTableArray tables)
            {
                foreach (var table in tables)
                {
                    if (table.TryGetValue("name", out var n) && n is string name && name.Trim().Length > 0
                        && table.TryGetValue("hotkey", out var h) && h is string hotkey && hotkey.Trim().Length > 0)
                    {
                        list.Add((name.Trim(), hotkey.Trim()));
                    }
                }
            }

            promptHotkeys = list;
            if (list.Count > 0) taken.Add(list.Count == 1 ? "1 prompt hotkey" : $"{list.Count} prompt hotkeys");
        }

        var text = DefaultKeybinds.Build(new KeybindsContent(mods, app, promptHotkeys, submaps));
        notes.Add(taken.Count == 0
            ? Note("created with the default hotkeys", show: false)
            : Note($"created from the hotkeys in settings.toml and prompts.toml ({string.Join(", ", taken)}); those files were not changed, "
                   + "their hotkeys are no longer read", show: true));
        if (skipped.Count > 0)
        {
            notes.Add(Note($"not taken over because the value could not be read: {string.Join(", ", skipped)}", show: true));
        }

        return (text, notes);
    }

    private static bool TryParse(string? text, string file, List<ConfigIssue> notes, out TomlTable? table)
    {
        table = null;
        if (text is null) return true;
        try
        {
            table = TomlSerializer.Deserialize<TomlTable>(text) ?? new TomlTable();
            return true;
        }
        catch (TomlException ex)
        {
            notes.Add(new ConfigIssue(KeybindsLoader.FileName, null,
                $"not created yet: {file} cannot be read ({(ex.Line > 0 ? $"line {ex.Line}: " : string.Empty)}{ex.Message}). "
                + $"Fix {file}; its hotkeys are then moved to keybinds.toml", IsError: true));
            return false;
        }
    }

    private static ConfigIssue Note(string message, bool show) => new(KeybindsLoader.FileName, null, message, IsError: false, Show: show);
}
