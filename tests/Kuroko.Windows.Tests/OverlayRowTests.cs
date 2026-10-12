using Kuroko.App;
using Kuroko.App.Overlay;
using Kuroko.Core.Hotkeys;
using Kuroko.Core.Localization;
using Kuroko.Core.Prompts;

namespace Kuroko.Windows.Tests;

public class OverlayRowTests
{
    private static readonly Dictionary<string, HotkeyModifiers> Mods = new(StringComparer.OrdinalIgnoreCase)
    {
        ["card"] = HotkeyModifiers.Ctrl | HotkeyModifiers.Alt,
    };

    [Theory]
    [InlineData("$card+S", "Ctrl+Alt+S")]          // variables filled in
    [InlineData("alt+ctrl+k", "Ctrl+Alt+K")]        // canonical, like the log and the tray tooltip
    [InlineData("$cards+S", "$cards+S")]          // unknown variable: as written
    [InlineData("Ctrl+Banana", "Ctrl+Banana")]      // does not parse: as written
    public void A_prompt_row_shows_the_hotkey_filled_in(string hotkey, string shown)
    {
        var row = OverlayRow.For(new PromptDefinition("Fix", "Fix it.", Hotkey: hotkey), "1", Mods);
        Assert.Equal(shown, row.Trailing);
    }

    [Fact]
    public void A_prompt_row_without_a_hotkey_shows_nothing()
    {
        Assert.Equal(string.Empty, OverlayRow.For(new PromptDefinition("Fix", "Fix it."), "1", Mods).Trailing);
    }

    [Fact]
    public void A_command_row_shows_the_command_its_label_and_its_state()
    {
        var on = true;
        var command = new AppCommand("autostart", "Start with Windows", () => { }, CommandFocus.Field) { State = () => on };

        var row = OverlayRow.For(command);
        on = false;
        var later = OverlayRow.For(command);

        Assert.Equal("/autostart", row.Name);
        Assert.Equal("Start with Windows", row.Detail.Trim());
        Assert.Equal(Loc.Get("command_on"), row.Trailing);
        Assert.Equal(Loc.Get("command_off"), later.Trailing);
        Assert.Same(command, row.Command);
        Assert.Null(row.Prompt);
    }

    [Fact]
    public void A_disabled_command_gives_a_disabled_row_and_a_prompt_row_is_unchanged()
    {
        var copy = new AppCommand("copy", "Copy last result", () => { }, CommandFocus.Field) { IsEnabled = () => false };
        var prompt = new PromptDefinition("Fix", "Fix it.", Hotkey: "Ctrl+Alt+K");

        var commandRow = OverlayRow.For(copy);
        var promptRow = OverlayRow.For(prompt, "1");

        Assert.False(commandRow.IsEnabled);
        Assert.Equal(string.Empty, commandRow.Trailing);
        Assert.True(promptRow.IsEnabled);
        Assert.Equal(("Fix", "1", "Ctrl+Alt+K", ""), (promptRow.Name, promptRow.Number, promptRow.Trailing, promptRow.Detail));
    }

    [Fact]
    public void The_command_list_holds_only_the_commands_and_filters_by_name()
    {
        AppCommand[] commands =
        [
            new("copy", "Copy", () => { }, CommandFocus.Field),
            new("quit", "Quit", () => { }, CommandFocus.None),
        ];

        var all = OverlayRow.ForCommands(commands, "");
        var filtered = OverlayRow.ForCommands(commands, "q");

        Assert.Equal(["/copy", "/quit"], all.Select(r => r.Name));
        Assert.Equal(["/quit"], filtered.Select(r => r.Name));
    }

    [Theory]
    [InlineData(new[] { true, true }, 0)]
    [InlineData(new[] { false, false, true }, 2)]
    [InlineData(new[] { false, false }, -1)]
    [InlineData(new bool[0], -1)]
    public void The_selection_starts_on_the_first_enabled_row(bool[] enabled, int expected)
        => Assert.Equal(expected, RowNavigation.First(enabled));

    [Theory]
    [InlineData(1, 1, 3)]   // down over the disabled row 2
    [InlineData(3, -1, 1)]  // up over it
    [InlineData(4, 1, 4)]   // already last
    [InlineData(1, -1, 1)]  // row 0 is disabled: nothing above
    [InlineData(1, 5, 4)]   // page down: clamped to the end
    [InlineData(3, -5, 1)]  // page up: clamped, row 0 disabled, so the nearest enabled row back towards the start position
    public void Moving_skips_disabled_rows(int current, int delta, int expected)
        => Assert.Equal(expected, RowNavigation.Move([false, true, false, true, true], current, delta));
}
