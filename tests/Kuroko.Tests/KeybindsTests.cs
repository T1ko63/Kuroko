using Kuroko.Core.Config;

namespace Kuroko.Tests;

/// <summary>keybinds.toml: [app] and [prompts], the file written on first start, and the move of the hotkeys out of older files.</summary>
public class KeybindsLoaderTests
{
    private static KeybindsLoadResult Parse(string toml) => KeybindsLoader.Parse(toml, FakeKeyLayout.German);

    [Fact]
    public void An_empty_file_keeps_the_default_app_hotkeys_and_has_no_prompt_hotkeys()
    {
        var result = Parse(string.Empty);

        Assert.True(result.Ok);
        Assert.Empty(result.Issues);
        Assert.Equal("Ctrl+Shift+Space", result.Keybinds!.OverlayHotkey);
        Assert.Equal("Ctrl+Alt+Z", result.Keybinds.UndoHotkey);
        Assert.Equal("Ctrl+Alt+C", result.Keybinds.ResultCopyHotkey);
        Assert.Empty(result.Keybinds.Prompts);
    }

    [Fact]
    public void App_and_prompt_hotkeys_are_read_with_their_lines()
    {
        var result = Parse("""
            [app]
            overlay = "Ctrl+Alt+Space"
            result_copy = "Ctrl+Shift+C"

            [prompts]
            Correction = "Ctrl+Alt+K"
            "Übersetzen DE↔EN" = "Ctrl+Alt+ü"
            Rewrite = ""
            """);

        Assert.True(result.Ok, string.Join("; ", result.Issues));
        var keybinds = result.Keybinds!;
        Assert.Equal(new Keybind("Ctrl+Alt+Space", 2), keybinds.Overlay);
        Assert.Equal(new Keybind("Ctrl+Alt+Z"), keybinds.Undo); // not in the file: the default, without a line
        Assert.Equal(new Keybind("Ctrl+Shift+C", 3), keybinds.ResultCopy);
        Assert.Equal([new PromptKeybind("Correction", "Ctrl+Alt+K", 6), new PromptKeybind("Übersetzen DE↔EN", "Ctrl+Alt+ü", 7)], keybinds.Prompts);
        Assert.Null(keybinds.HotkeyOf("Rewrite")); // "" = no hotkey
    }

    [Theory]
    [InlineData("[app]\noverlay = \"K\"\n", "[app] overlay: Hotkey 'K' needs at least one modifier", 2)]
    [InlineData("[app]\nundo = \"\"\n", "[app] undo: Hotkey is empty.", 2)]
    [InlineData("[app]\noverlay = 5\n", "overlay must be text", 2)]
    [InlineData("[prompts]\nA = \"Banana\"\n", "[prompts] 'A': Unknown key 'Banana'", 2)]
    [InlineData("[prompts]\nA = \"Ctrl+Alt+>\"\n", "[prompts] 'A': '>' in hotkey 'Ctrl+Alt+>' is Shift+<", 2)]
    [InlineData("[prompts]\nA = 3\n", "[prompts] 'A' must be a hotkey in quotes", 2)]
    [InlineData("[prompts]\nFix = \"Ctrl+Alt+K\"\nfix = \"Ctrl+Alt+J\"\n", "[prompts] 'fix' is listed twice", 3)]
    [InlineData("app = \"x\"\n", "app must be a table", 1)]
    [InlineData("prompts = \"x\"\n", "prompts must be a table", 1)]
    [InlineData("[app]\noverlay = \n", "", 2)]
    public void A_wrong_entry_is_an_error_with_its_line(string toml, string message, int line)
    {
        var result = Parse(toml);

        Assert.False(result.Ok);
        var issue = Assert.Single(result.Issues, i => i.IsError);
        Assert.Equal(KeybindsLoader.FileName, issue.File);
        Assert.Contains(message, issue.Message);
        Assert.Equal(line, issue.Line);
    }

    [Fact]
    public void Unknown_entries_are_warnings()
    {
        var result = Parse("overlay = \"Ctrl+K\"\n\n[app]\noverlya = \"Ctrl+Alt+O\"\n");

        Assert.True(result.Ok);
        Assert.Contains(result.Issues, i => !i.IsError && i.Line == 1 && i.Message.Contains("unknown entry 'overlay'"));
        Assert.Contains(result.Issues, i => !i.IsError && i.Line == 4 && i.Message.Contains("[app] unknown hotkey 'overlya'"));
    }

