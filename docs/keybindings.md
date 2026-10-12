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

A hotkey is modifiers and one key, joined with `+`. Write it in `settings.toml` (the app's own hotkeys) or as `hotkey`
of a prompt in `prompts.toml`:

```toml
# settings.toml
overlay_hotkey = "Ctrl+Shift+Space"   # modifiers + one key
undo_hotkey = "Ctrl+Alt+ü"            # a key written as the character printed on it
```

```toml
# prompts.toml
[[prompt]]
name = "Correction"
hotkey = "Ctrl+Alt+K"
prompt = "..."
```

Once you have more than a few prompt hotkeys, give your modifier combinations a name in `[mods]` and use it as
`$name`. Changing one line then moves a whole group of hotkeys:

```toml
# settings.toml
[mods]
replace = "Shift+Alt"   # prompts that replace the text
card = "Ctrl+Alt"       # prompts that show a card
```

```toml
# prompts.toml
[[prompt]]
name = "Correction"
hotkey = "$replace+C"   # Shift+Alt+C
prompt = "..."

[[prompt]]
name = "Summary"
hotkey = "$card+S"      # Ctrl+Alt+S
output = "overlay"
prompt = "..."
```

Save the file and the hotkeys apply at once. If something is wrong, a message names the file and line; the
[Messages](#messages) table says what it means. More in [Modifiers and variables](#modifiers-and-variables) and
[Recipes](#recipes).

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
[the AltGr trap](#the-altgr-trap)). Modifiers can also come from a variable: `$card+S`, see
[Modifiers and variables](#modifiers-and-variables).

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

A **modifier variable** is a name for a modifier combination. Define it once in the `[mods]` table of
`settings.toml` and write `$name` in any hotkey instead of the modifiers:

```toml
# settings.toml
[mods]
main = "Ctrl"
card = "Ctrl+Alt"
```

```toml
# settings.toml, above the first [table]
overlay_hotkey = "$main+Shift+Space"   # Ctrl+Shift+Space
```

```toml
# prompts.toml
hotkey = "$card+S"                     # Ctrl+Alt+S
```

### How it works

* Before a hotkey is read, every `$name` is replaced by its modifiers. `$card+S` then is exactly `Ctrl+Alt+S`, with
  all the rules of [Keys](#keys).
* Variables work in **every** hotkey: `overlay_hotkey`, `undo_hotkey`, `result_copy_hotkey` and the `hotkey` of each
  prompt.
* **Names:** letters, digits and `_` (`main`, `card_2`), not case-sensitive (`$Main` is `$main`), like the modifier
  names.
* **Values:** modifiers only (`Ctrl`, `Alt`, `Shift`, `Win` and their other spellings), at least one, joined with `+`.
  A value cannot contain a key (`"Ctrl+K"`) or another variable (`"$main+Alt"`): variables are not nested.
* **Saving `[mods]`** applies at once and registers all hotkeys again, also the prompt hotkeys whose `prompts.toml`
  did not change. If a prompt still uses a variable you removed or renamed, the new `settings.toml` is rejected with an
  error that names the prompt, and the last working version stays active.
* The log and the tray tooltip show hotkeys with the variables filled in (`Ctrl+Alt+S`). The prompt list in the
  overlay shows the hotkey as written in `prompts.toml` (`$card+S`).

### Main modifier and sub-modifiers

Like `$mainMod` in Hyprland: one variable is your **main** modifier, and each group of hotkeys adds one more
modifier to it. Change `main` and everything moves along.

```toml
[mods]
main = "Ctrl"
```

```toml
hotkey = "$main+Alt+K"     # Ctrl+Alt+K
hotkey = "$main+Shift+K"   # Ctrl+Shift+K
```

**Combining** is free: a variable plus fixed modifiers (`$main+Alt+K`), or two variables (`$main+$extra+K`), as
long as no modifier appears twice after filling in. `$card+Ctrl+K` with `card = "Ctrl+Alt"` is an error
("Modifier 'Ctrl' appears twice"), and the message shows both what you wrote and what it became.

### Things to keep in mind

* **Ctrl alone takes app shortcuts.** With `main = "Ctrl"`, `$main+S` is `Ctrl+S`, and no app can save any more while
  Kuroko runs. Use `main` only together with a second modifier (`$main+Alt+…`, `$main+Shift+…`), or make `main` a
  pair from the start (`main = "Ctrl+Shift"`).
* **Ctrl+Alt is AltGr.** A variable such as `card = "Ctrl+Alt"` runs into [the AltGr trap](#the-altgr-trap) for the
  keys that type something with AltGr (`$card+Q` takes `@` on German). Kuroko warns about each such hotkey.
* **Win as main** is mostly taken by Windows itself: `Win+E`, `Win+D`, `Win+L`, `Win+V`, `Win+.` and many more. Some
  cannot be registered at all, others stop working in Windows. `Win+Alt` and `Win+Shift` are freer, but not
  completely (see the [Win recipe](#win-as-main)).
* **Only Ctrl, Alt, Shift and Win are modifiers.** A variable cannot make `Tab` or `CapsLock` a modifier
  (see [Limits](#limits)).

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

### By output: replace or card

One modifier pair for prompts that replace the text, another for prompts that show a card. The letter says what the
prompt does; the modifiers say where the result goes. This is the scheme Kuroko's author uses.

```toml
# settings.toml
[mods]
replace = "Shift+Alt"   # the result replaces the text
card = "Ctrl+Alt"       # the result appears in a card
```

```toml
# prompts.toml
[[prompt]]
name = "Correct"
hotkey = "$replace+C"   # Alt+Shift+C
prompt = "..."

[[prompt]]
name = "Shorten"
hotkey = "$replace+S"   # Alt+Shift+S
prompt = "..."

[[prompt]]
name = "Summarize"
hotkey = "$card+S"      # Ctrl+Alt+S
output = "overlay"
prompt = "..."

[[prompt]]
name = "Fact check"
hotkey = "$card+F"      # Ctrl+Alt+F
output = "overlay"
prompt = "..."
```

`S` appears in both groups (shorten, summarize) without a clash, because the modifiers differ. On German, avoid
`$card+Q`, `E`, `M`, `2`, `3`, `7`–`0`, `ß`, `+` and `<` ([the AltGr trap](#the-altgr-trap)).

### Ctrl as main

```toml
# settings.toml: general settings stay above the first [table]
overlay_hotkey = "$main+Shift+Space"   # Ctrl+Shift+Space
undo_hotkey = "$main+Alt+Z"            # Ctrl+Alt+Z
result_copy_hotkey = "$main+Alt+C"     # Ctrl+Alt+C

[mods]
main = "Ctrl"
```

```toml
# prompts.toml
hotkey = "$main+Alt+K"     # Correction: Ctrl+Alt+K
hotkey = "$main+Shift+1"   # a second group: Ctrl+Shift+1, 2, 3 …
```

Always add a second modifier to `$main`; `$main+K` alone would be `Ctrl+K` and steal an app shortcut.
`Ctrl+Shift+<digit>` is rarely used by apps, which makes it a good group for many prompts.

### Win as main

```toml
# settings.toml: general settings stay above the first [table]
overlay_hotkey = "$main+Space"   # Win+Alt+Space
undo_hotkey = "$main+Z"          # Win+Alt+Z
result_copy_hotkey = "$main+C"   # Win+Alt+C

[mods]
main = "Win+Alt"
```

```toml
# prompts.toml
hotkey = "$main+J"   # Win+Alt+J
```

`Win+Alt` keeps all Ctrl and Alt shortcuts of your apps free, and there is no AltGr trap. Windows itself uses a few
`Win+Alt` combinations, so avoid them: `Win+Alt+D` (date and time), `Win+Alt+K` (microphone in calls), `Win+Alt+R`,
`Win+Alt+G`, `Win+Alt+PrintScreen` (Game Bar), `Win+Alt+B` (HDR), `Win+Alt+Up/Down` (snap), and
`Win+Alt+<digit>` (jump list of a taskbar app), so use letters for prompts. Plain `Win+<letter>` is
mostly taken by Windows; do not use `main = "Win"` alone.

> *Recipes with submaps follow in a later update.*

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
| hotkey … is used twice: … and … | Two settings or prompts have the same hotkey (also when written differently, e.g. `<` and `0xE2`, or `$card+S` and `Ctrl+Alt+S`). | Give one of them another hotkey. |
| Unknown variable '$…' in hotkey '…'. Defined in [mods] of settings.toml: … | The hotkey uses a `$name` that `[mods]` does not define (typo, or removed from `[mods]`). | Fix the name, or add it to `[mods]`. If you just removed it from `[mods]`, the old `settings.toml` stays active until no prompt uses it any more. |
| Unknown variable '$…' in hotkey '…'. No variables are defined: add them to a [mods] table in settings.toml. | The hotkey uses `$name`, but `settings.toml` has no `[mods]` (or it is empty). | Add `[mods]` with that name, see [Modifiers and variables](#modifiers-and-variables). |
| '$' in hotkey '…' must be followed by a variable name (letters, digits, _). | A `$` with no name after it (`$+K`). | Write the variable's name right after `$`: `$card+K`. |
| *any message above* … (written as '…', which is '…' with [mods] filled in). | The hotkey used variables; the message talks about the filled-in text and also shows what you wrote. | Fix what the message says, in the hotkey or in `[mods]`. Most often a modifier is twice: `$card+Ctrl+K` with `card = "Ctrl+Alt"`. |
| mods must be a table: [mods] with lines like main = "Ctrl" | `mods = …` was written as a single value. | Write a `[mods]` table, one variable per line. |
| [mods] name '…' is not allowed: use letters, digits and _ | A variable name with other characters (`"my-mod"`). | Rename it, e.g. `my_mod`. |
| [mods] '…' is defined twice (names are not case-sensitive) | Two names that differ only in case (`main`, `Main`). | Keep one. |
| [mods] … must be text such as "Ctrl+Alt" | The value is a number or a list. | Put the modifiers in quotes: `main = "Ctrl"`. |
| [mods] … = "…" is not a modifier combination like "Ctrl+Alt" | Empty value, or a `+` with nothing next to it. | Write one or more modifiers joined with `+`. |
| [mods] … = "…": '…' is not a modifier (Ctrl, Alt, Shift or Win); a variable holds modifiers only, the key comes in the hotkey | The value contains a key (`"Ctrl+K"`) or a misspelled modifier. | Keep only the modifiers in `[mods]` and write the key in the hotkey: `"$main+K"`. |
| [mods] … = "…" uses the variable '$…'; a variable holds modifiers only, not other variables | A variable refers to another one (`"$main+Alt"`). | Write the modifiers out: `"Ctrl+Alt"`. |
| [mods] … = "…": modifier '…' appears twice | `"Ctrl+Strg"` and the like. | Write each modifier once. |
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
