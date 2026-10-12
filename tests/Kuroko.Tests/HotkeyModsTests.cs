using Kuroko.Core.Config;
using Kuroko.Core.Hotkeys;

namespace Kuroko.Tests;

public class HotkeyModsTests
{
    private const HotkeyModifiers CtrlAlt = HotkeyModifiers.Ctrl | HotkeyModifiers.Alt;
    private const HotkeyModifiers ShiftAlt = HotkeyModifiers.Shift | HotkeyModifiers.Alt;

    private static readonly IReadOnlyDictionary<string, HotkeyModifiers> Mods =
        new Dictionary<string, HotkeyModifiers>(StringComparer.OrdinalIgnoreCase)
        {
            ["main"] = HotkeyModifiers.Ctrl, ["card"] = CtrlAlt, ["replace"] = ShiftAlt,
        };

    private const string Provider = "[providers.x]\ntype = \"openai-compatible\"\nmodel = \"m\"\n";

    // ---- expansion ----

    [Theory]
    [InlineData("$card+S", "Ctrl+Alt+S")]
    [InlineData("$replace+C", "Alt+Shift+C")] // canonical order, as ToString writes modifiers
    [InlineData("$main+Alt+K", "Ctrl+Alt+K")]
    [InlineData("$main + Shift + K", "Ctrl + Shift + K")]
    [InlineData("$MAIN+K", "Ctrl+K")] // names are not case-sensitive, like modifier names
    [InlineData("Ctrl+Alt+K", "Ctrl+Alt+K")]
    public void Variables_are_filled_in(string text, string expected)
    {
        Assert.True(HotkeyMods.TryExpand(text, Mods, out var expanded, out var error), error);
        Assert.Equal(expected, expanded);
    }

    [Fact]
    public void Expanded_hotkey_parses_to_the_same_gesture_as_the_written_one()
    {
        Assert.Equal(HotkeyGesture.Parse("Ctrl+Alt+S", null), HotkeyGesture.Parse("$card+S", Mods, null));
        Assert.Equal(HotkeyGesture.Parse("Ctrl+Alt+K", null), HotkeyGesture.Parse("$main+Alt+K", Mods, null));
        Assert.Equal("Ctrl+Alt+Shift+K", HotkeyGesture.Parse("$replace+Ctrl+K", Mods, null).ToString(null));
    }

    [Fact]
    public void Unknown_variable_names_the_known_ones()
    {
        Assert.False(HotkeyGesture.TryParse("$cards+S", Mods, null, out _, out var error));
        Assert.Contains("Unknown variable '$cards'", error);
        Assert.Contains("$card, $main, $replace", error);
        Assert.Contains("docs/keybindings.md", error);
    }

    [Fact]
    public void Unknown_variable_without_any_mods_says_so()
    {
        Assert.False(HotkeyGesture.TryParse("$card+S", HotkeyMods.None, null, out _, out var error));
        Assert.Contains("No variables are defined", error);
    }

    [Theory]
    [InlineData("$+K")]
    [InlineData("Ctrl+$")]
    public void Dollar_without_a_name_is_an_error(string text)
    {
        Assert.False(HotkeyGesture.TryParse(text, Mods, null, out _, out var error));
        Assert.Contains("must be followed by a variable name", error);
    }

    [Fact]
    public void Modifier_twice_after_expansion_shows_written_and_expanded_text()
    {
        Assert.False(HotkeyGesture.TryParse("$card+Ctrl+K", Mods, null, out _, out var error));
        Assert.Contains("appears twice", error);
        Assert.Contains("'Ctrl+Alt+Ctrl+K'", error);
        Assert.Contains("written as '$card+Ctrl+K'", error);
    }

    [Fact]
    public void Variable_as_the_key_is_an_error()
    {
        Assert.False(HotkeyGesture.TryParse("Win+$card", Mods, null, out _, out var error));
        Assert.Contains("no key", error);
        Assert.Contains("written as 'Win+$card'", error);
    }

    // ---- [mods] in settings.toml ----

