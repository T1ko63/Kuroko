# Keybindings

Everything about Kuroko's hotkeys: how to write a key, how to find the code of an unusual key, which hotkeys are a
good idea, what is not possible, and what every message means.

Hotkeys are set in two files in `%APPDATA%\Kuroko` (tray → **Edit settings** / **Edit prompts**):

| Setting | File | Default |
|---|---|---|
| `overlay_hotkey`: open the overlay | `settings.toml` | `Ctrl+Shift+Space` |
| `undo_hotkey`: undo the last replacement | `settings.toml` | `Ctrl+Alt+Z` |
| `result_copy_hotkey`: copy the result card | `settings.toml` | `Ctrl+Alt+C` |
| `hotkey` of a `[[prompt]]`: run that prompt directly | `prompts.toml` | `Ctrl+Alt+U` (Universal), `Ctrl+Alt+K` (Correction) |

Changes apply as soon as the file is saved. A hotkey with an error is reported with file and line, and the last
working configuration stays active.

**Contents:** [Quick start](#quick-start) · [Keys](#keys) · [Finding a key's code](#finding-a-keys-code) ·
[Modifiers and variables](#modifiers-and-variables) · [Submaps](#submaps) · [Choosing good hotkeys](#choosing-good-hotkeys) ·
[Recipes](#recipes) · [Limits](#limits) · [Messages](#messages) · [Migrating](#migrating)

## Quick start

> *To be written together with modifier variables (next update). Until then: [Keys](#keys) has everything you need.*

```toml
overlay_hotkey = "Ctrl+Shift+Space"   # modifiers + one key, joined with +
undo_hotkey = "Ctrl+Alt+ü"            # a key written as the character printed on it
```

## Keys

A hotkey is **modifiers joined with `+`, then one key**: `Ctrl+Alt+K`. Spaces around `+` are allowed, upper and lower
case do not matter (`ctrl + alt + k` is the same hotkey).

### Modifiers

| Modifier | Also written |
|---|---|
| `Ctrl` | `Control`, `Strg` |
| `Alt` | |
| `Shift` | `Umschalt` |
| `Win` | `Windows` |

At least one modifier is required, except for `F1`–`F24`, which may stand alone (`F8`). Left and right modifiers are
the same (`Ctrl` means either Ctrl key). AltGr is not a modifier of its own: Windows sends it as `Ctrl+Alt` (see
[the AltGr trap](#the-altgr-trap)).

### The key

There are four ways to write the key. They can be mixed freely; `Ctrl+Alt+<`, `Ctrl+Alt+Oem102` and `Ctrl+Alt+0xE2` are
the same hotkey on a German keyboard (and using two of them reports "used twice").

**1. Letters, digits, function keys:** `A`–`Z`, `0`–`9` (the row above the letters), `F1`–`F24`. These mean the same
key on every layout: `Z` is the key labelled Z, wherever it sits.

**2. Names:**

| Group | Names |
|---|---|
| Editing and navigation | `Space`, `Enter` (`Return`), `Tab`, `Esc` (`Escape`), `Backspace`, `Insert`, `Delete` (`Del`), `Home`, `End`, `PageUp`, `PageDown`, `Left`, `Up`, `Right`, `Down` |
| Lock and system keys | `PrintScreen`, `Pause`, `ScrollLock`, `CapsLock`, `NumLock`, `Menu` (`Apps`, the context-menu key) |
| Numeric keypad | `Num0`–`Num9`, `NumMultiply`, `NumAdd`, `NumSubtract`, `NumDecimal`, `NumDivide` |
| Layout keys | `Oem1`–`Oem8`, `Oem102`, `OemPlus` (`Plus`), `OemComma`, `OemMinus`, `OemPeriod` |
| Media and volume | `VolumeMute`, `VolumeDown`, `VolumeUp`, `MediaNext`, `MediaPrev`, `MediaStop`, `MediaPlayPause` |
| Browser and launch | `BrowserBack`, `BrowserForward`, `BrowserRefresh`, `BrowserStop`, `BrowserSearch`, `BrowserFavorites`, `BrowserHome`, `LaunchMail`, `LaunchMediaSelect`, `LaunchApp1`, `LaunchApp2` |

The layout keys are the punctuation keys; which character they type depends on the keyboard layout, see the
[tables below](#finding-a-keys-code).

**3. The character printed on the key**, as you type it without Shift: `Ctrl+Alt+ü`, `Ctrl+Alt+<`, `Ctrl+Alt+#`,
`Ctrl+Alt+^`. Kuroko asks Windows which key types that character on the active keyboard layout (like AutoHotkey does).

* Only the key's **own** character works. `>` is Shift+`<` on a German keyboard and `|` is AltGr+`<`; both are an error
  that names the key they sit on. A hotkey names a key, not a character: for Ctrl+Shift+`<` write `Ctrl+Shift+<`.
* Upper case means the same key: `Ctrl+Alt+Ü` is `Ctrl+Alt+ü`, as `Ctrl+Alt+K` is `Ctrl+Alt+k`.
* Dead keys work (`^` and `´` on German).
* **`+` as the key:** end the hotkey with `++`, e.g. `Ctrl+Alt++` (on German, where `+` has its own key), or write
  `Plus` (`Ctrl+Alt+Plus`), which is the same key on every layout (`=` on US).

**4. A key code** from `0x01` to `0xFE`: `Ctrl+Alt+0xE2`. This is the Windows *virtual-key code*. It works for every
key Windows knows, including keys that have no name above. How to find it: [next section](#finding-a-keys-code).

### Which keyboard layout counts

Characters are looked up in the keyboard layout of the window in front when the configuration is loaded (at start,
when a file is saved, and with tray → **Reload config**). The result is the *key*, so the hotkey stays on the
same key when you switch layouts later. If you write hotkeys for a different layout than the one you use, switch
first and reload, or use names and codes, which do not depend on the layout. `kuroko.log` names the layout
(`Keyboard layout: German (Germany) (0x04070407)`).

The log (`Hotkey registered: Ctrl+Alt+<`) and the tray tooltip show each hotkey with the character of the active
layout, so `Ctrl+Alt+0xE2` shows as `Ctrl+Alt+<` on German. Keys without a character show their name, others their
code.

## Finding a key's code

**Easiest:** write the character printed on the key. If it is a Shift or AltGr character, the message names the key it
sits on.

**For keys without a character** (extra keys on special keyboards, keys from other layouts), use the code from
Microsoft's list: [Virtual-Key Codes](https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes).
The punctuation keys are called `VK_OEM_1` … `VK_OEM_102` there; Kuroko accepts the code (`0xBA`) or the short name
(`Oem1`).

### German keyboard (QWERTZ)

| Key (as printed) | Shift | AltGr | Code | Name | Write |
|---|---|---|---|---|---|
| `<` | `>` | `\|` | `0xE2` | `Oem102` | `Ctrl+Alt+<` |
| `^` | `°` | | `0xDC` | `Oem5` | `Ctrl+Alt+^` |
| `ß` | `?` | `\` | `0xDB` | `Oem4` | `Ctrl+Alt+ß` |
| `´` | `` ` `` | | `0xDD` | `Oem6` | `Ctrl+Alt+´` |
| `ü` | `Ü` | | `0xBA` | `Oem1` | `Ctrl+Alt+ü` |
| `+` | `*` | `~` | `0xBB` | `OemPlus`, `Plus` | `Ctrl+Alt++` |
| `ö` | `Ö` | | `0xC0` | `Oem3` | `Ctrl+Alt+ö` |
| `ä` | `Ä` | | `0xDE` | `Oem7` | `Ctrl+Alt+ä` |
| `#` | `'` | | `0xBF` | `Oem2` | `Ctrl+Alt+#` |
| `,` | `;` | | `0xBC` | `OemComma` | `Ctrl+Alt+,` |
| `.` | `:` | | `0xBE` | `OemPeriod` | `Ctrl+Alt+.` |
| `-` | `_` | | `0xBD` | `OemMinus` | `Ctrl+Alt+-` |

### US keyboard (QWERTY)

| Key | Shift | Code | Name |
|---|---|---|---|
| `;` | `:` | `0xBA` | `Oem1` |
| `=` | `+` | `0xBB` | `OemPlus`, `Plus` |
| `,` | `<` | `0xBC` | `OemComma` |
| `-` | `_` | `0xBD` | `OemMinus` |
| `.` | `>` | `0xBE` | `OemPeriod` |
| `/` | `?` | `0xBF` | `Oem2` |
| `` ` `` | `~` | `0xC0` | `Oem3` |
| `[` | `{` | `0xDB` | `Oem4` |
| `\` | `\|` | `0xDC` | `Oem5` |
| `]` | `}` | `0xDD` | `Oem6` |
| `'` | `"` | `0xDE` | `Oem7` |

On a US keyboard with the extra key next to left Shift (102-key), that key is `0xE2` (`Oem102`).

## Modifiers and variables

> *Coming with the next update: your own names for modifier combinations, so a set of hotkeys can share them.*

## Submaps

> *Coming in a later update: key sequences (press a leader hotkey, then a second key).*

## Choosing good hotkeys

A global hotkey is taken away from **every** other app while Kuroko runs: the app in front never sees it. So pick
combinations that nothing else needs.

### The AltGr trap

On layouts with an AltGr key (German, French, Spanish, Polish, …), Windows treats `Ctrl+Alt` exactly like AltGr. A
hotkey `Ctrl+Alt+Q` therefore also catches AltGr+Q, and on German that is how `@` is typed: while Kuroko runs, AltGr+Q
no longer types `@`. Kuroko warns about this when it loads the configuration (the hotkey still works).

On German, these `Ctrl+Alt` hotkeys take away a character:

| Hotkey | Character lost |
|---|---|
| `Ctrl+Alt+Q` | `@` |
| `Ctrl+Alt+E` | `€` |
| `Ctrl+Alt+M` | `µ` |
| `Ctrl+Alt+2`, `Ctrl+Alt+3` | `²`, `³` |
| `Ctrl+Alt+7`, `8`, `9`, `0` | `{`, `[`, `]`, `}` |
| `Ctrl+Alt+ß` | `\` |
| `Ctrl+Alt++` | `~` |
| `Ctrl+Alt+<` | `\|` |

The default hotkeys avoid them. Older versions gave the Universal prompt `Ctrl+Alt+M` (`µ`); it is now `Ctrl+Alt+U`.
A `prompts.toml` created by an older version keeps `Ctrl+Alt+M` and shows the warning; change it there if you type
`µ` with AltGr+M.

Ways around it: another letter (`Ctrl+Alt+K`, `Ctrl+Alt+Z` are free on German), `Ctrl+Shift+…`, `Alt+Shift+…`,
`Ctrl+Alt+Shift+…` or `Win+Alt+…`.

### App shortcuts

* Avoid shortcuts you use in your apps: `Ctrl+Shift+T` (reopen tab), `Ctrl+Shift+N` (new window), `Ctrl+Shift+V`
  (paste as text), `Ctrl+Alt+Delete`, and the `Win+letter` shortcuts of Windows (many cannot be taken at all, e.g.
  `Win+L`).
* Plain `Ctrl+A`, `Ctrl+C` and `Ctrl+V` cannot be `result_copy_hotkey`: Kuroko sends those keys itself to read and
  replace text.
* `F12` is reserved by Windows for debuggers and may fail to register.
* If another app already owns a combination, Kuroko says so at start: "Hotkey … is already used by another app".

## Recipes

> *To be written together with modifier variables and submaps.*

## Limits

Kuroko registers hotkeys with Windows (`RegisterHotKey`); it does not install a keyboard hook. That keeps it light and
needs no special rights, but some things are not possible:

* **Fn key:** handled inside the keyboard, Windows never sees it. `Fn+F5` arrives as whatever the keyboard makes of it
  (often a media key: use that name, e.g. `VolumeUp`).
* **Mouse buttons:** not possible, also not as codes `0x01`–`0x06`.
* **Other keys as modifiers** (e.g. `Tab+K`, `CapsLock+K`): not possible; modifiers are Ctrl, Alt, Shift and Win only.
* **A modifier alone** (e.g. double-tap Shift, `Ctrl` by itself, codes `0x10`–`0x12`, `0x5B`, `0x5C`, `0xA0`–`0xA5`):
  not possible.
* **Left vs. right modifier** (`RCtrl+K`): not possible, both sides count.
* **Keys Windows keeps for itself** (`Ctrl+Alt+Delete`, `Win+L`) cannot be taken.
* **Numpad with NumLock off:** the keypad sends the navigation keys (`Num1` becomes `End`), so a `Num1` hotkey only
  fires with NumLock on.
* **`Ctrl+Pause`:** Windows turns it into Break (code `0x03`); write `Ctrl+0x03` if `Ctrl+Pause` does not fire.

## Messages

Messages appear in a pill and the tray, with file and line, e.g. `settings.toml, line 12: undo_hotkey: …` or
`prompts.toml, line 30: prompt 'Correction': …`. Errors keep the last working configuration active; warnings do not
block anything.

| Message | Meaning | Fix |
|---|---|---|
| Hotkey is empty. | A hotkey setting in `settings.toml` is `""`. | Write a hotkey, or remove the line to get the default. (In `prompts.toml`, an empty `hotkey` just means "no hotkey".) |
| Hotkey '…' is malformed. | A `+` without anything next to it (`Ctrl+`, `+K`, `Ctrl++K`). | Write modifiers, `+`, one key. For the `+` key: `Ctrl+Alt++` or `Ctrl+Alt+Plus`. |
| Unknown modifier '…' in hotkey '…'. | Something before the last `+` is not Ctrl, Alt, Shift or Win. | Only those four (and their other spellings) can be modifiers, see [Limits](#limits). |
| Modifier '…' appears twice in hotkey '…'. | `Ctrl+Strg+K` and the like. | Write each modifier once. |
| Hotkey '…' has no key: '…' is a modifier, and a modifier alone cannot be a hotkey. | The last part is a modifier (`Ctrl+Shift`). | Add a key: `Ctrl+Shift+K`. |
| Unknown key '…' in hotkey '…'. | Not a letter, digit, F-key, name, code or single character. | Check the spelling against [Keys](#keys), or use the [code](#finding-a-keys-code). |
| Hotkey '…' needs at least one modifier (Ctrl, Alt, Shift or Win). | A key alone; only F1–F24 may stand alone. | Add a modifier. |
| Key code '…' in hotkey '…' is not a hex code like 0xE2. | `0x` followed by something that is not one or two hex digits. | Write `0x` and two hex digits, e.g. `0xE2`. |
| Key code '…' in hotkey '…' is out of range: key codes go from 0x01 to 0xFE. | `0x00` or `0xFF`. | Look the code up in [Microsoft's list](https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes). |
| Key code '…' in hotkey '…' is the left mouse button: only keyboard keys can be hotkeys. | Codes `0x01`, `0x02`, `0x04`–`0x06` are mouse buttons (the message names which). | Mouse buttons are not possible, see [Limits](#limits). |
| Key code '…' in hotkey '…' is the Shift key: a modifier alone cannot be a hotkey. | Codes of Shift, Ctrl, Alt or Win (the message names which). | Use the modifier as a modifier and add a key. |
| '…' in hotkey '…' cannot be resolved: no keyboard layout is available. | A character as key, but Windows gave no layout (only in tests and unusual setups). | Use the name or code of the key. |
| No key on the active keyboard layout (…) types '…' (hotkey '…'). | The character is not on any key of this layout, or only through a dead key (`é`). | Write the character of the key itself, or its name or code. If the hotkey was written for another layout, switch to it and reload. |
| '…' in hotkey '…' is Shift+… / AltGr+… on the active keyboard layout (…). A hotkey names the key, not the character: … | You wrote a Shift or AltGr character (`>`, `\|`, `°`, `@`). | Write the key's own character as the message shows, and add `Shift` yourself if you want it: `Ctrl+Shift+<`. |
| *(warning)* … is also AltGr+…, which types '…' on the active keyboard layout (…); while Kuroko runs, '…' cannot be typed with AltGr. | A `Ctrl+Alt` hotkey takes away an AltGr character, see [the AltGr trap](#the-altgr-trap). | Choose another key or other modifiers. If you never type that character, ignore it. |
| hotkey … is used twice: … and … | Two settings or prompts have the same hotkey (also when written differently, e.g. `<` and `0xE2`). | Give one of them another hotkey. |
| result_copy_hotkey: … is used by the app to read the selection (Ctrl+C, Ctrl+A, Ctrl+V); choose another hotkey | See [App shortcuts](#app-shortcuts). | Choose another hotkey, e.g. the default `Ctrl+Alt+C`. |
| Hotkey … (…) is already used by another app. | Shown at start or reload: Windows refused the hotkey because another program registered it first. | Choose another hotkey, or quit the other program. |

## Migrating

Older versions knew a few punctuation keys by their **US** character: `;` `=` `,` `-` `.` `/` `` ` `` `[` `\` `]` `'`.
These characters now go through the active keyboard layout like every other character, so they mean the key that
types them on *your* keyboard.

* **On a US layout nothing changes.**
* **On other layouts** a hotkey with one of these characters now means a different key, or is an error because the
  character needs Shift or AltGr there. The old meaning is kept by writing the code or the `Oem` name.

German keyboard, old hotkey → the key it used to mean → how to write that key now:

| Old | Key it meant (code) | Now on German | Write instead |
|---|---|---|---|
| `Ctrl+Alt+;` | `ü` (`0xBA`) | error: `;` is Shift+`,` | `Ctrl+Alt+ü` |
| `Ctrl+Alt+=` | `+` (`0xBB`) | error: `=` is Shift+`0` | `Ctrl+Alt++` or `Ctrl+Alt+Plus` |
| `Ctrl+Alt+,` | `,` (`0xBC`) | unchanged | `Ctrl+Alt+,` |
| `Ctrl+Alt+-` | `-` (`0xBD`) | unchanged | `Ctrl+Alt+-` |
| `Ctrl+Alt+.` | `.` (`0xBE`) | unchanged | `Ctrl+Alt+.` |
| `Ctrl+Alt+/` | `#` (`0xBF`) | error: `/` is Shift+`7` | `Ctrl+Alt+#` |
| ``Ctrl+Alt+` `` | `ö` (`0xC0`) | error: `` ` `` is Shift+`´` | `Ctrl+Alt+ö` |
| `Ctrl+Alt+[` | `ß` (`0xDB`) | error: `[` is AltGr+`8` | `Ctrl+Alt+ß` |
| `Ctrl+Alt+\` | `^` (`0xDC`) | error: `\` is AltGr+`ß` | `Ctrl+Alt+^` |
| `Ctrl+Alt+]` | `´` (`0xDD`) | error: `]` is AltGr+`9` | `Ctrl+Alt+´` |
| `Ctrl+Alt+'` | `ä` (`0xDE`) | error: `'` is Shift+`#` | `Ctrl+Alt+ä` |

Nothing is changed in your files automatically: an affected hotkey is reported as an error with file and line,
and the message names the key to write.
