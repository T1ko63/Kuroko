using Kuroko.Core.Hotkeys;

namespace Kuroko.Tests;

/// <summary>A keyboard layout from a table, as Windows reports it (VkKeyScanEx / ToUnicodeEx); German and US for the tests.</summary>
internal sealed class FakeKeyLayout : IKeyLayout
{
    private readonly List<(char Character, int Key, KeyLayer Layer)> _keys = [];

    private FakeKeyLayout(string name, IEnumerable<(char, int, KeyLayer)> keys)
    {
        Name = name;
        for (var c = 'a'; c <= 'z'; c++)
        {
            _keys.Add((c, char.ToUpperInvariant(c), KeyLayer.Base));
            _keys.Add((char.ToUpperInvariant(c), char.ToUpperInvariant(c), KeyLayer.Shift));
        }

        for (var c = '0'; c <= '9'; c++) _keys.Add((c, c, KeyLayer.Base));
        _keys.AddRange(keys);
    }

    public string Name { get; }

    public bool TryFindKey(char character, out int virtualKey, out KeyLayer layer)
    {
        foreach (var (c, key, l) in _keys)
        {
            if (c != character) continue;
            virtualKey = key;
            layer = l;
            return true;
        }

        virtualKey = 0;
        layer = KeyLayer.Other;
        return false;
    }

    public string? CharOf(int virtualKey, KeyLayer layer)
    {
        foreach (var (c, key, l) in _keys)
        {
            if (key == virtualKey && l == layer) return c.ToString();
        }

        return null;
    }

    private static (char, int, KeyLayer) Base(char c, int key) => (c, key, KeyLayer.Base);
    private static (char, int, KeyLayer) Shift(char c, int key) => (c, key, KeyLayer.Shift);
    private static (char, int, KeyLayer) AltGr(char c, int key) => (c, key, KeyLayer.AltGr);

    /// <summary>German (QWERTZ), as measured with VkKeyScanEx and ToUnicodeEx on HKL 0x04070407.</summary>
    public static FakeKeyLayout German { get; } = new("German (Germany)",
    [
        Base('<', 0xE2), Shift('>', 0xE2), AltGr('|', 0xE2),
        Base('^', 0xDC), Shift('°', 0xDC),
        Base('#', 0xBF), Shift('\'', 0xBF),
        Base('+', 0xBB), Shift('*', 0xBB), AltGr('~', 0xBB),
        Base(',', 0xBC), Shift(';', 0xBC),
        Base('.', 0xBE), Shift(':', 0xBE),
        Base('-', 0xBD), Shift('_', 0xBD),
        Base('ü', 0xBA), Shift('Ü', 0xBA),
        Base('ö', 0xC0), Shift('Ö', 0xC0),
        Base('ä', 0xDE), Shift('Ä', 0xDE),
        Base('ß', 0xDB), Shift('?', 0xDB), AltGr('\\', 0xDB),
        Base('´', 0xDD), Shift('`', 0xDD),
        Shift('!', '1'), Shift('"', '2'), Shift('§', '3'), Shift('$', '4'), Shift('%', '5'),
        Shift('&', '6'), Shift('/', '7'), Shift('(', '8'), Shift(')', '9'), Shift('=', '0'),
        AltGr('@', 'Q'), AltGr('€', 'E'), AltGr('µ', 'M'), AltGr('²', '2'), AltGr('³', '3'),
        AltGr('{', '7'), AltGr('[', '8'), AltGr(']', '9'), AltGr('}', '0'),
        Base('/', 0x6F), Base('*', 0x6A), Base('0', 0x60), // numpad: found later than the main keys, as with VkKeyScanEx
    ]);

    /// <summary>US English (QWERTY); 0xE2 also types a backslash, but VkKeyScanEx('\') finds 0xDC.</summary>
    public static FakeKeyLayout Us { get; } = new("English (United States)",
    [
        Base(';', 0xBA), Shift(':', 0xBA),
        Base('=', 0xBB), Shift('+', 0xBB),
        Base(',', 0xBC), Shift('<', 0xBC),
        Base('-', 0xBD), Shift('_', 0xBD),
        Base('.', 0xBE), Shift('>', 0xBE),
        Base('/', 0xBF), Shift('?', 0xBF),
        Base('`', 0xC0), Shift('~', 0xC0),
        Base('[', 0xDB), Shift('{', 0xDB),
        Base('\\', 0xDC), Shift('|', 0xDC),
        Base(']', 0xDD), Shift('}', 0xDD),
        Base('\'', 0xDE), Shift('"', 0xDE),
        Base('\\', 0xE2), Shift('|', 0xE2),
        Shift('!', '1'), Shift('@', '2'), Shift('#', '3'), Shift('$', '4'), Shift('%', '5'),
        Shift('^', '6'), Shift('&', '7'), Shift('*', '8'), Shift('(', '9'), Shift(')', '0'),
    ]);
}
