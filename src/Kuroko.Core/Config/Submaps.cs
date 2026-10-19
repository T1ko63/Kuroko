namespace Kuroko.Core.Config;

/// <summary>
/// A submap ([submaps.name] in keybinds.toml): the <see cref="Open"/> hotkey shows <see cref="Label"/> in the pill and,
/// for <see cref="TimeoutMs"/>, makes each of <see cref="Keys"/> a hotkey of its own, without modifiers.
/// </summary>
/// <param name="Open">The hotkey that opens the submap, with its [mods] variables already filled in.</param>
/// <param name="Line">Line of the [submaps.name] header, for messages.</param>
public sealed record SubmapDefinition(string Name, string Open, string Label, int TimeoutMs, IReadOnlyList<SubmapKey> Keys, int? Line = null)
{
    public const int DefaultTimeoutMs = 2000;
    public const int MinTimeoutMs = 100;
    public const int MaxTimeoutMs = 60000;
}

/// <summary>One key of a submap and what it runs.</summary>
/// <param name="Key">The key as written in keybinds.toml ("P", "&lt;", "0xE2").</param>
/// <param name="Target">A prompt name from prompts.toml, or an app command with a leading slash ("/undo").</param>
/// <param name="Line">Line of the key in keybinds.toml, for messages.</param>
public sealed record SubmapKey(string Key, int VirtualKey, string Target, int? Line = null)
{
    public bool IsCommand => Target.StartsWith('/');

    /// <summary>The command without its slash; only meaningful when <see cref="IsCommand"/>.</summary>
    public string CommandName => IsCommand ? Target[1..] : string.Empty;
}

/// <summary>
/// The names of the app commands, as the overlay's "/" list and a submap's "/name" targets write them. The App builds its
/// command list (tray menu and "/" list) from these names, so a submap can be checked when keybinds.toml is read.
/// </summary>
public static class CommandNames
{
    public const string Copy = "copy";
    public const string Prompts = "prompts";
    public const string Settings = "settings";
    public const string Keybinds = "keybinds";
    public const string Folder = "folder";
    public const string Reload = "reload";
    public const string Keys = "keys";
    public const string Test = "test";
    public const string Autostart = "autostart";
    public const string About = "about";
    public const string Quit = "quit";

    /// <summary>Undo the last replacement, as the undo hotkey does. A submap target only: it is not in the tray menu or the "/" list.</summary>
    public const string Undo = "undo";

    /// <summary>Every name a submap may use after "/".</summary>
    public static IReadOnlyList<string> All { get; } = [Copy, Prompts, Settings, Keybinds, Folder, Reload, Keys, Test, Autostart, About, Quit, Undo];

    public static bool IsKnown(string name) => All.Contains(name, StringComparer.OrdinalIgnoreCase);
}
