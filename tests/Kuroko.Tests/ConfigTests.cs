using Kuroko.Core.Config;
using Kuroko.Core.Prompts;
using Kuroko.Core.Providers;

namespace Kuroko.Tests;

public class PromptsLoaderTests
{
    private static PromptsLoadResult Parse(string toml) => PromptsLoader.Parse(toml);

    private static ConfigIssue OnlyError(PromptsLoadResult result) => Assert.Single(result.Issues, i => i.IsError);

    [Fact]
    public void Reads_all_fields_and_multiline_prompt_text()
    {
        var result = Parse("""
            [[prompt]]
            name = "Correction"
            mode = "transform"
            provider = "gemini"
            model = "gemini-x"
            prompt = '''
            Du bist mein Experte.
            Gib nur den Text aus.
            '''
            """);

        Assert.True(result.Ok, string.Join("; ", result.Issues));
        var prompt = Assert.Single(result.Prompts!);
        Assert.Equal("Correction", prompt.Name);
        Assert.Equal(PromptMode.Transform, prompt.Mode);
        Assert.Null(prompt.Hotkey); // hotkeys are in keybinds.toml
        Assert.Equal("gemini", prompt.Provider);
        Assert.Equal("gemini-x", prompt.Model);
        Assert.Equal("Du bist mein Experte.\nGib nur den Text aus.", prompt.Prompt.Replace("\r\n", "\n"));
    }

    [Fact]
    public void Optional_fields_default_to_transform_without_hotkey_provider_or_model()
    {
        var prompt = Assert.Single(Parse("[[prompt]]\nname = \"A\"\nprompt = \"Do it\"\n").Prompts!);
        Assert.Equal(PromptMode.Transform, prompt.Mode);
        Assert.Null(prompt.Hotkey);
        Assert.Null(prompt.Provider);
        Assert.Null(prompt.Model);
    }

    [Fact]
    public void Instruction_mode_is_recognised_case_insensitively()
    {
        var prompt = Assert.Single(Parse("[[prompt]]\nname = \"Auftrag\"\nmode = \"Instruction\"\nprompt = \"x\"\n").Prompts!);
        Assert.Equal(PromptMode.Instruction, prompt.Mode);
    }

    [Fact]
    public void Universal_mode_is_recognised_and_the_default_file_ships_a_universal_prompt()
    {
        var prompt = Assert.Single(Parse("[[prompt]]\nname = \"U\"\nmode = \"universal\"\nprompt = \"x\"\n").Prompts!);
        Assert.Equal(PromptMode.Universal, prompt.Mode);

        var defaults = Parse(DefaultPrompts.Create()).Prompts!;
        Assert.Equal("Universal", defaults[0].Name);                      // first, so it is quick-select 1 in the overlay
        Assert.Equal(PromptMode.Universal, defaults[0].Mode);
        Assert.Contains(("Universal", "Ctrl+Alt+U"), DefaultKeybinds.PromptHotkeys);
    }

    [Fact]
    public void Several_prompts_keep_their_order()
    {
        var result = Parse("[[prompt]]\nname = \"B\"\nprompt = \"b\"\n\n[[prompt]]\nname = \"A\"\nprompt = \"a\"\n");
        Assert.Equal(["B", "A"], result.Prompts!.Select(p => p.Name));
    }

    [Fact]
    public void Syntax_error_reports_the_line()
    {
        var result = Parse("[[prompt]]\nname = \"A\"\nprompt = \n");
        Assert.False(result.Ok);
        var issue = OnlyError(result);
        Assert.Equal("prompts.toml", issue.File);
        Assert.Equal(3, issue.Line);
    }

    [Theory]
    [InlineData("prompt = \"x\"", "prompt without a name")]
    [InlineData("name = \"A\"", "prompt text is missing")]
    [InlineData("name = \"A\"\nprompt = \"   \"", "prompt text is missing")]
    [InlineData("name = \"A\"\nprompt = \"x\"\nmode = \"chat\"", "mode must be one of")]
    [InlineData("name = 5\nprompt = \"x\"", "name must be text")]
    public void Invalid_prompts_are_errors(string body, string expectedMessagePart)
    {
        var result = Parse("[[prompt]]\n" + body + "\n");
        Assert.False(result.Ok);
        Assert.Contains(expectedMessagePart, OnlyError(result).Message);
    }

