using Kuroko.App;

namespace Kuroko.Windows.Tests;

public class AppCommandTests
{
    [Fact]
    public void The_tray_menu_keeps_the_order_and_puts_a_separator_after_each_group_but_the_last()
    {
        AppCommand[] commands =
        [
            new("copy", "Copy", () => { }, CommandFocus.Field) { GroupEnd = true },
            new("prompts", "Prompts", () => { }, CommandFocus.Window),
            new("reload", "Reload", () => { }, CommandFocus.Field) { GroupEnd = true },
            new("quit", "Quit", () => { }, CommandFocus.None) { GroupEnd = true },
        ];

        var menu = AppCommand.ToTrayMenu(commands);

        Assert.Equal(["Copy", "", "Prompts", "Reload", "", "Quit"], menu.Select(i => i.Text));
        Assert.Equal([false, true, false, false, true, false], menu.Select(i => i.Separator));
    }

    [Fact]
    public void Enabled_and_state_are_read_when_the_menu_is_built()
    {
        var hasResult = false;
        var autostart = true;
        AppCommand[] commands =
        [
            new("copy", "Copy", () => { }, CommandFocus.Field) { IsEnabled = () => hasResult },
            new("autostart", "Autostart", () => { }, CommandFocus.Field) { State = () => autostart },
            new("about", "About", () => { }, CommandFocus.Window),
        ];

        var before = AppCommand.ToTrayMenu(commands);
        hasResult = true;
        autostart = false;
        var after = AppCommand.ToTrayMenu(commands);

        Assert.Equal([false, true, true], before.Select(i => i.Enabled));
        Assert.Equal([false, true, false], before.Select(i => i.Checked));
        Assert.Equal([true, true, true], after.Select(i => i.Enabled));
        Assert.Equal([false, false, false], after.Select(i => i.Checked));
    }

    [Fact]
    public void A_tray_entry_runs_the_command_itself()
    {
        var ran = 0;
        var menu = AppCommand.ToTrayMenu([new AppCommand("test", "Test", () => ran++, CommandFocus.Field)]);

        menu[0].OnClick();

        Assert.Equal(1, ran);
    }
}

public class AppCommandListTests
{
    private sealed class NoActions : ICommandActions
    {
        public bool HasLastResult => false;

        public bool Autostart => false;

        public void CopyLastResult() { }

        public void OpenPrompts() { }

        public void OpenSettings() { }

        public void OpenKeybinds() { }

        public void OpenFolder() { }

        public void Reload() { }

        public void ShowKeys() { }

        public void TestConnection() { }

        public void ToggleAutostart() { }

        public void ShowAbout() { }

        public void Quit() { }
    }

    [Fact]
    public void The_app_has_a_command_for_every_name_a_submap_may_use_except_undo()
    {
        // keybinds.toml is checked against CommandNames; a name the app does not have would load fine and then do nothing.
        var names = AppCommands.Create(new NoActions()).Select(c => c.Name).ToList();

        Assert.Equal(names.Distinct().Count(), names.Count);
        Assert.Equal(Kuroko.Core.Config.CommandNames.All.Where(n => n != Kuroko.Core.Config.CommandNames.Undo).Order(), names.Order());
        Assert.Contains("keybinds", names);
    }
}
