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

    [Theory]
    [InlineData("$card+S", "Ctrl+Alt+S")]
    [InlineData("$replace+c", "Alt+Shift+C")]
    [InlineData("Ctrl+Alt+0xE2", "Ctrl+Alt+<")]
    [InlineData("$nope+S", "$nope+S")]
    [InlineData("Ctrl+Alt+>", "Ctrl+Alt+>")]
    public void Display_fills_in_and_canonicalizes_or_keeps_the_text(string text, string shown)
    {
        Assert.Equal(shown, HotkeyGesture.Display(text, Mods, FakeKeyLayout.German));
    }

    // ---- [mods] in keybinds.toml ----

    private static KeybindsLoadResult Parse(string toml) => KeybindsLoader.Parse(toml, FakeKeyLayout.German);

    private static KeybindSettings Load(string toml)
    {
        var result = Parse(toml);
        Assert.True(result.Ok, string.Join("; ", result.Issues));
        return result.Keybinds!;
    }

    private static readonly AppSettings Settings = SettingsLoader.Parse(Provider, _ => null).Settings!;

    private static readonly IReadOnlyList<Core.Prompts.PromptDefinition> Prompts = [new("A", "x"), new("B", "y"), new("C", "z")];

    [Fact]
    public void Keybinds_read_mods_and_store_their_hotkeys_filled_in()
    {
        var keybinds = Load("[mods]\nmain = \"Ctrl\"\ncard = \"Ctrl+Alt\"\nReplace = \"Shift + Alt\"\n\n"
            + "[app]\noverlay = \"$main+Shift+Space\"\nundo = \"$card+Z\"\n\n[prompts]\nA = \"$replace+S\"\n");

        Assert.Equal("Ctrl+Shift+Space", keybinds.OverlayHotkey);
        Assert.Equal("Ctrl+Alt+Z", keybinds.UndoHotkey);
        Assert.Equal("Alt+Shift+S", keybinds.HotkeyOf("a"));
        Assert.Equal(ShiftAlt, keybinds.Mods["replace"]);
        Assert.Equal(3, keybinds.Mods.Count);
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
        var result = Parse("[app]\noverlay = \"Ctrl+Shift+Space\"\n\n[mods]\n" + entry + "\n");
        Assert.False(result.Ok);
        var issue = Assert.Single(result.Issues, i => i.IsError);
        Assert.Contains("[mods]", issue.Message);
        Assert.Contains(expected, issue.Message);
        Assert.True(issue.Line > 4, $"line {issue.Line}"); // inside the [mods] block, not at the top
    }

    [Fact]
    public void Mods_must_be_a_table()
    {
        var result = Parse("mods = \"Ctrl\"\n");
        Assert.Contains(result.Issues, i => i.IsError && i.Message.Contains("mods must be a table") && i.Line == 1);
    }

    [Fact]
    public void Hotkey_with_unknown_variable_is_an_error_with_its_line()
    {
        var result = Parse("[app]\nundo = \"$cards+Z\"\n\n[mods]\ncard = \"Ctrl+Alt\"\n");
        Assert.False(result.Ok);
        Assert.Contains(result.Issues, i => i.IsError && i.Line == 2 && i.Message.StartsWith("[app] undo: Unknown variable '$cards'") && i.Message.Contains("$card."));
    }

    [Fact]
    public void Prompt_hotkey_with_unknown_variable_is_an_error_with_its_line()
    {
        var result = Parse("[mods]\ncard = \"Ctrl+Alt\"\n\n[prompts]\nShorten = \"$replace+S\"\n");
        var issue = Assert.Single(result.Issues, i => i.IsError);
        Assert.Equal("keybinds.toml", issue.File);
        Assert.Equal(5, issue.Line);
        Assert.StartsWith("[prompts] 'Shorten': Unknown variable '$replace'", issue.Message);
        Assert.Contains("$card", issue.Message);
    }

    [Fact]
    public void Duplicate_hotkeys_are_found_after_expansion()
    {
        var keybinds = Load("[mods]\ncard = \"Ctrl+Alt\"\nalso = \"Alt+Ctrl\"\n\n[prompts]\nA = \"$card+S\"\nB = \"$also+S\"\nC = \"Ctrl+Alt+Z\"\n");

        var issues = ConfigValidator.Validate(Settings, Prompts, keybinds);

        Assert.Contains(issues, i => i.IsError && i.Message == "hotkey Ctrl+Alt+S is used twice: prompt 'A' and prompt 'B'" && i.Line == 7);
        // Ctrl+Alt+Z is also the default undo hotkey.
        Assert.Contains(issues, i => i.IsError && i.Message == "hotkey Ctrl+Alt+Z is used twice: [app] undo and prompt 'C'");
    }

    [Fact]
    public void App_hotkey_and_prompt_hotkey_collide_through_variables()
    {
        var keybinds = Load("[mods]\nmain = \"Win\"\n\n[app]\noverlay = \"$main+Space\"\n\n[prompts]\nA = \"$main+Space\"\n");
        Assert.Contains(ConfigValidator.Validate(Settings, Prompts, keybinds),
            i => i.IsError && i.Message.Contains("hotkey Win+Space is used twice: [app] overlay and prompt 'A'"));
    }

    [Fact]
    public void AltGr_warning_also_for_prompt_hotkeys_with_variables()
    {
        var result = Parse("[mods]\ncard = \"Ctrl+Alt\"\n\n[prompts]\nA = \"$card+Q\"\n");
        Assert.True(result.Ok);
        var issue = Assert.Single(result.Issues);
        Assert.False(issue.IsError);
        Assert.True(issue.Show);
        Assert.Equal(5, issue.Line);
        Assert.Contains("'@'", issue.Message);
    }
}

