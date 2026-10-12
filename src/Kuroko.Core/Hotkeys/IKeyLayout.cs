namespace Kuroko.Core.Hotkeys;

/// <summary>Which modifiers a keyboard layout needs to type a character on a key.</summary>
public enum KeyLayer
{
    /// <summary>The key alone.</summary>
    Base,
    /// <summary>Shift + key.</summary>
    Shift,
    /// <summary>AltGr (= Ctrl+Alt) + key.</summary>
    AltGr,
    /// <summary>Any other combination (e.g. Shift+AltGr, Ctrl).</summary>
    Other,
}

/// <summary>
/// A keyboard layout: which key types which character. Lets <see cref="HotkeyGesture"/> accept keys as the character
/// printed on them ("Ctrl+Alt+ü", "Ctrl+Alt+&lt;") without OS calls in Core. Win32KeyLayout (Kuroko.Platform) asks Windows.
/// </summary>
public interface IKeyLayout
{
    /// <summary>Readable name for messages, e.g. "German (Germany)".</summary>
    string Name { get; }

    /// <summary>Finds the key that types <paramref name="character"/> and the layer it sits on; false if no key types it.</summary>
    bool TryFindKey(char character, out int virtualKey, out KeyLayer layer);

    /// <summary>The text a key types on <paramref name="layer"/> (Base or AltGr), dead keys included; null if it types none.</summary>
    string? CharOf(int virtualKey, KeyLayer layer);
}

/// <summary>The keyboard layout hotkeys are resolved against when the caller passes none.</summary>
public static class KeyLayouts
{
    private static volatile IKeyLayout? _current;

    /// <summary>
    /// Set by the app to the active Windows layout before each config load, so a load and everything parsed from it
    /// (registration, log, tray tooltip) see the same layout. Null: characters other than A-Z and 0-9 cannot be resolved.
    /// </summary>
    public static IKeyLayout? Current
    {
        get => _current;
        set => _current = value;
    }
}