    [Fact]
    public void AltGr_warning_names_the_line()
    {
        var issue = Assert.Single(Parse("[app]\nundo = \"Ctrl+Alt+Q\"\n").Issues);
        Assert.False(issue.IsError);
        Assert.True(issue.Show);
        Assert.Equal(2, issue.Line);
        Assert.Contains("'@'", issue.Message);
    }

    [Fact]
    public void The_default_file_loads_without_issues_and_matches_the_defaults()
    {
        var result = Parse(DefaultKeybinds.Create());

        Assert.True(result.Ok, string.Join("; ", result.Issues));
        Assert.Empty(result.Issues);
        var keybinds = result.Keybinds!;
        Assert.Equal(KeybindSettings.DefaultOverlayHotkey, keybinds.OverlayHotkey);
        Assert.Equal(KeybindSettings.DefaultUndoHotkey, keybinds.UndoHotkey);
        Assert.Equal(KeybindSettings.DefaultResultCopyHotkey, keybinds.ResultCopyHotkey);
        Assert.Equal(DefaultKeybinds.PromptHotkeys, keybinds.Prompts.Select(p => (p.Prompt, p.Hotkey)));
        Assert.Empty(keybinds.Mods);
        Assert.Empty(keybinds.Submaps);
    }

    [Fact]
    public void The_default_file_fits_the_default_prompts()
    {
        var prompts = PromptsLoader.Parse(DefaultPrompts.Create()).Prompts!;
        var settings = SettingsLoader.Parse(DefaultSettings.Create(_ => null), _ => null).Settings!;
        var issues = ConfigValidator.Validate(settings, prompts, Parse(DefaultKeybinds.Create()).Keybinds!, keyLayout: FakeKeyLayout.German);
        Assert.DoesNotContain(issues, i => i.IsError || i.Show);
    }
}

public class KeybindsMigrationTests
{
    private const string OldSettings = """
        overlay_hotkey = "$main+Shift+Space"
        undo_hotkey = "Ctrl+Alt+ü"
        theme = "dark"

        [mods]
        main = "Ctrl"
        replace = "Shift+Alt"

        [providers.x]
        type = "openai-compatible"
        model = "m"

        [submaps.tone]
        open = "$replace+^"
        label = "Ton: P \"pro\" · < kürzer"
        timeout = 3000
        keys = { P = "Professioneller", "<" = "Kürzen", U = "/undo" }

        [submaps.tools]
        open = "Ctrl+Alt+Shift+K"

        [submaps.tools.keys]
        R = "/reload"
        """;

    private const string OldPrompts = """
        [[prompt]]
        name = "Professioneller"
        hotkey = "$replace+P"
        prompt = "p"

        [[prompt]]
        name = "Kürzen"
        prompt = "k"

        [[prompt]]
        name = "Übersetzen DE↔EN"
        hotkey = "Ctrl+Alt+T"
        output = "overlay"
        prompt = "t"
        """;

    [Fact]
    public void The_old_hotkeys_move_to_keybinds_with_their_mods_and_submaps()
    {
        var (text, notes) = KeybindsMigration.Create(OldSettings, OldPrompts);

        Assert.NotNull(text);
        var result = KeybindsLoader.Parse(text, FakeKeyLayout.German);
        Assert.True(result.Ok, string.Join("; ", result.Issues) + "\n" + text);
        Assert.Empty(result.Issues);
        var keybinds = result.Keybinds!;

        Assert.Equal("Ctrl+Shift+Space", keybinds.OverlayHotkey);
        Assert.Equal("Ctrl+Alt+ü", keybinds.UndoHotkey);
        Assert.Equal(KeybindSettings.DefaultResultCopyHotkey, keybinds.ResultCopyHotkey); // was not set: the default
        Assert.Equal(["main", "replace"], keybinds.Mods.Keys);
        Assert.Equal([("Professioneller", "Alt+Shift+P"), ("Übersetzen DE↔EN", "Ctrl+Alt+T")], keybinds.Prompts.Select(p => (p.Prompt, p.Hotkey)));
        Assert.Contains("\"Übersetzen DE↔EN\" = \"Ctrl+Alt+T\"", text);
        Assert.Contains("Professioneller = \"$replace+P\"", text); // written as it was, the variable included

        Assert.Equal(["tone", "tools"], keybinds.Submaps.Select(s => s.Name));
        var tone = keybinds.Submaps[0];
        Assert.Equal("Ton: P \"pro\" · < kürzer", tone.Label);
        Assert.Equal(3000, tone.TimeoutMs);
        Assert.Equal(["Professioneller", "Kürzen", "/undo"], tone.Keys.Select(k => k.Target));
        Assert.Equal("/reload", Assert.Single(keybinds.Submaps[1].Keys).Target);

        var note = Assert.Single(notes);
        Assert.False(note.IsError);
        Assert.True(note.Show);
        Assert.Contains("overlay_hotkey, undo_hotkey, [mods], 2 submaps, 2 prompt hotkeys", note.Message);
    }