    [Fact]
    public void Errors_point_at_the_faulty_prompt_not_the_first_one()
    {
        var result = Parse("""
            [[prompt]]
            name = "Good"
            prompt = "ok"

            [[prompt]]
            name = "Also good"
            prompt = "ok"

            [[prompt]]
            name = "Bad"
            prompt = "x"
            mode = "chat"
            """);
        Assert.Equal(12, OnlyError(result).Line); // the "mode" line of the third block
    }

    [Fact]
    public void Duplicate_names_are_errors_regardless_of_case()
    {
        var result = Parse("[[prompt]]\nname = \"Fix\"\nprompt = \"a\"\n\n[[prompt]]\nname = \"fix\"\nprompt = \"b\"\n");
        var issue = OnlyError(result);
        Assert.Contains("used twice", issue.Message);
        Assert.Equal(5, issue.Line);
    }

    [Fact]
    public void A_name_starting_with_a_slash_is_an_error_on_its_line()
    {
        // "/" in the overlay switches to the command list, so such a prompt could never be found.
        var result = Parse("[[prompt]]\nname = \"Fix\"\nprompt = \"a\"\n\n[[prompt]]\nprompt = \"b\"\nname = \"/copy\"\n");
        var issue = OnlyError(result);
        Assert.Contains("must not start with '/'", issue.Message);
        Assert.Equal(7, issue.Line);
        Assert.Null(result.Prompts); // like every error: the last valid prompt list stays active
    }

    [Fact]
    public void Single_bracket_table_is_explained()
    {
        var result = Parse("[prompt]\nname = \"A\"\nprompt = \"x\"\n");
        Assert.Contains("[[prompt]]", OnlyError(result).Message);
    }

    [Fact]
    public void A_hotkey_in_prompts_toml_is_ignored_with_a_warning_on_its_line()
    {
        var result = Parse("[[prompt]]\nname = \"A\"\nprompt = \"x\"\nhotkey = \"Banana\"\n");

        Assert.True(result.Ok);
        Assert.Null(Assert.Single(result.Prompts!).Hotkey);
        var issue = Assert.Single(result.Issues);
        Assert.False(issue.IsError);
        Assert.True(issue.Show);
        Assert.Equal(4, issue.Line);
        Assert.Equal("prompt 'A': hotkey in prompts.toml is no longer read; keybinds live in keybinds.toml", issue.Message);
    }

    [Fact]
    public void Unknown_keys_are_only_warnings()
    {
        var result = Parse("[[prompt]]\nname = \"A\"\nprompt = \"x\"\nhotkeys = \"Ctrl+Alt+K\"\n");
        Assert.True(result.Ok);
        Assert.Contains(result.Issues, i => !i.IsError && i.Message.Contains("'hotkeys'"));
    }

    [Fact]
    public void Empty_file_is_valid_but_noted()
    {
        var result = Parse(string.Empty);
        Assert.True(result.Ok);
        Assert.Empty(result.Prompts!);
        Assert.Contains(result.Issues, i => !i.IsError && i.Message.Contains("no prompts"));
    }

    [Fact]
    public void The_generated_default_file_documents_every_prompt_key()
    {
        var text = DefaultPrompts.Create();
        foreach (var key in new[] { "name", "prompt", "mode", "output", "provider", "model" })
            Assert.Contains($"#   {key} ", text);
        Assert.Contains("\"overlay\"", text);
    }

    [Fact]
    public void The_generated_default_file_round_trips_to_the_built_in_prompts()
    {
        var result = Parse(DefaultPrompts.Create());

        Assert.True(result.Ok, string.Join("; ", result.Issues));
        Assert.Equal(BuiltInPrompts.All.Count, result.Prompts!.Count);
        for (var i = 0; i < BuiltInPrompts.All.Count; i++)
        {
            var expected = BuiltInPrompts.All[i];
            var actual = result.Prompts[i];
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.Mode, actual.Mode);
            Assert.Equal(expected.Output, actual.Output);
            Assert.Equal(expected.Prompt.Trim().Replace("\r\n", "\n"), actual.Prompt.Replace("\r\n", "\n"));
        }
    }

    [Fact]
    public void Special_characters_in_prompts_survive_the_generated_file_format()
    {
        // A prompt with quotes, backslashes and triple quotes must not break the file written on first start.
        var text = "Say \"hi\" and use C:\\temp and \"\"\" carefully.";
        var generated = "[[prompt]]\nname = \"A\"\nprompt = \"\"\"\n" + text.Replace("\\", "\\\\").Replace("\"\"\"", "\"\"\\\"") + "\n\"\"\"\n";
        Assert.Equal(text, Assert.Single(Parse(generated).Prompts!).Prompt);
    }
}

