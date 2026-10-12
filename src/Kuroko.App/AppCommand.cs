using Kuroko.Platform.Tray;

namespace Kuroko.App;

/// <summary>Where the focus goes after a command ran from the overlay.</summary>
public enum CommandFocus
{
    /// <summary>Back into the text field the overlay was opened over.</summary>
    Field,

    /// <summary>A window of its own (editor, Explorer, the keys or info window) takes the focus.</summary>
    Window,

    /// <summary>Nothing: the app ends.</summary>
    None,
}

/// <summary>
/// One app command, shown in the tray menu and (later) in the overlay's "/" list. Both are built from the same list,
/// so a command has one implementation and one label. Special cases belong in the entry, not in the menu or the overlay.
/// </summary>
/// <param name="Name">The command without "/", e.g. "copy".</param>
/// <param name="Label">The tray text, also shown next to the command in the overlay.</param>
/// <param name="Run">The action; the same one the tray calls.</param>
/// <param name="FocusAfter">Where the focus goes when the command ran from the overlay.</param>
public sealed record AppCommand(string Name, string Label, Action Run, CommandFocus FocusAfter)
{
    /// <summary>Read each time a menu opens; null means always available.</summary>
    public Func<bool>? IsEnabled { get; init; }

    /// <summary>An on/off state shown with the command (a check mark in the tray), read each time a menu opens; null means none.</summary>
    public Func<bool>? State { get; init; }

    /// <summary>A separator follows this entry in the tray menu.</summary>
    public bool GroupEnd { get; init; }

    public bool Enabled => IsEnabled?.Invoke() ?? true;

    /// <summary>The tray menu for <paramref name="commands"/>, in their order, with a separator after each group.</summary>
    public static IReadOnlyList<TrayMenuItem> ToTrayMenu(IReadOnlyList<AppCommand> commands)
    {
        var items = new List<TrayMenuItem>(commands.Count + 4);
        for (var i = 0; i < commands.Count; i++)
        {
            var command = commands[i];
            items.Add(new TrayMenuItem(command.Label, command.Run, Checked: command.State?.Invoke() ?? false, Enabled: command.Enabled));
            if (command.GroupEnd && i < commands.Count - 1) items.Add(new TrayMenuItem(string.Empty, () => { }, Separator: true));
        }

        return items;
    }
}