    [Fact]
    public void The_moved_file_fits_the_old_prompts()
    {
        var keybinds = KeybindsLoader.Parse(KeybindsMigration.Create(OldSettings, OldPrompts).Text!, FakeKeyLayout.German).Keybinds!;
        var prompts = PromptsLoader.Parse(OldPrompts).Prompts!;
        var settings = SettingsLoader.Parse(OldSettings, _ => null).Settings!;

        Assert.DoesNotContain(ConfigValidator.Validate(settings, prompts, keybinds, keyLayout: FakeKeyLayout.German), i => i.IsError);
    }

    [Fact]
    public void Without_old_files_the_default_file_is_written()
    {
        var (text, notes) = KeybindsMigration.Create(null, null);
        Assert.Equal(DefaultKeybinds.Create(), text);
        Assert.DoesNotContain(notes, n => n.IsError || n.Show);
    }

    [Fact]
    public void An_old_settings_file_without_prompts_file_gets_the_built_in_prompt_hotkeys()
    {
        var keybinds = KeybindsLoader.Parse(KeybindsMigration.Create("theme = \"dark\"\n", null).Text!).Keybinds!;
        Assert.Equal(DefaultKeybinds.PromptHotkeys, keybinds.Prompts.Select(p => (p.Prompt, p.Hotkey)));
    }

    [Fact]
    public void Old_files_without_hotkeys_give_the_default_app_hotkeys_and_no_prompt_hotkeys()
    {
        var (text, notes) = KeybindsMigration.Create("theme = \"dark\"\n", "[[prompt]]\nname = \"A\"\nprompt = \"a\"\n");
        var keybinds = KeybindsLoader.Parse(text!).Keybinds!;

        Assert.Equal(KeybindSettings.DefaultOverlayHotkey, keybinds.OverlayHotkey);
        Assert.Empty(keybinds.Prompts); // the user had none: none are added
        Assert.DoesNotContain(notes, n => n.IsError || n.Show);
    }

    [Fact]
    public void An_old_file_with_a_syntax_error_writes_nothing_yet()
    {
        var (text, notes) = KeybindsMigration.Create("theme = \n", OldPrompts);

        Assert.Null(text);
        var error = Assert.Single(notes);
        Assert.True(error.IsError);
        Assert.Equal(KeybindsLoader.FileName, error.File);
        Assert.Contains("settings.toml cannot be read (line 1", error.Message);
    }
}

