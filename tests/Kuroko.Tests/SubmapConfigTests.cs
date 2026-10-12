using Kuroko.Core.Config;
using Kuroko.Core.Hotkeys;
using Kuroko.Core.Prompts;

namespace Kuroko.Tests;

/// <summary>[submaps.name] in settings.toml: reading, every error with its line, and the checks against prompts.toml.</summary>
public class SubmapConfigTests
{
    private const string Provider = "[providers.x]\ntype = \"openai-compatible\"\nmodel = \"m\"\n";

    private static readonly IReadOnlyList<PromptDefinition> Prompts =
    [
        new("Professioneller", "p"), new("Lockerer", "l"), new("Kürzen", "k"), new("Ausführlicher", "a"),
    ];

    private static SettingsLoadResult Parse(string toml) => SettingsLoader.Parse(toml + "\n" + Provider, _ => null, keyLayout: FakeKeyLayout.German);

    private static AppSettings Load(string toml)
    {
        var result = Parse(toml);
        Assert.True(result.Ok, string.Join("; ", result.Issues));
        return result.Settings!;
    }

    private static ConfigIssue SingleError(string toml)
    {
        var result = Parse(toml);
        Assert.False(result.Ok);
        return Assert.Single(result.Issues, i => i.IsError);
    }

    [Fact]
    public void A_submap_is_read_with_its_keys_and_targets()
    {
        var settings = Load("""
            [mods]
            replace = "Shift+Alt"

            [submaps.tone]
            open = "$replace+^"
            label = "Ton: P L K A"
            timeout = 3000
            keys = { P = "Professioneller", l = "Lockerer", "<" = "Kürzen", 0x41 = "Ausführlicher", F5 = "/undo" }
            """);

        var tone = Assert.Single(settings.Submaps);
        Assert.Equal("tone", tone.Name);
        Assert.Equal("Alt+Shift+^", HotkeyGesture.Parse(tone.Open, FakeKeyLayout.German).ToString(FakeKeyLayout.German));
        Assert.Equal("Ton: P L K A", tone.Label);
        Assert.Equal(3000, tone.TimeoutMs);
        Assert.Equal([0x50, 0x4C, 0xE2, 0x41, 0x74], tone.Keys.Select(k => k.VirtualKey));
        Assert.Equal(["Professioneller", "Lockerer", "Kürzen", "Ausführlicher", "/undo"], tone.Keys.Select(k => k.Target));
        Assert.True(tone.Keys[4].IsCommand);
        Assert.Equal("undo", tone.Keys[4].CommandName);
        Assert.Equal(4, tone.Line);
    }

    [Fact]
    public void Label_defaults_to_the_name_and_timeout_to_two_seconds()
    {
        var submap = Assert.Single(Load("[submaps.quick]\nopen = \"Ctrl+Alt+Q\"\nkeys = { P = \"Professioneller\" }\n").Submaps);
        Assert.Equal("quick", submap.Label);
        Assert.Equal(SubmapDefinition.DefaultTimeoutMs, submap.TimeoutMs);
        Assert.Equal(2000, submap.TimeoutMs);
    }

    [Fact]
    public void Keys_may_also_be_a_table_of_their_own_with_one_line_per_key()
    {
        var settings = Load("""
            [submaps.tone]
            open = "Alt+Shift+T"

            [submaps.tone.keys]
            P = "Professioneller"
            "#" = "/copy"
            """);

        Assert.Equal([0x50, 0xBF], Assert.Single(settings.Submaps).Keys.Select(k => k.VirtualKey));
    }

    [Theory]
    [InlineData("open = \"Alt+Shift+T\"\nkeys = { \"Ctrl+P\" = \"Lockerer\" }", "has modifiers", 3)]
    [InlineData("open = \"Alt+Shift+T\"\nkeys = { Esc = \"Lockerer\" }", "is Esc", 3)]
    [InlineData("open = \"Alt+Shift+T\"\nkeys = { Shift = \"Lockerer\" }", "is a modifier", 3)]
    [InlineData("open = \"Alt+Shift+T\"\nkeys = { Banana = \"Lockerer\" }", "Unknown key 'Banana'", 3)]
    [InlineData("open = \"Alt+Shift+T\"\nkeys = { \">\" = \"Lockerer\" }", "Shift+<", 3)]
    [InlineData("open = \"Alt+Shift+T\"\nkeys = { p = \"Lockerer\", P = \"Kürzen\" }", "listed twice (also as 'p')", 3)]
    [InlineData("open = \"Alt+Shift+T\"\nkeys = { P = \"/nope\" }", "unknown command '/nope'", 3)]
    [InlineData("open = \"Alt+Shift+T\"\nkeys = { P = \"\" }", "must name a prompt", 3)]
    [InlineData("open = \"Alt+Shift+T\"\nkeys = { }", "keys must list at least one key", 3)]
    [InlineData("open = \"Alt+Shift+T\"", "keys must list at least one key", 1)]
    [InlineData("keys = { P = \"Lockerer\" }", "open is missing", 1)]
    [InlineData("open = \"T\"\nkeys = { P = \"Lockerer\" }", "open: Hotkey 'T' needs at least one modifier", 2)]
    [InlineData("open = \"$nope+T\"\nkeys = { P = \"Lockerer\" }", "Unknown variable '$nope'", 2)]
    [InlineData("open = \"Alt+Shift+T\"\nlabel = \" \"\nkeys = { P = \"Lockerer\" }", "label must not be empty", 3)]
    [InlineData("open = \"Alt+Shift+T\"\ntimeout = 0\nkeys = { P = \"Lockerer\" }", "timeout must be between 100 and 60000", 3)]
    [InlineData("open = \"Alt+Shift+T\"\ntimeout = 60001\nkeys = { P = \"Lockerer\" }", "timeout must be between", 3)]
    public void A_wrong_submap_is_an_error_with_its_line(string body, string message, int line)
    {
        var issue = SingleError($"[submaps.tone]\n{body}\n");
        Assert.Contains(message, issue.Message);
        Assert.StartsWith("[submaps.tone] ", issue.Message);
        Assert.Equal(line, issue.Line);
    }

