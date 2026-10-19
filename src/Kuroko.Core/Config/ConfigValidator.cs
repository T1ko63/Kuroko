using Kuroko.Core.Hotkeys;
using Kuroko.Core.Prompts;

namespace Kuroko.Core.Config;

/// <summary>
/// Checks that need more than one file: providers named by prompts, prompts named by keybinds.toml, and the one place
/// where every hotkey is checked against the others and against the keys the app sends itself.
/// </summary>
public static class ConfigValidator
{
    /// <param name="keyLayout">Shows hotkeys in messages with the characters of this layout; null uses <see cref="KeyLayouts.Current"/>.</param>
    public static List<ConfigIssue> Validate(
        AppSettings settings, IReadOnlyList<PromptDefinition> prompts, KeybindSettings keybinds,
        IReadOnlyDictionary<string, int>? promptLines = null, IKeyLayout? keyLayout = null)
    {
        var layout = keyLayout ?? KeyLayouts.Current;
        var issues = new List<ConfigIssue>();
        int? LineOf(string promptName) => promptLines is not null && promptLines.TryGetValue(promptName, out var line) ? line : null;

        foreach (var prompt in prompts)
        {
            if (prompt.Provider is null) continue;

            if (!settings.Providers.TryGetValue(prompt.Provider, out var provider))
            {
                var known = settings.Providers.Count == 0 ? "none defined" : string.Join(", ", settings.Providers.Keys);
                issues.Add(new ConfigIssue(PromptsLoader.FileName, LineOf(prompt.Name),
                    $"prompt '{prompt.Name}': unknown provider '{prompt.Provider}' (defined in settings.toml: {known})", IsError: true));
            }
            else if (provider.ApiKey is null && provider.Type != Providers.ProviderType.OpenAiCompatible)
            {
                issues.Add(new ConfigIssue(PromptsLoader.FileName, LineOf(prompt.Name),
                    $"prompt '{prompt.Name}': provider '{provider.Name}' has no API key", IsError: false));
            }
        }

        const string KeybindsFile = KeybindsLoader.FileName;
        var promptNames = prompts.Count == 0 ? "none" : string.Join(", ", prompts.Select(p => p.Name));

        // Every hotkey may be used once: the app hotkeys, the prompt hotkeys and the submap open hotkeys share one namespace.
        // The loader has filled in [mods] and checked each hotkey on its own already.
        var owners = new Dictionary<HotkeyGesture, string>();
        void Claim(string hotkey, string owner, int? line)
        {
            if (!HotkeyGesture.TryParse(hotkey, layout, out var gesture, out _)) return;

            if (KeybindsLoader.CollidesWithCapture(gesture))
            {
                // The app sends exactly these keys to read and replace text; RegisterHotKey would catch those too, and they
                // would stop working in every other app while Kuroko runs.
                issues.Add(new ConfigIssue(KeybindsFile, line,
                    $"{owner}: {gesture.ToString(layout)} is used by the app to read and replace text (Ctrl+C, Ctrl+A, Ctrl+V); choose another hotkey", IsError: true));
                return;
            }

            if (owners.TryGetValue(gesture, out var existing))
            {
                issues.Add(new ConfigIssue(KeybindsFile, line, $"hotkey {gesture.ToString(layout)} is used twice: {existing} and {owner}", IsError: true));
                return;
            }

            owners[gesture] = owner;
        }

        Claim(keybinds.OverlayHotkey, "[app] overlay", keybinds.Overlay.Line);
        if (settings.UndoHistory > 0) Claim(keybinds.UndoHotkey, "[app] undo", keybinds.Undo.Line);
        Claim(keybinds.ResultCopyHotkey, "[app] result_copy", keybinds.ResultCopy.Line);
        foreach (var binding in keybinds.Prompts)
        {
            if (!prompts.Any(p => p.Name.Equals(binding.Prompt, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(new ConfigIssue(KeybindsFile, binding.Line,
                    $"[prompts] no prompt '{binding.Prompt}' in prompts.toml (prompts: {promptNames})", IsError: true));
                continue;
            }

            Claim(binding.Hotkey, $"prompt '{binding.Prompt}'", binding.Line);
        }

        foreach (var submap in keybinds.Submaps) Claim(submap.Open, $"submap '{submap.Name}'", submap.Line);

        foreach (var submap in keybinds.Submaps)
        {
            foreach (var key in submap.Keys)
            {
                // A configured hotkey without modifiers (F1-F24) is already registered, so the submap could never take that key.
                if (owners.TryGetValue(new HotkeyGesture(HotkeyModifiers.None, key.VirtualKey), out var owner))
                {
                    issues.Add(new ConfigIssue(KeybindsFile, key.Line,
                        $"[submaps.{submap.Name}] key '{key.Key}' is also the hotkey of {owner}; give one of them another key", IsError: true));
                }

                if (!key.IsCommand && !prompts.Any(p => p.Name.Equals(key.Target, StringComparison.OrdinalIgnoreCase)))
                {
                    issues.Add(new ConfigIssue(KeybindsFile, key.Line,
                        $"[submaps.{submap.Name}] key '{key.Key}' runs '{key.Target}', but prompts.toml has no prompt of that name "
                        + $"(prompts: {promptNames}; a command starts with /, e.g. \"/undo\")", IsError: true));
                }
            }
        }

        return issues;
    }
}