public class ConfigValidatorTests
{
    private static AppSettings Settings(params string[] providerNames)
        => AppSettings.Fallback with
        {
            Providers = providerNames.ToDictionary(
                n => n,
                n => new ProviderSettings(n, ProviderType.Gemini, "https://g.test", "m", "key", TimeSpan.FromSeconds(30)),
                StringComparer.OrdinalIgnoreCase),
        };

    [Fact]
    public void Unknown_provider_is_an_error_listing_the_known_ones()
    {
        var prompts = new[] { new PromptDefinition("Fix", "x", Provider: "nope") };
        var issue = Assert.Single(ConfigValidator.Validate(Settings("a", "b"), prompts, KeybindSettings.Default), i => i.IsError);
        Assert.Contains("unknown provider 'nope'", issue.Message);
        Assert.Contains("a, b", issue.Message);
        Assert.Equal("prompts.toml", issue.File);
    }

    [Fact]
    public void Provider_names_match_case_insensitively()
    {
        var prompts = new[] { new PromptDefinition("Fix", "x", Provider: "GEMINI") };
        Assert.DoesNotContain(ConfigValidator.Validate(Settings("gemini"), prompts, KeybindSettings.Default), i => i.IsError);
    }

    private static readonly PromptDefinition[] TwoPrompts = [new("A", "x"), new("B", "y")];

    private static KeybindSettings Keybinds(string toml)
    {
        var result = KeybindsLoader.Parse(toml, FakeKeyLayout.German);
        Assert.True(result.Ok, string.Join("; ", result.Issues));
        return result.Keybinds!;
    }

    [Fact]
    public void Two_prompts_with_the_same_hotkey_conflict_even_when_spelled_differently()
    {
        var keybinds = Keybinds("[prompts]\nA = \"Ctrl+Alt+K\"\nB = \"alt+strg+k\"\n");
        var issue = Assert.Single(ConfigValidator.Validate(Settings("a"), TwoPrompts, keybinds), i => i.IsError);
        Assert.Contains("prompt 'A'", issue.Message);
        Assert.Contains("prompt 'B'", issue.Message);
        Assert.Equal(KeybindsLoader.FileName, issue.File);
        Assert.Equal(3, issue.Line);
    }

    [Fact]
    public void Prompt_hotkey_must_not_collide_with_the_overlay_hotkey()
    {
        var keybinds = Keybinds($"[prompts]\nA = \"{KeybindSettings.DefaultOverlayHotkey}\"\n");
        var error = Assert.Single(ConfigValidator.Validate(Settings("a"), TwoPrompts, keybinds), i => i.IsError);
        Assert.Contains("[app] overlay", error.Message);
        Assert.Contains("prompt 'A'", error.Message);
    }

    [Fact]
    public void A_prompt_name_that_prompts_toml_does_not_have_is_an_error_with_its_line()
    {
        var keybinds = Keybinds("[prompts]\nA = \"Ctrl+Alt+K\"\n\"Kürzen\" = \"Ctrl+Alt+J\"\n");
        var error = Assert.Single(ConfigValidator.Validate(Settings("a"), TwoPrompts, keybinds), i => i.IsError);
        Assert.Equal(KeybindsLoader.FileName, error.File);
        Assert.Equal(3, error.Line);
        Assert.Equal("[prompts] no prompt 'Kürzen' in prompts.toml (prompts: A, B)", error.Message);
    }

    [Fact]
    public void Prompt_names_in_keybinds_are_not_case_sensitive()
    {
        var keybinds = Keybinds("[prompts]\na = \"Ctrl+Alt+K\"\n");
        Assert.DoesNotContain(ConfigValidator.Validate(Settings("a"), TwoPrompts, keybinds), i => i.IsError);
        Assert.Equal("Ctrl+Alt+K", keybinds.HotkeyOf("A"));
    }

