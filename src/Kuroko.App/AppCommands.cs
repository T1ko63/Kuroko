using Kuroko.Core.Config;
using Kuroko.Core.Localization;

namespace Kuroko.App;

/// <summary>What the app commands do. <see cref="App"/> implements it; tests use a fake to check the list itself.</summary>
public interface ICommandActions
{
    bool HasLastResult { get; }

    bool Autostart { get; }

    void CopyLastResult();

    void OpenPrompts();

    void OpenSettings();

    void OpenKeybinds();

    void OpenFolder();

    void Reload();

    void ShowKeys();

    void TestConnection();

    void ToggleAutostart();

    void ShowAbout();

    void Quit();
}

public static class AppCommands
{
    /// <summary>
    /// All app commands in their fixed order: the tray menu and the overlay's command list are built from this one list.
    /// Built anew each time, so the labels follow the current language. The names come from <see cref="CommandNames"/>, which
    /// keybinds.toml is checked against ("/name" in a submap): a new command gets its name there.
    /// </summary>
    public static IReadOnlyList<AppCommand> Create(ICommandActions actions) =>
    [
        new(CommandNames.Copy, Loc.Get("tray_copy_last"), actions.CopyLastResult, CommandFocus.Field)
        {
            IsEnabled = () => actions.HasLastResult, GroupEnd = true,
        },
        new(CommandNames.Prompts, Loc.Get("tray_edit_prompts"), actions.OpenPrompts, CommandFocus.Window),
        new(CommandNames.Settings, Loc.Get("tray_edit_settings"), actions.OpenSettings, CommandFocus.Window),
        new(CommandNames.Keybinds, Loc.Get("tray_edit_keybinds"), actions.OpenKeybinds, CommandFocus.Window),
        new(CommandNames.Folder, Loc.Get("tray_open_config"), actions.OpenFolder, CommandFocus.Window),
        new(CommandNames.Reload, Loc.Get("tray_reload"), actions.Reload, CommandFocus.Field) { GroupEnd = true },
        new(CommandNames.Keys, Loc.Get("tray_keys"), actions.ShowKeys, CommandFocus.Window),
        new(CommandNames.Test, Loc.Get("tray_test"), actions.TestConnection, CommandFocus.Field),
        new(CommandNames.Autostart, Loc.Get("tray_autostart"), actions.ToggleAutostart, CommandFocus.Field)
        {
            State = () => actions.Autostart, GroupEnd = true,
        },
        new(CommandNames.About, Loc.Get("tray_about"), actions.ShowAbout, CommandFocus.Window),
        new(CommandNames.Quit, Loc.Get("tray_quit"), actions.Quit, CommandFocus.None),
    ];
}