    [Fact]
    public void Settings_read_mods_and_store_their_hotkeys_filled_in()
    {
        var result = SettingsLoader.Parse(
            "overlay_hotkey = \"$main+Shift+Space\"\nundo_hotkey = \"$card+Z\"\n" + Provider + "[mods]\nmain = \"Ctrl\"\ncard = \"Ctrl+Alt\"\nReplace = \"Shift + Alt\"\n",
            _ => null);
        Assert.True(result.Ok, string.Join("; ", result.Issues));
        var settings = result.Settings!;
        Assert.Equal("Ctrl+Shift+Space", settings.OverlayHotkey);
        Assert.Equal("Ctrl+Alt+Z", settings.UndoHotkey);
        Assert.Equal(ShiftAlt, settings.Mods["replace"]);
        Assert.Equal(3, settings.Mods.Count);
    }

    [Theory]
    [InlineData("main = \"Ctrl+K\"", "'K' is not a modifier")]
    [InlineData("main = \"\"", "not a modifier combination")]
    [InlineData("main = \"Ctrl+\"", "not a modifier combination")]
    [InlineData("main = \"Ctrl+Ctrl\"", "appears twice")]
    [InlineData("main = \"$card\"", "not other variables")]
    [InlineData("main = 3", "must be text")]
    [InlineData("\"my-mod\" = \"Ctrl\"", "letters, digits and _")]
    [InlineData("main = \"Ctrl\"\nMAIN = \"Alt\"", "defined twice")]
    public void Bad_mods_are_an_error_with_their_line(string entry, string expected)
    {
        var toml = Provider + "[mods]\n" + entry + "\n";
        var result = SettingsLoader.Parse(toml, _ => null);
        Assert.False(result.Ok);
        var issue = Assert.Single(result.Issues, i => i.IsError);
        Assert.Contains("[mods]", issue.Message);
        Assert.Contains(expected, issue.Message);
        Assert.True(issue.Line > 4, $"line {issue.Line}"); // inside the [mods] block, not at the top
    }

    [Fact]
    public void Mods_must_be_a_table()
    {
        var result = SettingsLoader.Parse("mods = \"Ctrl\"\n" + Provider, _ => null);
        Assert.Contains(result.Issues, i => i.IsError && i.Message.Contains("mods must be a table") && i.Line == 1);
    }

    [Fact]
    public void Settings_hotkey_with_unknown_variable_is_an_error_with_its_line()
    {
        var result = SettingsLoader.Parse("undo_hotkey = \"$cards+Z\"\n" + Provider + "[mods]\ncard = \"Ctrl+Alt\"\n", _ => null);
        Assert.False(result.Ok);
        Assert.Contains(result.Issues, i => i.IsError && i.Line == 1 && i.Message.StartsWith("undo_hotkey: Unknown variable '$cards'") && i.Message.Contains("$card."));
    }

    // ---- prompt hotkeys: checked with both files ----

    private static AppSettings SettingsWith(string mods)
        => SettingsLoader.Parse(Provider + "[mods]\n" + mods, _ => null).Settings!;

    private static IReadOnlyList<Core.Prompts.PromptDefinition> Prompts(string toml)
    {
        var result = PromptsLoader.Parse(toml);
        Assert.True(result.Ok, string.Join("; ", result.Issues));
        return result.Prompts!;
    }

    [Fact]
    public void Prompt_hotkeys_keep_the_variable_and_are_checked_against_settings()
    {
        var prompts = Prompts("[[prompt]]\nname = \"Shorten\"\nhotkey = \"$replace+S\"\nprompt = \"x\"\n");
        Assert.Equal("$replace+S", prompts[0].Hotkey);

        Assert.DoesNotContain(ConfigValidator.Validate(SettingsWith("replace = \"Shift+Alt\"\n"), prompts), i => i.IsError);

        var issues = ConfigValidator.Validate(SettingsWith("card = \"Ctrl+Alt\"\n"), prompts, new Dictionary<string, int> { ["Shorten"] = 1 });
        var issue = Assert.Single(issues, i => i.IsError);
        Assert.Equal("prompts.toml", issue.File);
        Assert.Equal(1, issue.Line);
        Assert.StartsWith("prompt 'Shorten': Unknown variable '$replace'", issue.Message);
        Assert.Contains("$card", issue.Message);
    }