    [Theory]
    [InlineData("[app]\noverlay = \"Ctrl+C\"\n", "[app] overlay", 2)]
    [InlineData("[app]\nundo = \"Strg+V\"\n", "[app] undo", 2)]
    [InlineData("[app]\nresult_copy = \"ctrl+a\"\n", "[app] result_copy", 2)]
    [InlineData("[prompts]\nA = \"Ctrl+C\"\n", "prompt 'A'", 2)]
    [InlineData("[mods]\nmain = \"Ctrl\"\n\n[prompts]\nB = \"$main+V\"\n", "prompt 'B'", 5)]
    [InlineData("[submaps.tools]\nopen = \"Ctrl+A\"\nkeys = { U = \"/undo\" }\n", "submap 'tools'", 1)]
    public void Plain_ctrl_c_v_and_a_are_rejected_for_every_hotkey(string toml, string owner, int line)
    {
        var error = Assert.Single(ConfigValidator.Validate(Settings("a"), TwoPrompts, Keybinds(toml)), i => i.IsError);
        Assert.StartsWith(owner + ": Ctrl+", error.Message);
        Assert.Contains("is used by the app to read and replace text (Ctrl+C, Ctrl+A, Ctrl+V)", error.Message);
        Assert.Equal(KeybindsLoader.FileName, error.File);
        Assert.Equal(line, error.Line);
    }

    [Theory]
    [InlineData("Ctrl+Shift+C")]
    [InlineData("Ctrl+Alt+V")]
    [InlineData("Alt+A")]
    public void Other_combinations_with_c_v_and_a_are_fine(string hotkey)
    {
        Assert.DoesNotContain(ConfigValidator.Validate(Settings("a"), TwoPrompts, Keybinds($"[prompts]\nA = \"{hotkey}\"\n")), i => i.IsError);
    }

    [Fact]
    public void The_undo_hotkey_is_not_checked_while_undo_is_off()
    {
        var keybinds = Keybinds("[prompts]\nA = \"Ctrl+Alt+Z\"\n"); // the default undo hotkey
        Assert.Contains(ConfigValidator.Validate(Settings("a"), TwoPrompts, keybinds), i => i.IsError);
        Assert.DoesNotContain(ConfigValidator.Validate(Settings("a") with { UndoHistory = 0 }, TwoPrompts, keybinds), i => i.IsError);
    }

    [Fact]
    public void Missing_key_of_a_prompts_provider_is_only_a_warning()
    {
        var settings = Settings("a") with
        {
            Providers = new Dictionary<string, ProviderSettings>
            {
                ["a"] = new("a", ProviderType.Gemini, "https://g.test", "m", null, TimeSpan.FromSeconds(30)),
            },
        };
        var issues = ConfigValidator.Validate(settings, [new PromptDefinition("Fix", "x", Provider: "a")], KeybindSettings.Default);
        Assert.Contains(issues, i => !i.IsError && i.Message.Contains("no API key"));
        Assert.DoesNotContain(issues, i => i.IsError);
    }
}

