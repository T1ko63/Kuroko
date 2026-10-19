using Kuroko.Core.Hotkeys;
using Kuroko.Core.Prompts;

namespace Kuroko.Core.Config;

/// <summary>One hotkey from keybinds.toml, with its [mods] variables already filled in.</summary>
/// <param name="Line">Line in keybinds.toml, for messages; null for a default that is not written in the file.</param>
public sealed record Keybind(string Hotkey, int? Line = null);

/// <summary>A line of [prompts] in keybinds.toml: the prompt (by name, as written) and its hotkey, filled in.</summary>
public sealed record PromptKeybind(string Prompt, string Hotkey, int? Line = null);

/// <summary>Everything from keybinds.toml, validated and with defaults filled in: which key does what.</summary>
public sealed record KeybindSettings
{
    public const string DefaultOverlayHotkey = "Ctrl+Shift+Space";
    public const string DefaultUndoHotkey = "Ctrl+Alt+Z";

    /// <summary>Copies the text of the result card (overlay output) while the card is visible.</summary>
    public const string DefaultResultCopyHotkey = "Ctrl+Alt+C";

    /// <summary>Opens the overlay ([app] overlay).</summary>
    public Keybind Overlay { get; init; } = new(DefaultOverlayHotkey);

    /// <summary>Undoes the last replacement ([app] undo); only registered while undo_history in settings.toml is above 0.</summary>
    public Keybind Undo { get; init; } = new(DefaultUndoHotkey);

    /// <summary>Copies the result card ([app] result_copy); only registered while a card is visible.</summary>
    public Keybind ResultCopy { get; init; } = new(DefaultResultCopyHotkey);

    /// <summary>[prompts]: prompt name to hotkey, in file order. Names are not case-sensitive.</summary>
    public IReadOnlyList<PromptKeybind> Prompts { get; init; } = [];

    /// <summary>Modifier variables ([mods]): name (without '$', case-insensitive) to modifiers. All hotkeys above are stored filled in.</summary>
    public IReadOnlyDictionary<string, HotkeyModifiers> Mods { get; init; } = HotkeyMods.None;

    /// <summary>Submaps ([submaps.name]): a hotkey that makes a few plain keys hotkeys for a moment. Open hotkeys are stored filled in.</summary>
    public IReadOnlyList<SubmapDefinition> Submaps { get; init; } = [];

    public string OverlayHotkey => Overlay.Hotkey;

    public string UndoHotkey => Undo.Hotkey;

    public string ResultCopyHotkey => ResultCopy.Hotkey;

    /// <summary>The hotkey [prompts] gives the prompt <paramref name="name"/>, or null.</summary>
    public string? HotkeyOf(string name)
        => Prompts.FirstOrDefault(p => p.Prompt.Equals(name, StringComparison.OrdinalIgnoreCase))?.Hotkey;

    /// <summary>The first submap (in file order) with a key that runs the prompt <paramref name="name"/>, and that key; null when none does.</summary>
    public (SubmapDefinition Submap, SubmapKey Key)? SubmapOf(string name)
    {
        foreach (var submap in Submaps)
        {
            var key = submap.Keys.FirstOrDefault(k => !k.IsCommand && k.Target.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (key is not null) return (submap, key);
        }

        return null;
    }

    /// <summary>
    /// The prompt with its hotkey from [prompts] and, only when it has none, the first submap key that runs it (see
    /// <see cref="PromptDefinition.Submap"/>).
    /// </summary>
    public PromptDefinition ApplyTo(PromptDefinition prompt)
    {
        var hotkey = HotkeyOf(prompt.Name);
        var submap = hotkey is null && SubmapOf(prompt.Name) is var (map, key) ? new SubmapCall(map.Open, key.VirtualKey) : null;
        return prompt with { Hotkey = hotkey, Submap = submap };
    }

    /// <summary>Used until keybinds.toml has been read: the app hotkeys at their defaults, no prompt hotkeys, no submaps.</summary>
    public static KeybindSettings Default { get; } = new();
}

public sealed record KeybindsLoadResult(KeybindSettings? Keybinds, IReadOnlyList<ConfigIssue> Issues)
{
    public bool Ok => Keybinds is not null;
}