    [Fact]
    public void Duplicate_hotkeys_are_found_after_expansion()
    {
        var settings = SettingsWith("card = \"Ctrl+Alt\"\nalso = \"Alt+Ctrl\"\n");
        var prompts = Prompts(
            "[[prompt]]\nname = \"A\"\nhotkey = \"$card+S\"\nprompt = \"x\"\n\n[[prompt]]\nname = \"B\"\nhotkey = \"$also+S\"\nprompt = \"y\"\n\n"
            + "[[prompt]]\nname = \"C\"\nhotkey = \"Ctrl+Alt+Z\"\nprompt = \"z\"\n");

        var issues = ConfigValidator.Validate(settings, prompts);

        Assert.Contains(issues, i => i.IsError && i.Message == "hotkey Ctrl+Alt+S is used twice: prompt 'A' and prompt 'B'");
        // Ctrl+Alt+Z is also the default undo_hotkey.
        Assert.Contains(issues, i => i.IsError && i.Message == "hotkey Ctrl+Alt+Z is used twice: undo_hotkey and prompt 'C'");
    }

    [Fact]
    public void Settings_hotkey_and_prompt_hotkey_collide_through_variables()
    {
        var settings = SettingsLoader.Parse("overlay_hotkey = \"$main+Space\"\n" + Provider + "[mods]\nmain = \"Win\"\n", _ => null).Settings!;
        var prompts = Prompts("[[prompt]]\nname = \"A\"\nhotkey = \"$main+Space\"\nprompt = \"x\"\n");
        Assert.Contains(ConfigValidator.Validate(settings, prompts), i => i.IsError && i.Message.Contains("hotkey Win+Space is used twice: overlay_hotkey and prompt 'A'"));
    }

    [Fact]
    public void AltGr_warning_also_for_prompt_hotkeys_with_variables()
    {
        var settings = SettingsWith("card = \"Ctrl+Alt\"\n");
        var prompts = Prompts("[[prompt]]\nname = \"A\"\nhotkey = \"$card+Q\"\nprompt = \"x\"\n");
        var issue = Assert.Single(ConfigValidator.Validate(settings, prompts, keyLayout: FakeKeyLayout.German));
        Assert.False(issue.IsError);
        Assert.True(issue.Show);
        Assert.Contains("'@'", issue.Message);
    }
}

public sealed class HotkeyModsReloadTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ip-mods-" + Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_dir, "settings.toml");

    private string PromptsPath => Path.Combine(_dir, "prompts.toml");

    private const string Provider = "[providers.x]\ntype = \"openai-compatible\"\nmodel = \"m\"\n";

    private const string Prompts = "[[prompt]]\nname = \"Shorten\"\nhotkey = \"$replace+S\"\nprompt = \"x\"\n";

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private ConfigManager Started()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, Provider + "[mods]\nreplace = \"Shift+Alt\"\n");
        File.WriteAllText(PromptsPath, Prompts);
        var manager = new ConfigManager(_dir, _ => null);
        var result = manager.Load();
        Assert.False(result.HasErrors, string.Join("; ", result.Issues));
        return manager;
    }

    private static HotkeyGesture PromptGesture(ConfigManager manager)
        => HotkeyGesture.Parse(manager.Current.Prompts[0].Hotkey!, manager.Current.Settings.Mods, null);

    [Fact]
    public void Changing_only_mods_changes_the_prompt_hotkeys()
    {
        var manager = Started();
        Assert.Equal(HotkeyGesture.Parse("Shift+Alt+S", null), PromptGesture(manager));

        File.WriteAllText(SettingsPath, Provider + "[mods]\nreplace = \"Ctrl+Shift\"\n");
        var result = manager.Reload();

        Assert.False(result.HasErrors, string.Join("; ", result.Issues));
        Assert.True(result.SettingsChanged);
        Assert.False(result.PromptsChanged);
        Assert.True(result.Changed); // the app registers all hotkeys again on any change
        Assert.Equal(HotkeyGesture.Parse("Ctrl+Shift+S", null), PromptGesture(manager));
    }

    [Fact]
    public void Removing_a_variable_that_prompts_still_use_keeps_the_old_settings()
    {
        var manager = Started();
        var before = manager.Current.Settings;

        File.WriteAllText(SettingsPath, Provider + "[mods]\ncard = \"Ctrl+Alt\"\n");
        var result = manager.Reload();

        Assert.True(result.HasErrors);
        Assert.False(result.SettingsChanged);
        Assert.Same(before, manager.Current.Settings);
        Assert.Contains(result.Issues, i => i.IsError && i.File == "prompts.toml" && i.Message.Contains("Unknown variable '$replace'"));
    }
}