public sealed class ConfigManagerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ip-cfg-" + Guid.NewGuid().ToString("N"));

    private static string? Env(string name) => name == "GEMINI_API_KEY" ? "gem-key" : null;

    private ConfigManager Manager() => new(_dir, Env);

    private string SettingsPath => Path.Combine(_dir, "settings.toml");

    private string PromptsPath => Path.Combine(_dir, "prompts.toml");

    private string KeybindsPath => Path.Combine(_dir, "keybinds.toml");

    private const string TwoProviders = """
        default_provider = "gemini"
        [providers.gemini]
        type = "gemini"
        model = "m"
        api_key = "k"
        [providers.other]
        type = "anthropic"
        model = "m"
        api_key = "k"
        """;

    private const string OnePrompt = "[[prompt]]\nname = \"Fix\"\nprompt = \"fix it\"\n";

    private ConfigManager Started(string settings = TwoProviders, string prompts = OnePrompt)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, settings);
        File.WriteAllText(PromptsPath, prompts);
        var manager = Manager();
        var result = manager.Load();
        Assert.False(result.HasErrors, string.Join("; ", result.Issues));
        return manager;
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void First_start_creates_all_three_files_and_loads_the_defaults()
    {
        var manager = Manager();
        var result = manager.Load();

        Assert.False(result.HasErrors, string.Join("; ", result.Issues));
        Assert.DoesNotContain(result.Issues, i => i.Show);
        Assert.True(File.Exists(SettingsPath));
        Assert.True(File.Exists(PromptsPath));
        Assert.True(File.Exists(KeybindsPath));
        Assert.Equal(BuiltInPrompts.All.Count, manager.Current.Prompts.Count);
        Assert.Equal("gemini", manager.Current.Settings.DefaultProvider);
        Assert.True(result.SettingsChanged && result.PromptsChanged && result.KeybindsChanged);

        // The built-in prompts get their hotkeys from the new keybinds.toml.
        Assert.Equal("Ctrl+Alt+U", manager.Current.Prompts.Single(p => p.Name == "Universal").Hotkey);
        Assert.Equal("Ctrl+Alt+K", manager.Current.Prompts.Single(p => p.Name == "Correction").Hotkey);
        Assert.Null(manager.Current.Prompts.Single(p => p.Name == "Rewrite").Hotkey);
        Assert.Equal(KeybindSettings.DefaultOverlayHotkey, manager.Current.Keybinds.OverlayHotkey);
        Assert.DoesNotContain("_hotkey =", File.ReadAllText(SettingsPath));
        Assert.DoesNotContain("hotkey =", File.ReadAllText(PromptsPath));
    }

    [Fact]
    public void Reload_without_changes_reports_no_changes()
    {
        var manager = Started();
        var result = manager.Reload();
        Assert.False(result.Changed);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void Editing_prompts_applies_only_the_prompts()
    {
        var manager = Started();
        File.WriteAllText(PromptsPath, OnePrompt + "\n[[prompt]]\nname = \"New\"\nprompt = \"n\"\n");

        var result = manager.Reload();

        Assert.True(result.PromptsChanged);
        Assert.False(result.SettingsChanged);
        Assert.Equal(["Fix", "New"], manager.Current.Prompts.Select(p => p.Name));
    }

    [Fact]
    public void Syntax_error_keeps_the_last_working_prompts_and_names_file_and_line()
    {
        var manager = Started();
        var before = manager.Current;
        File.WriteAllText(PromptsPath, "[[prompt]]\nname = \"Fix\"\nprompt = \n");

        var result = manager.Reload();

        Assert.True(result.HasErrors);
        Assert.False(result.Changed);
        var issue = Assert.Single(result.Issues, i => i.IsError);
        Assert.Equal("prompts.toml", issue.File);
        Assert.Equal(3, issue.Line);
        Assert.Same(before.Prompts, manager.Current.Prompts);
    }

    [Fact]
    public void Fixing_the_file_applies_it_again()
    {
        var manager = Started();
        File.WriteAllText(PromptsPath, "broken = ");
        Assert.True(manager.Reload().HasErrors);

        File.WriteAllText(PromptsPath, OnePrompt + "\n[[prompt]]\nname = \"Fixed\"\nprompt = \"n\"\n");
        var result = manager.Reload();

        Assert.False(result.HasErrors);
        Assert.True(result.PromptsChanged);
        Assert.Equal(2, manager.Current.Prompts.Count);
    }

    [Fact]
    public void A_broken_settings_file_does_not_stop_valid_prompts_from_applying()
    {
        var manager = Started();
        var settingsBefore = manager.Current.Settings;
        File.WriteAllText(SettingsPath, "theme = ");
        File.WriteAllText(PromptsPath, OnePrompt + "\n[[prompt]]\nname = \"New\"\nprompt = \"n\"\n");

        var result = manager.Reload();

        Assert.True(result.HasErrors);
        Assert.False(result.SettingsChanged);
        Assert.True(result.PromptsChanged);
        Assert.Same(settingsBefore, manager.Current.Settings);
        Assert.Equal(2, manager.Current.Prompts.Count);
    }

    [Fact]
    public void Prompt_with_unknown_provider_is_rejected_and_points_at_its_line()
    {
        var manager = Started();
        var before = manager.Current;
        File.WriteAllText(PromptsPath, OnePrompt + "\n[[prompt]]\nname = \"Bad\"\nprovider = \"missing\"\nprompt = \"x\"\n");

        var result = manager.Reload();

        var issue = Assert.Single(result.Issues, i => i.IsError);
        Assert.Contains("unknown provider 'missing'", issue.Message);
        Assert.Equal("prompts.toml", issue.File);
        Assert.Equal(5, issue.Line);
        Assert.False(result.PromptsChanged);
        Assert.Same(before.Prompts, manager.Current.Prompts);
    }

    [Fact]
    public void Removing_a_provider_that_a_prompt_uses_rejects_the_settings_change()
    {
        var manager = Started(prompts: "[[prompt]]\nname = \"Fix\"\nprovider = \"other\"\nprompt = \"x\"\n");
        var before = manager.Current;
        File.WriteAllText(SettingsPath, TwoProviders.Replace("[providers.other]", "[providers.renamed]"));

        var result = manager.Reload();

        Assert.True(result.HasErrors);
        Assert.False(result.SettingsChanged);
        Assert.Same(before.Settings, manager.Current.Settings);
        Assert.Contains(result.Issues, i => i.IsError && i.Message.Contains("unknown provider 'other'"));
    }

    [Fact]
    public void New_provider_and_a_prompt_using_it_apply_together()
    {
        var manager = Started(settings: "[providers.gemini]\ntype = \"gemini\"\nmodel = \"m\"\napi_key = \"k\"\n");
        File.WriteAllText(SettingsPath, TwoProviders);
        File.WriteAllText(PromptsPath, "[[prompt]]\nname = \"Fix\"\nprovider = \"other\"\nprompt = \"x\"\n");

        var result = manager.Reload();

        Assert.False(result.HasErrors, string.Join("; ", result.Issues));
        Assert.True(result.SettingsChanged && result.PromptsChanged);
        Assert.Equal("other", manager.Current.Prompts[0].Provider);
    }

    [Fact]
    public void Duplicate_hotkey_in_keybinds_is_rejected()
    {
        var manager = Started(prompts: "[[prompt]]\nname = \"A\"\nprompt = \"x\"\n\n[[prompt]]\nname = \"B\"\nprompt = \"y\"\n");
        File.WriteAllText(KeybindsPath, "[prompts]\nA = \"Ctrl+Alt+J\"\nB = \"Ctrl+Alt+J\"\n");

        var result = manager.Reload();

        Assert.True(result.HasErrors);
        Assert.False(result.KeybindsChanged);
        Assert.Contains(result.Issues, i => i.IsError && i.File == "keybinds.toml" && i.Message.Contains("used twice") && i.Line == 3);
    }

    [Fact]
    public void Renaming_a_prompt_that_keybinds_names_rejects_the_prompts_change()
    {
        var manager = Started();
        File.WriteAllText(KeybindsPath, "[prompts]\nFix = \"Ctrl+Alt+J\"\n");
        Assert.True(manager.Reload().KeybindsChanged);
        var before = manager.Current;

        File.WriteAllText(PromptsPath, "[[prompt]]\nname = \"Repair\"\nprompt = \"fix it\"\n");
        var result = manager.Reload();

        Assert.True(result.HasErrors);
        Assert.False(result.PromptsChanged);
        Assert.Same(before.Prompts, manager.Current.Prompts);
        Assert.Contains(result.Issues, i => i.IsError && i.File == "keybinds.toml" && i.Line == 2 && i.Message.Contains("no prompt 'Fix' in prompts.toml"));
    }

    [Fact]
    public void Renaming_a_prompt_in_both_files_applies_both()
    {
        var manager = Started();
        File.WriteAllText(KeybindsPath, "[prompts]\nFix = \"Ctrl+Alt+J\"\n");
        manager.Reload();

        File.WriteAllText(PromptsPath, "[[prompt]]\nname = \"Repair\"\nprompt = \"fix it\"\n");
        File.WriteAllText(KeybindsPath, "[prompts]\nRepair = \"Ctrl+Alt+J\"\n");
        var result = manager.Reload();

        Assert.False(result.HasErrors, string.Join("; ", result.Issues));
        Assert.True(result.PromptsChanged && result.KeybindsChanged);
        Assert.Equal("Ctrl+Alt+J", Assert.Single(manager.Current.Prompts).Hotkey);
    }

    [Fact]
    public void A_deleted_keybinds_file_is_written_again_on_the_next_reload()
    {
        var manager = Started();
        File.Delete(KeybindsPath);

        var result = manager.Reload();

        Assert.True(File.Exists(KeybindsPath));
        Assert.False(result.HasErrors, string.Join("; ", result.Issues));
    }

    [Fact]
    public void Deleted_file_is_reported_and_the_previous_configuration_stays()
    {
        var manager = Started();
        var before = manager.Current;
        File.Delete(PromptsPath);

        var result = manager.Reload();

        Assert.Contains(result.Issues, i => i.IsError && i.File == "prompts.toml" && i.Message.Contains("not found"));
        Assert.Same(before.Prompts, manager.Current.Prompts);
    }

    [Fact]
    public void Forced_reload_parses_again_even_when_the_text_is_unchanged()
    {
        var manager = Started();
        var result = manager.Reload(force: true);
        Assert.True(result.SettingsChanged && result.PromptsChanged);
    }

    [Fact]
    public void The_retired_inline_hotkey_setting_is_only_a_warning()
    {
        // The marker feature moved into the "universal" prompt mode; an old settings.toml may still contain the key.
        var manager = Started();
        File.WriteAllText(SettingsPath, "inline_hotkey = \"Ctrl+Alt+M\"\n" + TwoProviders);

        var result = manager.Reload();

        Assert.False(result.HasErrors);
        Assert.Contains(result.Issues, i => !i.IsError && i.Message.Contains("'inline_hotkey'"));
    }
}

