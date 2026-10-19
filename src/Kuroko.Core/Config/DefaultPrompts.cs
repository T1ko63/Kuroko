using System.Text;
using Kuroko.Core.Prompts;

namespace Kuroko.Core.Config;

/// <summary>The prompts.toml written on first start, generated from the built-in prompt collection.</summary>
public static class DefaultPrompts
{
    public static string Create()
    {
        var text = new StringBuilder();
        text.AppendLine("# Kuroko - Prompts. Änderungen werden nach dem Speichern automatisch übernommen.");
        text.AppendLine("# Bearbeiten, löschen und ergänzen ist ausdrücklich erwünscht. Pro Prompt ein [[prompt]]-Block:");
        text.AppendLine("#   name     Anzeigename im Overlay (Pflicht, eindeutig, nicht mit \"/\" am Anfang: das ist für Befehle)");
        text.AppendLine("#   prompt   die Anweisung an die KI (Pflicht); sie soll nur den reinen Ergebnistext verlangen");
        text.AppendLine("#   mode     \"transform\" (Standard): der markierte Text bzw. das ganze Feld wird bearbeitet");
        text.AppendLine("#            \"instruction\": der Text im Feld ist selbst die Anweisung (Auftrag)");
        text.AppendLine("#            \"universal\": beides in einem. Ohne Marker ist der ganze Text ein Auftrag (\"Absage an Freunde\");");
        text.AppendLine("#                         mit Marker wird dessen Inhalt auf den übrigen Text angewendet (\"<<als Rapsong>> Hallo ...\").");
        text.AppendLine("#                         Die Marker-Zeichen stehen in settings.toml (marker_start / marker_end).");
        text.AppendLine("#   output   optional: wohin das Ergebnis geht, unabhängig von mode");
        text.AppendLine("#            \"replace\" (Standard): das Ergebnis ersetzt den markierten Text bzw. das ganze Feld");
        text.AppendLine("#            \"overlay\": das Ergebnis erscheint in einer kleinen Karte, der Text in der App bleibt unverändert.");
        text.AppendLine("#                       Braucht eine Markierung (gelesen wird nur der markierte Text, nie das ganze Feld),");
        text.AppendLine("#                       funktioniert auch auf schreibgeschütztem Text wie Webseiten. Esc schließt die Karte,");
        text.AppendLine("#                       result_copy in keybinds.toml kopiert sie.");
        text.AppendLine("#   provider optional: Name eines [providers.*] aus settings.toml (sonst der Standard-Anbieter)");
        text.AppendLine("#   model    optional: Modell, das statt dem des Anbieters verwendet wird");
        text.AppendLine("# Hotkeys für Prompts stehen in keybinds.toml unter [prompts], z. B. Correction = \"Ctrl+Alt+K\".");

        foreach (var prompt in BuiltInPrompts.All)
        {
            text.AppendLine();
            text.AppendLine("[[prompt]]");
            text.AppendLine($"name = {Quote(prompt.Name)}");
            text.AppendLine($"mode = \"{ModeText(prompt.Mode)}\"");
            if (prompt.Output != PromptOutput.Replace) text.AppendLine($"output = \"{OutputText(prompt.Output)}\"");
            text.AppendLine("prompt = \"\"\"");
            text.AppendLine(EscapeMultiline(prompt.Prompt));
            text.AppendLine("\"\"\"");
        }

        return text.ToString();
    }

    public static void EnsureExists(string path)
    {
        if (File.Exists(path)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Create(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public static string ModeText(PromptMode mode) => mode switch
    {
        PromptMode.Instruction => "instruction",
        PromptMode.Universal => "universal",
        _ => "transform",
    };

    public static string OutputText(PromptOutput output) => output switch
    {
        PromptOutput.Overlay => "overlay",
        _ => "replace",
    };

    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static string EscapeMultiline(string value)
        => value.Replace("\\", "\\\\").Replace("\"\"\"", "\"\"\\\"");
}
