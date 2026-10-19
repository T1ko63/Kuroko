using Kuroko.Core.Config;
using Kuroko.Core.Hotkeys;

namespace Kuroko.Tests;

public class HotkeyGestureTests
{
    private static readonly IKeyLayout De = FakeKeyLayout.German;
    private static readonly IKeyLayout Us = FakeKeyLayout.Us;

    private const HotkeyModifiers CtrlAlt = HotkeyModifiers.Ctrl | HotkeyModifiers.Alt;

    [Theory]
    [InlineData("Ctrl+Alt+K", HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, 'K')]
    [InlineData("ctrl+shift+1", HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, '1')]
    [InlineData("Strg + Alt + p", HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, 'P')]
    [InlineData("Win+Space", HotkeyModifiers.Win, 0x20)]
    [InlineData("Alt+F4", HotkeyModifiers.Alt, 0x73)]
    public void Parses_valid_gestures(string text, HotkeyModifiers modifiers, int vk)
    {
        Assert.True(HotkeyGesture.TryParse(text, null, out var gesture, out var error), error);
        Assert.Equal(modifiers, gesture.Modifiers);
        Assert.Equal(vk, gesture.VirtualKey);
    }

    [Fact]
    public void Function_key_without_modifier_is_allowed()
    {
        Assert.True(HotkeyGesture.TryParse("F9", null, out var gesture, out _));
        Assert.Equal(HotkeyModifiers.None, gesture.Modifiers);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("K")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Ctrl+K")]
    [InlineData("Hyper+K")]
    [InlineData("Ctrl+Banana")]
    [InlineData("Ctrl+F25")]
    [InlineData("+K")]
    [InlineData("Ctrl++K")]
    [InlineData("Ctrl+++")]
    [InlineData("Num1")]
    [InlineData("VolumeUp")]
    [InlineData("0xE2")]
    [InlineData("+")]
    public void Rejects_invalid_gestures(string text)
    {
        Assert.False(HotkeyGesture.TryParse(text, De, out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void Equivalent_spellings_are_equal()
    {
        Assert.Equal(HotkeyGesture.Parse("Alt+Ctrl+k", null), HotkeyGesture.Parse("Strg+Alt+K", null));
    }

    [Fact]
    public void ToString_is_canonical_and_round_trips()
    {
        var gesture = HotkeyGesture.Parse("shift+alt+ctrl+f5", null);
        Assert.Equal("Ctrl+Alt+Shift+F5", gesture.ToString(null));
        Assert.Equal(gesture, HotkeyGesture.Parse(gesture.ToString(null), null));
    }

    // ---- characters through the keyboard layout ----

    [Theory]
    [InlineData("Ctrl+Alt+<", 0xE2)]
    [InlineData("Ctrl+Alt+^", 0xDC)]
    [InlineData("Ctrl+Alt+#", 0xBF)]
    [InlineData("Ctrl+Alt+ü", 0xBA)]
    [InlineData("Ctrl+Alt+Ü", 0xBA)] // Ü means the ü key, as K means the k key
    [InlineData("Ctrl+Alt+ö", 0xC0)]
    [InlineData("Ctrl+Alt+ß", 0xDB)]
    [InlineData("Ctrl+Alt+´", 0xDD)]
    [InlineData("Ctrl+Alt+,", 0xBC)]
    [InlineData("Ctrl+Alt+-", 0xBD)]
    [InlineData("Ctrl+Alt+.", 0xBE)]
    public void German_characters_resolve_to_their_key(string text, int vk)
    {
        Assert.True(HotkeyGesture.TryParse(text, De, out var gesture, out var error), error);
        Assert.Equal(new HotkeyGesture(CtrlAlt, vk), gesture);
    }

    [Theory]
    [InlineData("Ctrl+Alt+;", 0xBA)]
    [InlineData("Ctrl+Alt+=", 0xBB)]
    [InlineData("Ctrl+Alt+/", 0xBF)]
    [InlineData("Ctrl+Alt+`", 0xC0)]
    [InlineData("Ctrl+Alt+[", 0xDB)]
    [InlineData("Ctrl+Alt+\\", 0xDC)]
    [InlineData("Ctrl+Alt+]", 0xDD)]
    [InlineData("Ctrl+Alt+'", 0xDE)]
    public void Old_us_punctuation_names_resolve_through_the_us_layout(string text, int vk)
    {
        Assert.True(HotkeyGesture.TryParse(text, Us, out var gesture, out var error), error);
        Assert.Equal(new HotkeyGesture(CtrlAlt, vk), gesture);
    }

    [Theory]
    [InlineData("Ctrl+Alt+;", "Shift+,")]
    [InlineData("Ctrl+Alt+'", "Shift+#")]
    [InlineData("Ctrl+Alt+/", "Shift+7")]
    [InlineData("Ctrl+Alt+=", "Shift+0")]
    [InlineData("Ctrl+Alt+`", "Shift+´")]
    public void Old_us_punctuation_names_on_a_german_layout_name_the_key_they_sit_on(string text, string expected)
    {
        Assert.False(HotkeyGesture.TryParse(text, De, out _, out var error));
        Assert.Contains(expected, error);
    }

    [Theory]
    [InlineData("Ctrl+Alt+>", "Shift+<")]
    [InlineData("Ctrl+Alt+°", "Shift+^")]
    [InlineData("Ctrl+*", "Shift++")]
    [InlineData("Ctrl+Alt+|", "AltGr+<")]
    [InlineData("Ctrl+@", "AltGr+Q")]
    [InlineData("Ctrl+€", "AltGr+E")]
    public void Shift_and_AltGr_characters_are_an_error_naming_their_key(string text, string expected)
    {
        Assert.False(HotkeyGesture.TryParse(text, De, out _, out var error));
        Assert.Contains(expected, error);
        Assert.Contains("German (Germany)", error);
        Assert.Contains("docs/keybindings.md", error);
    }

    [Fact]
    public void Character_without_a_key_on_the_layout_is_an_error()
    {
        Assert.False(HotkeyGesture.TryParse("Ctrl+Alt+é", De, out _, out var error));
        Assert.Contains("No key", error);
    }

    [Fact]
    public void Character_without_a_layout_asks_for_the_code()
    {
        Assert.False(HotkeyGesture.TryParse("Ctrl+Alt+,", null, out _, out var error));
        Assert.Contains("0x", error);
    }

    // ---- '+' as the key ----

    [Theory]
    [InlineData("Ctrl+Alt++")]
    [InlineData("Ctrl + Alt + +")]
    [InlineData("Ctrl+Alt+Plus")]
    [InlineData("ctrl+alt+plus")]
    [InlineData("Ctrl+Alt+OemPlus")]
    public void Plus_key_on_german_layout(string text)
    {
        Assert.True(HotkeyGesture.TryParse(text, De, out var gesture, out var error), error);
        Assert.Equal(new HotkeyGesture(CtrlAlt, 0xBB), gesture);
    }

    [Fact]
    public void Plus_character_on_us_layout_is_shift_of_the_equals_key()
    {
        Assert.False(HotkeyGesture.TryParse("Ctrl+Alt++", Us, out _, out var error));
        Assert.Contains("Shift+=", error);
        Assert.True(HotkeyGesture.TryParse("Ctrl+Alt+Plus", Us, out var gesture, out _));
        Assert.Equal(0xBB, gesture.VirtualKey);
    }

    // ---- hex codes ----

    [Theory]
    [InlineData("Ctrl+Alt+0xE2", 0xE2)]
    [InlineData("Ctrl+Alt+0xe2", 0xE2)]
    [InlineData("Ctrl+Alt+0XDC", 0xDC)]
    [InlineData("Ctrl+0x41", 0x41)]
    [InlineData("Ctrl+0x9", 0x09)]
    [InlineData("Ctrl+0xFE", 0xFE)]
    public void Hex_codes_are_keys(string text, int vk)
    {
        Assert.True(HotkeyGesture.TryParse(text, null, out var gesture, out var error), error);
        Assert.Equal(vk, gesture.VirtualKey);
    }

    [Theory]
    [InlineData("Ctrl+0x00", "out of range")]
    [InlineData("Ctrl+0xFF", "out of range")]
    [InlineData("Ctrl+0x100", "not a hex code")]
    [InlineData("Ctrl+0xZZ", "not a hex code")]
    [InlineData("Ctrl+0x01", "mouse button")]
    [InlineData("Ctrl+0x05", "mouse button")]
    [InlineData("Ctrl+0x10", "Shift key")]
    [InlineData("Ctrl+0xA4", "Alt key")]
    [InlineData("Ctrl+0x5B", "Win key")]
    [InlineData("Ctrl+Shift", "no key")]
    [InlineData("Ctrl+Alt+Win", "no key")]
    public void Codes_that_are_not_keys_are_rejected(string text, string expected)
    {
        Assert.False(HotkeyGesture.TryParse(text, null, out _, out var error));
        Assert.Contains(expected, error);
    }

    // ---- Win32 names ----

    [Theory]
    [InlineData("Oem1", 0xBA)] [InlineData("OemPlus", 0xBB)] [InlineData("OemComma", 0xBC)] [InlineData("OemMinus", 0xBD)]
    [InlineData("OemPeriod", 0xBE)] [InlineData("Oem2", 0xBF)] [InlineData("Oem3", 0xC0)] [InlineData("Oem4", 0xDB)]
    [InlineData("Oem5", 0xDC)] [InlineData("Oem6", 0xDD)] [InlineData("Oem7", 0xDE)] [InlineData("Oem8", 0xDF)]
    [InlineData("Oem102", 0xE2)]
    [InlineData("Num0", 0x60)] [InlineData("Num9", 0x69)] [InlineData("NumMultiply", 0x6A)] [InlineData("NumAdd", 0x6B)]
    [InlineData("NumSubtract", 0x6D)] [InlineData("NumDecimal", 0x6E)] [InlineData("NumDivide", 0x6F)]
    [InlineData("PrintScreen", 0x2C)] [InlineData("Pause", 0x13)] [InlineData("ScrollLock", 0x91)]
    [InlineData("CapsLock", 0x14)] [InlineData("NumLock", 0x90)] [InlineData("Menu", 0x5D)] [InlineData("Apps", 0x5D)]
    [InlineData("VolumeMute", 0xAD)] [InlineData("VolumeDown", 0xAE)] [InlineData("VolumeUp", 0xAF)]
    [InlineData("MediaNext", 0xB0)] [InlineData("MediaPrev", 0xB1)] [InlineData("MediaStop", 0xB2)] [InlineData("MediaPlayPause", 0xB3)]
    [InlineData("BrowserBack", 0xA6)] [InlineData("BrowserHome", 0xAC)] [InlineData("LaunchMail", 0xB4)] [InlineData("LaunchApp2", 0xB7)]
    [InlineData("numpad5", 0)] // not a name: "Num5" is
    public void Win32_names(string name, int vk)
    {
        var ok = HotkeyGesture.TryParse($"Ctrl+{name}", null, out var gesture, out _);
        Assert.Equal(vk != 0, ok);
        if (ok) Assert.Equal(vk, gesture.VirtualKey);
    }

    // ---- ToString ----

    [Theory]
    [InlineData(0xE2, "Ctrl+Alt+<")]
    [InlineData(0xDC, "Ctrl+Alt+^")]
    [InlineData(0xBA, "Ctrl+Alt+ü")]
    [InlineData(0xBB, "Ctrl+Alt++")]
    [InlineData(0xDF, "Ctrl+Alt+Oem8")]   // no character on German
    [InlineData(0x6F, "Ctrl+Alt+NumDivide")] // types '/', but '/' is Shift+7
    [InlineData(0x61, "Ctrl+Alt+Num1")]
    [InlineData(0xE1, "Ctrl+Alt+0xE1")]
    [InlineData(0x0D, "Ctrl+Alt+Enter")]
    public void ToString_shows_the_german_character(int vk, string expected)
    {
        Assert.Equal(expected, new HotkeyGesture(CtrlAlt, vk).ToString(De));
    }

    [Theory]
    [InlineData(0xBA, "Ctrl+Alt+;")]
    [InlineData(0xBB, "Ctrl+Alt+=")]
    [InlineData(0xDC, "Ctrl+Alt+\\")]
    [InlineData(0xE2, "Ctrl+Alt+Oem102")] // also types '\', but '\' finds 0xDC
    public void ToString_shows_the_us_character(int vk, string expected)
    {
        Assert.Equal(expected, new HotkeyGesture(CtrlAlt, vk).ToString(Us));
    }

    [Fact]
    public void ToString_without_a_layout_uses_names()
    {
        Assert.Equal("Ctrl+Alt+Oem1", new HotkeyGesture(CtrlAlt, 0xBA).ToString(null));
        Assert.Equal("Ctrl+Alt+OemPlus", new HotkeyGesture(CtrlAlt, 0xBB).ToString(null));
    }

    public static TheoryData<string> Layouts => ["de", "us", "none"];

    [Theory]
    [MemberData(nameof(Layouts))]
    public void Every_key_code_round_trips(string which)
    {
        var layout = which switch { "de" => De, "us" => Us, _ => (IKeyLayout?)null };
        for (var vk = 0x01; vk <= 0xFE; vk++)
        {
            foreach (var modifiers in new[] { CtrlAlt, HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, HotkeyModifiers.Win })
            {
                if (!HotkeyGesture.TryParse($"{modifiers.ToString().Replace(", ", "+")}+0x{vk:X2}", layout, out var gesture, out _)) continue;
                var text = gesture.ToString(layout);
                Assert.True(HotkeyGesture.TryParse(text, layout, out var back, out var error), $"0x{vk:X2} -> '{text}': {error}");
                Assert.Equal(gesture, back);
            }
        }
    }

    // ---- AltGr warning ----

    [Theory]
    [InlineData("Ctrl+Alt+Q", "@")]
    [InlineData("Ctrl+Alt+E", "€")]
    [InlineData("Ctrl+Alt+M", "µ")]
    [InlineData("Ctrl+Alt+7", "{")]
    [InlineData("Ctrl+Alt+<", "|")]
    [InlineData("Ctrl+Alt+ß", "\\")]
    [InlineData("Ctrl+Alt+K", null)]
    [InlineData("Ctrl+Alt+Shift+Q", null)]
    [InlineData("Ctrl+Q", null)]
    [InlineData("Alt+Q", null)]
    [InlineData("Ctrl+Alt+Win+Q", null)]
    public void Ctrl_Alt_hotkeys_that_swallow_an_AltGr_character(string text, string? swallowed)
    {
        var gesture = HotkeyGesture.Parse(text, De);
        Assert.Equal(swallowed, gesture.SwallowedAltGrCharacter(De));
        Assert.Null(gesture.SwallowedAltGrCharacter(Us));
        Assert.Null(gesture.SwallowedAltGrCharacter(null));
    }

    [Fact]
    public void AltGr_warning_names_the_character_and_the_layout()
    {
        var warning = HotkeyGesture.Parse("Ctrl+Alt+Q", De).AltGrWarning(De);
        Assert.NotNull(warning);
        Assert.Contains("'@'", warning);
        Assert.Contains("AltGr+Q", warning);
        Assert.Contains("German (Germany)", warning);
    }

    [Fact]
    public void Keybinds_loader_warns_about_an_AltGr_app_hotkey_but_loads()
    {
        var result = KeybindsLoader.Parse("[app]\noverlay = \"Ctrl+Alt+Q\"\n", De);
        Assert.True(result.Ok);
        var issue = Assert.Single(result.Issues, i => i.Message.Contains("'@'"));
        Assert.False(issue.IsError);
        Assert.True(issue.Show);
        Assert.Equal(2, issue.Line);
        Assert.StartsWith("[app] overlay:", issue.Message);
    }

    [Fact]
    public void Keybinds_loader_warns_about_an_AltGr_prompt_hotkey_but_loads()
    {
        var result = KeybindsLoader.Parse("[prompts]\n\"Übersetzen\" = \"Ctrl+Alt+E\"\n", De);
        Assert.True(result.Ok);
        var issue = Assert.Single(result.Issues, i => i.Message.Contains("'€'"));
        Assert.False(issue.IsError);
        Assert.Equal(2, issue.Line);
    }

    [Fact]
    public void Keybinds_loader_reports_a_shift_character()
    {
        var result = KeybindsLoader.Parse("[prompts]\nA = \"Ctrl+Alt+>\"\n", De);
        Assert.False(result.Ok);
        Assert.Contains(result.Issues, i => i.IsError && i.Message.Contains("Shift+<") && i.Line == 2);
    }

    [Fact]
    public void Character_and_code_of_the_same_key_are_a_duplicate()
    {
        var settings = SettingsLoader.Parse(string.Empty, _ => null).Settings!;
        var keybinds = KeybindsLoader.Parse("[app]\noverlay = \"Ctrl+Alt+<\"\n\n[prompts]\nA = \"Ctrl+Alt+0xE2\"\n", De).Keybinds!;
        var issues = ConfigValidator.Validate(settings, [new Kuroko.Core.Prompts.PromptDefinition("A", "x")], keybinds, keyLayout: De);
        Assert.Contains(issues, i => i.IsError && i.Message.Contains("hotkey Ctrl+Alt+< is used twice") && i.Line == 5);
    }

    [Fact]
    public void Default_hotkeys_take_no_AltGr_character_on_german()
    {
        var defaults = new[] { KeybindSettings.DefaultOverlayHotkey, KeybindSettings.DefaultUndoHotkey, KeybindSettings.DefaultResultCopyHotkey }
            .Concat(DefaultKeybinds.PromptHotkeys.Select(p => p.Hotkey))
            .Concat(KeybindsLoader.Parse(DefaultKeybinds.Create(), De).Keybinds!.Prompts.Select(p => p.Hotkey));
        foreach (var hotkey in defaults)
        {
            Assert.Null(HotkeyGesture.Parse(hotkey, De).SwallowedAltGrCharacter(De));
        }
    }

    [Theory]
    [InlineData(0x4B, "K")]
    [InlineData(0x74, "F5")]
    [InlineData(0xE2, "<")] // the layout's character, as written in keybinds.toml
    public void A_single_key_is_shown_like_in_a_hotkey(int vk, string shown)
    {
        Assert.Equal(shown, HotkeyGesture.DisplayKey(vk, De));
    }
}