public sealed class HotkeyModsReloadTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ip-mods-" + Guid.NewGuid().ToString("N"));

    private string KeybindsPath => Path.Combine(_dir, "keybinds.toml");

    private const string Provider = "[providers.x]\ntype = \"openai-compatible\"\nmodel = \"m\"\n";

    private const string PromptBindings = "\n[prompts]\nShorten = \"$replace+S\"\n";

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private ConfigManager Started()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "settings.toml"), Provider);
        File.WriteAllText(Path.Combine(_dir, "prompts.toml"), "[[prompt]]\nname = \"Shorten\"\nprompt = \"x\"\n");
        File.WriteAllText(KeybindsPath, "[mods]\nreplace = \"Shift+Alt\"\n" + PromptBindings);
        var manager = new ConfigManager(_dir, _ => null);
        var result = manager.Load();
        Assert.False(result.HasErrors, string.Join("; ", result.Issues));
        return manager;
    }

    private static HotkeyGesture PromptGesture(ConfigManager manager) => HotkeyGesture.Parse(manager.Current.Prompts[0].Hotkey!, null);

    [Fact]
    public void Changing_only_mods_changes_the_prompt_hotkeys()
    {
        var manager = Started();
        var promptsBefore = manager.Current.Prompts;
        Assert.Equal(HotkeyGesture.Parse("Shift+Alt+S", null), PromptGesture(manager));

        File.WriteAllText(KeybindsPath, "[mods]\nreplace = \"Ctrl+Shift\"\n" + PromptBindings);
        var result = manager.Reload();

        Assert.False(result.HasErrors, string.Join("; ", result.Issues));
        Assert.True(result.KeybindsChanged);
        Assert.False(result.PromptsChanged);
        Assert.False(result.SettingsChanged);
        Assert.True(result.Changed); // the app registers all hotkeys again on any change
        Assert.NotSame(promptsBefore, manager.Current.Prompts); // a new list, so the overlay shows the new hotkey
        Assert.Equal(HotkeyGesture.Parse("Ctrl+Shift+S", null), PromptGesture(manager));
    }

    [Fact]
    public void Removing_a_variable_that_a_hotkey_still_uses_keeps_the_old_keybinds()
    {
        var manager = Started();
        var before = manager.Current.Keybinds;

        File.WriteAllText(KeybindsPath, "[mods]\ncard = \"Ctrl+Alt\"\n" + PromptBindings);
        var result = manager.Reload();

        Assert.True(result.HasErrors);
        Assert.False(result.KeybindsChanged);
        Assert.Same(before, manager.Current.Keybinds);
        Assert.Contains(result.Issues, i => i.IsError && i.File == "keybinds.toml" && i.Line == 5 && i.Message.Contains("Unknown variable '$replace'"));
    }

    [Fact]
    public void An_unchanged_reload_keeps_the_same_prompt_list()
    {
        var manager = Started();
        var before = manager.Current.Prompts;

        var result = manager.Reload();

        Assert.False(result.Changed);
        Assert.Same(before, manager.Current.Prompts);
    }
}