public sealed class ConfigWatcherTests : IDisposable
{
    // The quiet periods run on a manual clock, so no test depends on how quickly the machine schedules timers. Only the
    // FileSystemWatcher itself runs in real time; the tests wait for its events, never for a timer.
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(150);

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ip-watch-" + Guid.NewGuid().ToString("N"));
    private readonly ManualTimeProvider _time = new();

    public ConfigWatcherTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Debouncer_turns_a_burst_of_triggers_into_one_call()
    {
        var calls = 0;
        using var debouncer = new Debouncer(TimeSpan.FromMilliseconds(120), () => calls++, _time);

        // The burst lasts longer than the quiet period; every trigger starts the period again.
        for (var i = 0; i < 6; i++)
        {
            debouncer.Trigger();
            _time.Advance(TimeSpan.FromMilliseconds(20));
        }

        Assert.Equal(0, calls);
        _time.Advance(TimeSpan.FromMilliseconds(99)); // 119 ms after the last trigger: still inside the quiet period
        Assert.Equal(0, calls);
        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, calls);
        _time.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Debouncer_survives_a_throwing_handler()
    {
        var calls = 0;
        using var debouncer = new Debouncer(TimeSpan.FromMilliseconds(30), () =>
        {
            calls++;
            throw new InvalidOperationException();
        }, _time);

        debouncer.Trigger();
        _time.Advance(TimeSpan.FromMilliseconds(30));
        debouncer.Trigger(); // timer still usable
        _time.Advance(TimeSpan.FromMilliseconds(30));

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Saving_a_watched_file_raises_one_change_and_other_files_are_ignored()
    {
        var count = 0;
        using var watcher = new ConfigWatcher(_dir, Quiet, _time, "settings.toml", "prompts.toml");
        watcher.Changed += () => Interlocked.Increment(ref count);

        File.WriteAllText(Path.Combine(_dir, "other.txt"), "ignored");
        await Task.Delay(400); // real time for the watcher to deliver the event; a trigger would wait on the manual clock
        _time.Advance(Quiet * 10);
        Assert.Equal(0, Volatile.Read(ref count));

        // An editor save is several writes in a row.
        var path = Path.Combine(_dir, "prompts.toml");
        File.WriteAllText(path, "a");
        File.AppendAllText(path, "b");
        File.AppendAllText(path, "c");

        // However long the watcher takes between the events, the manual clock has not moved, so they are one burst.
        await Task.Delay(500);
        Assert.True(await AdvanceUntilAsync(() => Volatile.Read(ref count) > 0), "no change was raised");
        _time.Advance(Quiet * 10);
        Assert.Equal(1, Volatile.Read(ref count));
    }

    [Fact]
    public async Task Save_via_temp_file_and_rename_is_noticed()
    {
        var count = 0;
        using var watcher = new ConfigWatcher(_dir, Quiet, _time, "settings.toml");
        watcher.Changed += () => Interlocked.Increment(ref count);

        var temp = Path.Combine(_dir, "settings.toml.tmp");
        File.WriteAllText(temp, "x");
        File.Move(temp, Path.Combine(_dir, "settings.toml"));

        Assert.True(await AdvanceUntilAsync(() => Volatile.Read(ref count) > 0), "no change was raised");
    }

    /// <summary>Gives the watcher real time to deliver its events and lets a quiet period pass, until done (at most 5 s).</summary>
    private async Task<bool> AdvanceUntilAsync(Func<bool> done)
    {
        for (var waited = 0; waited < 5000; waited += 50)
        {
            _time.Advance(Quiet);
            if (done()) return true;
            await Task.Delay(50);
        }

        return done();
    }
}
