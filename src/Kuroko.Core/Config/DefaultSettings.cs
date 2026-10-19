using System.Globalization;

namespace Kuroko.Core.Config;

/// <summary>The settings.toml written on first start.</summary>
public static class DefaultSettings
{
    /// <param name="getEnv">Used to pick the default provider whose key is already in the environment.</param>
    public static string Create(Func<string, string?> getEnv)
    {
        bool Has(string name) => !string.IsNullOrWhiteSpace(getEnv(name));
        var defaultProvider = Has("GEMINI_API_KEY") ? "gemini"
            : Has("ANTHROPIC_API_KEY") ? "anthropic"
            : Has("OPENAI_API_KEY") ? "openai"
            : "gemini";

        return string.Create(CultureInfo.InvariantCulture, $$"""
            # Kuroko - Einstellungen. Änderungen werden nach dem Speichern automatisch übernommen.
            # Bei einem Fehler in der Datei bleibt die letzte funktionierende Konfiguration aktiv und es erscheint eine Meldung.
            # Prompts stehen in prompts.toml, alle Hotkeys in keybinds.toml.

            # Rückgängig (Hotkey undo in keybinds.toml) schreibt den ursprünglichen Text zurück (mehrfach drücken = Schritt für Schritt weiter).
            # Es wird nichts überschrieben, wenn der Text im Feld inzwischen geändert wurde oder ein anderes Feld den Fokus hat.
            # So viele Ersetzungen merkt sich die App (nur im Arbeitsspeicher, nie auf der Festplatte; 0 = Rückgängig aus).
            undo_history = {{AppSettings.DefaultUndoHistory}}

            # Wo die Ergebnis-Karte erscheint: "fixed" (an result_fixed_position auf dem Monitor, auf dem das Zielfenster liegt, Standard)
            # | "follow" (dort, wo sich das Prompt-Menü öffnet, siehe overlay_position) | "caret" (am Textcursor, sonst an der Maus) | "mouse" (an der Maus).
            # Bei follow, caret und mouse wächst die Karte nach unten, bei zu wenig Platz nach oben.
            result_position = "{{AppSettings.DefaultResultPosition}}"
            # Der feste Platz: top-third | center | bottom-third (waagerecht mittig, in der Mitte dieses Drittels), top-left | bottom-left |
            # top-right | bottom-right (Ecken), left | right (an diesem Rand, senkrecht mittig), top | bottom (an diesem Rand, waagerecht mittig).
            # Die Karte wächst immer vom Bildschirmrand weg (oben nach unten, unten nach oben, in der Mitte in beide Richtungen).
            result_fixed_position = "{{AppSettings.DefaultResultFixedPosition}}"
            # Abstand der Karte zum Bildschirmrand in Pixeln, 0 bis 200.
            result_screen_margin = {{AppSettings.DefaultResultScreenMargin}}
            # Schriftgröße des Kartentexts, 8 bis 32 (auch Dezimalwerte wie 14.5).
            result_font_size = {{AppSettings.DefaultResultFontSize}}
            # Größe der Karte in Pixeln (sie wird nie größer als der Monitor). Die Breite ist fest (240 bis 1600). Die Höhe folgt dem Text,
            # ist aber mindestens result_min_height (0 bis 2000, 0 = so klein wie der Text) und höchstens result_max_height (120 bis 2000); darüber scrollt der Text.
            result_width = {{AppSettings.DefaultResultWidth}}
            result_min_height = {{AppSettings.DefaultResultMinHeight}}
            result_max_height = {{AppSettings.DefaultResultMaxHeight}}

            # Wo die Pille (Fortschritt, Hinweise, Fehler) erscheint: "follow" (dort, wo sich die Prompt-Auswahl öffnet, Standard)
            # | "caret" (am Textcursor, sonst an der Maus) | "mouse" (an der Maus) | "fixed" (an pill_fixed_position)
            # | "card" (dort, wo die Ergebnis-Karte erscheint, siehe result_position). Bei Prompts mit Ergebnis-Karte steht die Pille immer am Platz der Karte.
            pill_position = "{{AppSettings.DefaultPillPosition}}"
            # Der feste Platz: dieselben Werte wie bei result_fixed_position.
            pill_fixed_position = "{{AppSettings.DefaultPillFixedPosition}}"
            # Abstand der Pille zum Bildschirmrand in Pixeln, 0 bis 200 (bei fixed, caret und mouse).
            pill_screen_margin = {{AppSettings.DefaultPillScreenMargin}}
            # Breite der Pille in Pixeln: mindestens pill_min_width, höchstens pill_max_width (je 100 bis 1600); längerer Text bricht um.
            pill_min_width = {{AppSettings.DefaultPillMinWidth}}
            pill_max_width = {{AppSettings.DefaultPillMaxWidth}}
            # Mindesthöhe in Pixeln, 0 bis 400 (0 = so hoch wie der Text; sonst steht der Text mittig, und die Pille wächst, wenn er umbricht).
            pill_min_height = {{AppSettings.DefaultPillMinHeight}}
            # Schriftgröße der Pille, 8 bis 32.
            pill_font_size = {{AppSettings.DefaultPillFontSize}}
            # So viele Sekunden steht ein Hinweis wie „Rückgängig gemacht“ (0.5 bis 60). Fehler bleiben 5 s, die Pille nach einem gescheiterten Einfügen 15 s.
            pill_duration = {{AppSettings.DefaultPillDuration}}

            # Marker für einmalige Anweisungen im Text, z. B.:  <<kürzer und freundlicher>> Hallo ...
            # Sie wirken in Prompts mit mode = "universal" (siehe prompts.toml): ohne Marker ist der ganze Text ein Auftrag,
            # mit Marker wird dessen Inhalt auf den übrigen Text angewendet. Der Marker steht nie im Ergebnis.
            # Die Marker dürfen am Anfang, am Ende oder mitten im Text stehen. Alternativen: "^^" und "^^", oder "%%" und "%%".
            marker_start = "<<"
            marker_end = ">>"

            theme = "system"            # system | light | dark
            # Akzentfarbe für Textcursor, Textmarkierung und Fortschrittslinie (die Auswahlzeile färbt [appearance] selection): "none" (neutral, Standard) | "system" (Windows-Akzent) | Hex wie "#3B82F6".
            accent = "none"
            overlay_position = "caret"  # caret (am Textcursor, sonst Maus) | mouse | fixed (fester Platz, siehe unten)
            # Prompt-Auswahl (und die Pille bei pill_position = "follow"): fester Platz und Größe wie bei der Ergebnis-Karte. Der feste Platz (overlay_position = "fixed"):
            # top-third | center | bottom-third | top-left | bottom-left | top-right | bottom-right | left | right | top | bottom.
            # Das Menü wächst vom Bildschirmrand weg. Der Abstand zum Bildschirmrand gilt in Pixeln (0 bis 200).
            overlay_fixed_position = "{{AppSettings.DefaultOverlayFixedPosition}}"
            overlay_screen_margin = {{AppSettings.DefaultOverlayScreenMargin}}
            overlay_font_size = {{AppSettings.DefaultOverlayFontSize}}    # 8 bis 32; das ganze Menü skaliert mit
            overlay_width = {{AppSettings.DefaultOverlayWidth}}       # 240 bis 1600, nie größer als der Monitor
            overlay_min_height = {{AppSettings.DefaultOverlayMinHeight}}       # 0 bis 2000 (0 = so klein wie die Liste)
            overlay_max_height = {{AppSettings.DefaultOverlayMaxHeight}}    # 120 bis 2000: Suchzeile plus acht Zeilen; längere Listen scrollen
            language = "auto"           # auto | de | en
            autostart = false           # mit Windows starten (auch über das Tray-Menü schaltbar)

            # Nach so vielen Sekunden Ruhe gibt die App ungenutzten Arbeitsspeicher frei (0 = nie).
            # Das kostet beim nächsten Hotkey etwa 2 ms und senkt die Anzeige im Task-Manager von ca. 200 auf wenige MB.
            idle_trim_seconds = {{AppSettings.DefaultIdleTrimSeconds}}

            # Anbieter, der verwendet wird, wenn ein Prompt keinen eigenen angibt.
            default_provider = "{{defaultProvider}}"

            # Ausweich-Anbieter (leer = aus). Ist ein Anbieter nicht erreichbar (keine Verbindung, Serverfehler 5xx, Rate-Limit 429),
            # geht dieselbe Anfrage einmal an diesen Anbieter, z. B. "local" für das Ollama-Modell unten.
            # Datenschutz: Dein Text geht dann an diesen Anbieter statt an den des Prompts. Nicht nach Esc, nicht bei abgelehntem
            # Schlüssel oder abgelehnter Anfrage (4xx) und nicht nach einer Zeitüberschreitung; nie mehr als ein Schritt.
            # Lokale Anbieter nutzen ihn nicht (ihr Text bleibt auf dem Rechner), außer ihr eigener Block nennt einen.
            # Pro Anbieter geht auch fallback_provider = "name" im Block [providers.name]; das gilt dann vor diesem Wert ("" = keiner).
            fallback_provider = ""

            # Aussehen des Overlays. Fehlt ein Wert, gilt der hier gezeigte Standard. Ein ungültiger Wert wird gemeldet und durch den Standard ersetzt.
            # Gilt der Block des aktiven Themes (theme oben): [appearance.dark] oder [appearance.light].
            [appearance]
            # Grauer Text im leeren Suchfeld der Prompt-Auswahl ("" = kein Platzhalter).
            placeholder = "{{AppearanceSettings.DefaultPlaceholder}}"
            # Durchsichtigkeit der Fensterfläche in Prozent: 0 = deckend, 100 = vollständig durchsichtig. Text bleibt immer voll lesbar.
            # Über 90 leidet die Lesbarkeit (der Hintergrund scheint fast ungehindert durch).
            transparency = {{AppearanceSettings.DefaultTransparency}}
            # Eckenradius des Fensters in px (0-{{AppearanceSettings.MaxRadius}}).
            radius = {{AppearanceSettings.DefaultRadius}}
            # "acrylic" (Windows-11-Acrylic: Blur mit Rauschen und Systemtönung) | "blur" (schlichter Blur ohne Systemtönung, am stärksten durchsichtig)
            # | "mica" / "micaalt" (Windows-11-Materialien, undurchsichtig, nehmen nur die Farbe des Hintergrundbilds auf; micaalt ist dunkler/kräftiger) | "none" (kein Blur).
            # Bei acrylic, blur und mica rundet Windows die Ecken selbst (0, 4 oder 8 px); nur "none" nutzt den Radius exakt.
            # Ohne Windows-11-Unterstützung entsteht automatisch eine halbtransparente einfarbige Fläche.
            blur = "acrylic"
            # Markierung der ausgewählten Zeile: "fill" (Fläche, Standard) | "border" (nur ein Rahmen, der Text verschiebt sich nicht).
            selection_style = "fill"
            # Deckkraft der Markierung in Prozent, 0 bis 100, für beide Stile; die Zeile unter der Maus hat die halbe Deckkraft.
            selection_opacity = {{AppearanceSettings.DefaultSelectionOpacity}}
            # Breite des Rahmens bei selection_style = "border" in px, 1 bis 4.
            selection_border_width = {{AppearanceSettings.DefaultLineWidth}}
            # Feiner Rand um das Fenster: true | false, und seine Breite in px (0 bis 4; border = false heißt 0).
            # Bei acrylic, blur und mica rundet Windows die Ecken selbst; ein breiter Rand kann dort an den Ecken unsauber aussehen (außer mit custom_corners).
            border = true
            border_width = {{AppearanceSettings.DefaultLineWidth}}
            # Trennlinie unter der Eingabezeile: true | false, und ihre Höhe in px (0 bis 4).
            separator = true
            separator_width = {{AppearanceSettings.DefaultLineWidth}}
            # Schrift für Prompt-Auswahl, Pille, Ergebnis-Karte und das Fenster für die API-Schlüssel. Mehrere Namen mit Komma getrennt:
            # Es gilt der erste installierte. Ein Name, den Windows nicht kennt, wird gemeldet.
            font = "{{AppearanceSettings.DefaultFont}}"
            # true: der Radius gilt auch bei acrylic/blur/mica exakt. Braucht den Windhawk-Mod "Kuroko Blur" (Teil "Ecken", läuft in dwm.exe);
            # Windows selbst kennt dort nur 0, 4 und 8 px. Ohne Mod bleibt es bei der Rundung von Windows. false: wie bisher.
            custom_corners = false

            [appearance.dark]
            background = "#000000"     # Farbe der Fensterfläche als #RRGGBB oder rgb(r, g, b), ohne Transparenz (die steuert transparency)
            foreground = "#F0F0F0"     # Haupttext; gedämpfter Text, Trennlinie und Scrollbar werden daraus abgeleitet
            selection  = "#FFFFFF"     # Farbe der ausgewählten Zeile; ihre Deckkraft steuert selection_opacity
            # Farbe von Rand und Trennlinie als #RRGGBB oder rgb(r, g, b). Ohne Angabe werden sie aus foreground abgeleitet, z. B.:
            # border_color    = "#3D3D3D"
            # separator_color = "#2E2E2E"

            [appearance.light]
            background = "#FFFFFF"
            foreground = "#1A1A1A"
            selection  = "#000000"

            # Pro Anbieter: type, model und ein API-Schlüssel (api_key direkt ODER api_key_env = Name einer Umgebungsvariable).
            # Ohne beides gilt der Schlüssel aus der Windows-Anmeldeinformationsverwaltung (Tray-Symbol → API-Schlüssel …).
            # Modellnamen gehören nur hierher; sie sind nirgends im Programm festgelegt - bitte auf aktuelle Namen prüfen.
            # reasoning = "off" hält die Antwort schnell (kein langes Nachdenken). Weitere Werte: default | low | medium | high | xhigh | max.
            # Nur OpenAI: service_tier = standard | fast | ultrafast | flex (fast ca. 2x Preis, ultrafast teurer und nur gpt-6-astra/gpt-6.1-sol,
            # flex günstiger, langsamer und kann abgelehnt werden). Ein abgelehnter Tier wird weggelassen (Warnung im Log).
            # Weitere optionale Felder: base_url, timeout_seconds (Standard 30, bei flex 120), max_output_tokens (0 = automatisch).

            # Für kurze Umformulierungen zählt Tempo: gemini-2.5-flash antwortet gemessen in ca. 0,8 s, gemini-3.8-flash in 2-3 s.
            [providers.gemini]
            type = "gemini"
            model = "gemini-2.5-flash"
            api_key_env = "GEMINI_API_KEY"
            reasoning = "off"

            [providers.anthropic]
            type = "anthropic"
            model = "claude-haiku-4-5"
            api_key_env = "ANTHROPIC_API_KEY"
            reasoning = "off"

            [providers.openai]
            type = "openai"
            model = "gpt-5.5"
            api_key_env = "OPENAI_API_KEY"
            reasoning = "off"
            service_tier = "standard"

            # Lokale Modelle über OpenAI-kompatible Server: Ollama (Port 11434) oder LM Studio (http://localhost:1234/v1).
            [providers.local]
            type = "openai-compatible"
            base_url = "http://localhost:11434/v1"
            model = "llama3.2"
            """);
    }

    public static void EnsureExists(string path, Func<string, string?> getEnv)
    {
        if (File.Exists(path)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Create(getEnv), new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