public sealed class KeybindsMigrationFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ip-keys-" + Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_dir, "settings.toml");

    private string PromptsPath => Path.Combine(_dir, "prompts.toml");

    private string KeybindsPath => Path.Combine(_dir, "keybinds.toml");

    private const string Provider = "\n[providers.x]\ntype = \"openai-compatible\"\nmodel = \"m\"\n";

    private const string OldSettings = "overlay_hotkey = \"Ctrl+Alt+Space\"\nundo_hotkey = \"Ctrl+Shift+Z\"\n" + Provider;

    private const string OldPrompts = "[[prompt]]\nname = \"Fix\"\nhotkey = \"Ctrl+Alt+F\"\nprompt = \"f\"\n";

    public KeybindsMigrationFileTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void First_start_of_the_new_version_moves_the_hotkeys_and_leaves_the_old_files_as_they_are()
    {
        File.WriteAllText(SettingsPath, OldSettings);
        File.WriteAllText(PromptsPath, OldPrompts);

        var manager = new ConfigManager(_dir, _ => null);
        var result = manager.Load();

        Assert.False(result.HasErrors, string.Join("; ", result.Issues));
        Assert.Equal(OldSettings, File.ReadAllText(SettingsPath));
        Assert.Equal(OldPrompts, File.ReadAllText(PromptsPath));
        Assert.True(File.Exists(KeybindsPath));

        Assert.Equal("Ctrl+Alt+Space", manager.Current.Keybinds.OverlayHotkey);
        Assert.Equal("Ctrl+Shift+Z", manager.Current.Keybinds.UndoHotkey);
        Assert.Equal("Ctrl+Alt+F", Assert.Single(manager.Current.Prompts).Hotkey);

        // One note that the hotkeys moved, and one warning per old key, each with file and line.
        Assert.Contains(result.Issues, i => i.File == "keybinds.toml" && i.Show && i.Message.StartsWith("created from the hotkeys in settings.toml and prompts.toml"));
        Assert.Contains(result.Issues, i => !i.IsError && i.Show && i.File == "settings.toml" && i.Line == 1
            && i.Message == "overlay_hotkey in settings.toml is no longer read; keybinds live in keybinds.toml");
        Assert.Contains(result.Issues, i => !i.IsError && i.File == "settings.toml" && i.Line == 2 && i.Message.StartsWith("undo_hotkey in settings.toml"));
        Assert.Contains(result.Issues, i => !i.IsError && i.File == "prompts.toml" && i.Line == 3
            && i.Message == "prompt 'Fix': hotkey in prompts.toml is no longer read; keybinds live in keybinds.toml");
    }

    [Fact]
    public void An_existing_keybinds_file_is_never_overwritten()
    {
        const string Mine = "[app]\noverlay = \"Ctrl+Alt+O\"\n";
        File.WriteAllText(SettingsPath, OldSettings);
        File.WriteAllText(PromptsPath, OldPrompts);
        File.WriteAllText(KeybindsPath, Mine);

        var manager = new ConfigManager(_dir, _ => null);
        var result = manager.Load();
        manager.Reload(force: true);

        Assert.False(result.HasErrors, string.Join("; ", result.Issues));
        Assert.Equal(Mine, File.ReadAllText(KeybindsPath));
        Assert.Equal("Ctrl+Alt+O", manager.Current.Keybinds.OverlayHotkey);
        Assert.Null(Assert.Single(manager.Current.Prompts).Hotkey); // the old hotkey in prompts.toml is not read
        Assert.DoesNotContain(result.Issues, i => i.Message.StartsWith("created from"));
    }

    [Fact]
    public void Editing_the_old_keys_after_the_move_changes_nothing()
    {
        File.WriteAllText(SettingsPath, OldSettings);
        File.WriteAllText(PromptsPath, OldPrompts);
        var manager = new ConfigManager(_dir, _ => null);
        manager.Load();

        File.WriteAllText(SettingsPath, OldSettings.Replace("Ctrl+Alt+Space", "Ctrl+Alt+X"));
        var result = manager.Reload();

        Assert.False(result.HasErrors);
        Assert.False(result.KeybindsChanged);
        Assert.Equal("Ctrl+Alt+Space", manager.Current.Keybinds.OverlayHotkey);
    }

    [Fact]
    public void A_settings_file_with_a_syntax_error_delays_the_move_until_it_is_fixed()
    {
        File.WriteAllText(SettingsPath, "overlay_hotkey = \"Ctrl+Alt+Space\"\ntheme = \n" + Provider);
        File.WriteAllText(PromptsPath, OldPrompts);

        var manager = new ConfigManager(_dir, _ => null);
        var first = manager.Load();

        Assert.True(first.HasErrors);
        Assert.False(File.Exists(KeybindsPath));
        Assert.Contains(first.Issues, i => i.IsError && i.File == "keybinds.toml" && i.Message.StartsWith("not created yet: settings.toml cannot be read"));

        File.WriteAllText(SettingsPath, OldSettings);
        var second = manager.Reload();

        Assert.False(second.HasErrors, string.Join("; ", second.Issues));
        Assert.True(File.Exists(KeybindsPath));
        Assert.Equal("Ctrl+Alt+Space", manager.Current.Keybinds.OverlayHotkey);
        Assert.Equal("Ctrl+Alt+F", Assert.Single(manager.Current.Prompts).Hotkey);
    }
}
