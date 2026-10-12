using Kuroko.Core.Localization;
using Kuroko.Core.Prompts;
using Kuroko.Core.Search;

namespace Kuroko.App.Overlay;

/// <summary>One line of the picker: a prompt or, after "/", an app command.</summary>
public sealed record OverlayRow
{
    public PromptDefinition? Prompt { get; private init; }

    public AppCommand? Command { get; private init; }


    /// <summary>Quick-select digit; empty when the row has none.</summary>
    public string Number { get; private init; } = string.Empty;

    /// <summary>The prompt name, or the command as typed ("/copy").</summary>
    public string Name { get; private init; } = string.Empty;

    /// <summary>Muted text after the name, with its leading gap: the command's tray label. Empty for prompts.</summary>
    public string Detail { get; private init; } = string.Empty;

    /// <summary>Right-aligned subtle text: the prompt's own hotkey, or a command's on/off state.</summary>
    public string Trailing { get; private init; } = string.Empty;

    /// <summary>Greyed out and skipped by the keys; Enter and click do nothing on it.</summary>
    public bool IsEnabled { get; private init; } = true;

    public static OverlayRow For(PromptDefinition prompt, string number) => new()
    {
        Prompt = prompt,
        Number = number,
        Name = prompt.Name,
        Trailing = prompt.Hotkey ?? string.Empty,
    };

    /// <summary>The "/" list: the commands matching <paramref name="name"/>, in their fixed order.</summary>
    public static List<OverlayRow> ForCommands(IReadOnlyList<AppCommand> commands, string name)
        => FuzzyMatcher.Keep(commands, name, c => c.Name).Select(For).ToList();

    /// <summary>Reads the command's enabled state and on/off state now, i.e. each time the list is built.</summary>
    public static OverlayRow For(AppCommand command) => new()
    {
        Command = command,
        Name = "/" + command.Name,
        Detail = "  " + command.Label,
        Trailing = command.State is { } state ? Loc.Get(state() ? "command_on" : "command_off") : string.Empty,
        IsEnabled = command.Enabled,
    };
}

/// <summary>Moving the selection through rows of which some are disabled; kept apart from the window so it can be tested.</summary>
public static class RowNavigation
{
    /// <summary>The first enabled row, or -1 when there is none.</summary>
    public static int First(IReadOnlyList<bool> enabled)
    {
        for (var i = 0; i < enabled.Count; i++)
        {
            if (enabled[i]) return i;
        }

        return -1;
    }

    /// <summary>
    /// The row <paramref name="delta"/> steps away, clamped to the list. A disabled row is passed over in the direction of
    /// the move; if there is no enabled row that way, the nearest one back towards <paramref name="current"/> is taken.
    /// Returns <paramref name="current"/> when no other row can be selected.
    /// </summary>
    public static int Move(IReadOnlyList<bool> enabled, int current, int delta)
    {
        if (enabled.Count == 0 || delta == 0) return current;
        var step = Math.Sign(delta);
        var target = Math.Clamp(current + delta, 0, enabled.Count - 1);

        for (var i = target; i >= 0 && i < enabled.Count; i += step)
        {
            if (enabled[i]) return i;
        }

        for (var i = target - step; i != current && i >= 0 && i < enabled.Count; i -= step)
        {
            if (enabled[i]) return i;
        }

        return current;
    }
}