    [Fact]
    public void A_name_with_other_characters_is_an_error()
    {
        Assert.Contains("name 'my tone' is not allowed", SingleError("[submaps.\"my tone\"]\nopen = \"Alt+Shift+T\"\nkeys = { P = \"Lockerer\" }\n").Message);
    }

    [Fact]
    public void An_unknown_field_is_a_warning()
    {
        var result = Parse("[submaps.tone]\nopen = \"Alt+Shift+T\"\nlable = \"x\"\nkeys = { P = \"Lockerer\" }\n");
        Assert.True(result.Ok);
        var issue = Assert.Single(result.Issues, i => i.Message.Contains("unknown setting 'lable'"));
        Assert.Equal(3, issue.Line);
    }

    [Fact]
    public void A_submap_key_that_needs_no_modifier_would_not_be_a_hotkey_anywhere_else()
    {
        Assert.False(HotkeyGesture.TryParse("P", FakeKeyLayout.German, out _, out _));
        Assert.True(HotkeyGesture.TryParseSubmapKey("P", FakeKeyLayout.German, out var vk, out _));
        Assert.Equal('P', vk);
        Assert.True(HotkeyGesture.TryParseSubmapKey("+", FakeKeyLayout.German, out vk, out _));
        Assert.Equal(0xBB, vk);
        Assert.True(HotkeyGesture.TryParseSubmapKey("Space", FakeKeyLayout.German, out vk, out _));
        Assert.Equal(0x20, vk);
    }

    // ---- against prompts.toml and the other hotkeys ----

    [Fact]
    public void Prompt_targets_must_exist_in_prompts_toml()
    {
        var settings = Load("[submaps.tone]\nopen = \"Alt+Shift+T\"\nkeys = { P = \"Professioneller\", K = \"Kurz\", U = \"/undo\" }\n");

        var issue = Assert.Single(ConfigValidator.Validate(settings, Prompts, keyLayout: FakeKeyLayout.German), i => i.IsError);

        Assert.Contains("key 'K' runs 'Kurz', but prompts.toml has no prompt of that name", issue.Message);
        Assert.Equal(SettingsLoader.FileName, issue.File);
        Assert.Equal(3, issue.Line);
    }

    [Fact]
    public void Prompt_targets_are_not_case_sensitive()
    {
        var settings = Load("[submaps.tone]\nopen = \"Alt+Shift+T\"\nkeys = { P = \"professioneller\" }\n");
        Assert.DoesNotContain(ConfigValidator.Validate(settings, Prompts, keyLayout: FakeKeyLayout.German), i => i.IsError);
    }

    [Fact]
    public void The_open_hotkey_counts_in_the_duplicate_check()
    {
        var settings = Load("[mods]\ncard = \"Ctrl+Alt\"\n\n[submaps.tone]\nopen = \"$card+K\"\nkeys = { P = \"Professioneller\" }\n");
        IReadOnlyList<PromptDefinition> prompts = [.. Prompts, new PromptDefinition("Fix", "f", Hotkey: "Ctrl+Alt+K")];

        var issue = Assert.Single(ConfigValidator.Validate(settings, prompts, keyLayout: FakeKeyLayout.German), i => i.IsError);

        Assert.Contains("hotkey Ctrl+Alt+K is used twice", issue.Message);
        Assert.Contains("submap 'tone'", issue.Message);
    }

    [Fact]
    public void A_submap_key_cannot_be_a_hotkey_that_already_stands_alone()
    {
        var settings = Load("[submaps.tone]\nopen = \"Alt+Shift+T\"\nkeys = { F8 = \"Professioneller\" }\n");
        IReadOnlyList<PromptDefinition> prompts = [.. Prompts, new PromptDefinition("Fix", "f", Hotkey: "F8")];

        var issue = Assert.Single(ConfigValidator.Validate(settings, prompts, keyLayout: FakeKeyLayout.German), i => i.IsError);

        Assert.Contains("key 'F8' is also the hotkey of prompt 'Fix'", issue.Message);
    }

    [Fact]
    public void A_settings_file_whose_submap_names_a_missing_prompt_is_rejected_and_the_old_one_stays()
    {
        var dir = Directory.CreateTempSubdirectory("kuroko-submaps").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, PromptsLoader.FileName), "[[prompt]]\nname = \"Lockerer\"\nprompt = \"l\"\n");
            File.WriteAllText(Path.Combine(dir, SettingsLoader.FileName), Provider);
            var manager = new ConfigManager(dir, _ => null);
            Assert.False(manager.Load().HasErrors);

            File.WriteAllText(Path.Combine(dir, SettingsLoader.FileName),
                "[submaps.tone]\nopen = \"Alt+Shift+T\"\nkeys = { P = \"Professioneller\" }\n" + Provider);
            var result = manager.Reload();

            Assert.True(result.HasErrors);
            Assert.False(result.SettingsChanged);
            Assert.Empty(manager.Current.Settings.Submaps);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
