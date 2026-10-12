using Kuroko.Core.Hotkeys;
using Kuroko.Platform.Hotkeys;

namespace Kuroko.Windows.Tests;

/// <summary>The real layout of the machine running the tests (US on CI, German on a German PC); only layout-independent facts are checked.</summary>
public class Win32KeyLayoutTests
{
    private readonly Win32KeyLayout _layout = Win32KeyLayout.Active();

    [Fact]
    public void Has_a_name_and_a_handle()
    {
        Assert.False(string.IsNullOrWhiteSpace(_layout.Name));
        Assert.Matches("^0x[0-9A-F]{8}$", _layout.Handle);
    }

    [Fact]
    public void The_character_of_a_key_finds_the_same_key()
    {
        var a = _layout.CharOf(0x41, KeyLayer.Base);
        Assert.NotNull(a);
        Assert.True(_layout.TryFindKey(a![0], out var vk, out var layer));
        Assert.Equal(0x41, vk);
        Assert.Equal(KeyLayer.Base, layer);
    }

    [Fact]
    public void Unknown_characters_and_keys_without_text_are_reported_as_such()
    {
        Assert.False(_layout.TryFindKey('☃', out _, out _)); // snowman: on no keyboard
        Assert.Null(_layout.CharOf(0x70, KeyLayer.Base)); // F1 types nothing
    }

    [Fact]
    public void Every_oem_key_round_trips_through_ToString()
    {
        foreach (var vk in new[] { 0xBA, 0xBB, 0xBC, 0xBD, 0xBE, 0xBF, 0xC0, 0xDB, 0xDC, 0xDD, 0xDE, 0xDF, 0xE2 })
        {
            var gesture = new HotkeyGesture(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, vk);
            var text = gesture.ToString(_layout);
            Assert.True(HotkeyGesture.TryParse(text, _layout, out var back, out var error), $"0x{vk:X2} -> '{text}': {error}");
            Assert.Equal(gesture, back);
        }
    }
}
