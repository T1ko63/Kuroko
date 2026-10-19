using System.Globalization;
using System.Text;
using Tomlyn.Model;

namespace Kuroko.Core.Config;

/// <summary>What a keybinds.toml written by the app contains; every value is TOML text exactly as it goes into the file.</summary>
/// <param name="Mods">[mods]: name and value, e.g. ("card", "\"Ctrl+Alt\"").</param>
/// <param name="App">[app]: key and hotkey, e.g. ("overlay", "Ctrl+Shift+Space").</param>
/// <param name="Prompts">[prompts]: prompt name and hotkey.</param>
/// <param name="Submaps">[submaps.name]: name and its fields as TOML text, e.g. ("open", "\"Alt+Shift+T\"").</param>
internal sealed record KeybindsContent(
    IReadOnlyList<(string Name, string Value)> Mods,
    IReadOnlyList<(string Key, string Hotkey)> App,
    IReadOnlyList<(string Prompt, string Hotkey)> Prompts,
    IReadOnlyList<(string Name, IReadOnlyList<(string Field, string Value)> Fields)> Submaps);

/// <summary>The keybinds.toml written on first start (or by <see cref="KeybindsMigration"/> from the hotkeys of older files).</summary>
public static class DefaultKeybinds
{
    /// <summary>The hotkeys of the built-in prompts (<see cref="Prompts.BuiltInPrompts"/>) in a new keybinds.toml.</summary>
    public static IReadOnlyList<(string Prompt, string Hotkey)> PromptHotkeys { get; } =
    [
        ("Universal", "Ctrl+Alt+U"),
        ("Correction", "Ctrl+Alt+K"),
    ];

    public static string Create() => Build(new KeybindsContent(
        [],
        KeybindsLoader.AppKeys.Select(k => (k.Key, k.Default)).ToList(),
        PromptHotkeys,
        []));

    internal static string Build(KeybindsContent content)
    {
        var text = new StringBuilder();
        text.AppendLine("# Kuroko - Tastenbelegung. Hier steht jeder Hotkey, und nur hier. Änderungen werden nach dem Speichern automatisch übernommen.");
        text.AppendLine("# Bei einem Fehler bleibt die letzte funktionierende Belegung aktiv und es erscheint eine Meldung mit Datei und Zeile.");
        text.AppendLine("# Ein Hotkey ist Modifier + eine Taste: \"Ctrl+Alt+K\", \"Ctrl+Alt+ü\", \"Ctrl+Alt+<\", \"F8\", \"Ctrl+Alt+0xE2\".");
        text.AppendLine("# Modifier: Ctrl (Strg), Alt, Shift (Umschalt), Win. Einfaches Strg+C, Strg+A und Strg+V geht nicht: diese Tasten sendet Kuroko selbst.");
        text.AppendLine("# Alles zu Tasten, Codes und Meldungen: docs/keybindings.md.");

        text.AppendLine();
        text.AppendLine("# Namen für Modifier-Kombinationen, in jedem Hotkey als $name nutzbar: mit card = \"Ctrl+Alt\" ist \"$card+S\" Ctrl+Alt+S.");
        text.AppendLine("[mods]");
        if (content.Mods.Count == 0)
        {
            text.AppendLine("# replace = \"Shift+Alt\"   # Prompts, die den Text ersetzen");
            text.AppendLine("# card = \"Ctrl+Alt\"        # Prompts, die eine Karte zeigen");
        }

        foreach (var (name, value) in content.Mods) text.AppendLine($"{Key(name)} = {value}");

        text.AppendLine();
        text.AppendLine("# Die Hotkeys der App:");
        text.AppendLine("#   overlay      öffnet die Prompt-Auswahl");
        text.AppendLine("#   undo         macht die letzte Ersetzung rückgängig (nur aktiv, wenn undo_history in settings.toml größer als 0 ist)");
        text.AppendLine("#   result_copy  kopiert die Ergebnis-Karte und schließt sie; nur belegt, solange eine Karte (oder die Pille eines");
        text.AppendLine("#                gescheiterten Einfügens) zu sehen ist");
        text.AppendLine("[app]");
        foreach (var (key, hotkey) in content.App) text.AppendLine($"{key} = {String(hotkey)}");

        text.AppendLine();
        text.AppendLine("# Prompt-Hotkeys: Name des Prompts aus prompts.toml = Hotkey. Er führt den Prompt direkt aus, ohne Overlay.");
        text.AppendLine("# Namen mit Leerzeichen, Umlauten oder Sonderzeichen stehen in Anführungszeichen: \"Übersetzen DE↔EN\" = \"Ctrl+Alt+T\".");
        text.AppendLine("[prompts]");
        foreach (var (prompt, hotkey) in content.Prompts) text.AppendLine($"{Key(prompt)} = {String(hotkey)}");

        text.AppendLine();
        text.AppendLine("# Submaps: ein Hotkey (open) öffnet für timeout Millisekunden einfache Tasten ohne Modifier; die Pille zeigt label,");
        text.AppendLine("# ohne label eine Liste der Tasten (Taste und Ziel, eine pro Zeile).");
        text.AppendLine("# Ziel einer Taste ist ein Prompt-Name oder ein Befehl wie \"/undo\", \"/copy\", \"/reload\".");
        if (content.Submaps.Count == 0)
        {
            text.AppendLine("# [submaps.ton]");
            text.AppendLine("# open = \"Shift+Alt+T\"");
            text.AppendLine("# timeout = 3000");
            text.AppendLine("# keys = { P = \"Professioneller\", L = \"Lockerer\" }");
        }

        foreach (var (name, fields) in content.Submaps)
        {
            text.AppendLine();
            text.AppendLine($"[submaps.{Key(name)}]");
            foreach (var (field, value) in fields) text.AppendLine($"{Key(field)} = {value}");
        }

        return text.ToString();
    }

    public static void EnsureExists(string path)
    {
        if (File.Exists(path)) return;
        Write(path, Create());
    }

    /// <summary>Writes a new file; never replaces one that appeared in the meantime.</summary>
    internal static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(text);
    }

    /// <summary>A key as TOML writes it: bare when it is only letters, digits, _ and -, otherwise quoted.</summary>
    internal static string Key(string key)
        => key.Length > 0 && key.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-') ? key : String(key);

    /// <summary>A TOML basic string.</summary>
    internal static string String(string value)
    {
        var text = new StringBuilder(value.Length + 2).Append('"');
        foreach (var c in value)
        {
            text.Append(c switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ when char.IsControl(c) => $"\\u{(int)c:X4}",
                _ => c.ToString(),
            });
        }

        return text.Append('"').ToString();
    }

    /// <summary>A value read from an older file, written back as TOML text; null for what cannot be written on one line.</summary>
    internal static string? Value(object? value) => value switch
    {
        string s => String(s),
        bool b => b ? "true" : "false",
        long l => l.ToString(CultureInfo.InvariantCulture),
        double d => FormatDouble(d),
        TomlTable table => "{ " + string.Join(", ", table.Select(e => (e.Key, Text: Value(e.Value))).Where(e => e.Text is not null).Select(e => $"{Key(e.Key)} = {e.Text}")) + " }",
        TomlArray array => "[" + string.Join(", ", array.Select(Value).Where(v => v is not null)) + "]",
        _ => null,
    };

    private static string FormatDouble(double d)
    {
        if (double.IsNaN(d)) return "nan";
        if (double.IsInfinity(d)) return d > 0 ? "inf" : "-inf";
        var text = d.ToString("R", CultureInfo.InvariantCulture);
        return text.Contains('.') || text.Contains('E') ? text : text + ".0";
    }
}
