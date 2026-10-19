# Keybindings

Everything about Kuroko's hotkeys: how to write a key, how to find the code of an unusual key, which hotkeys are a
good idea, what is not possible, and what every message means.

**Contents:** [Where keybinds live](#where-keybinds-live) · [Quick start](#quick-start) · [Keys](#keys) ·
[Finding a key's code](#finding-a-keys-code) · [Modifiers and variables](#modifiers-and-variables) · [Submaps](#submaps) ·
[Choosing good hotkeys](#choosing-good-hotkeys) · [Recipes](#recipes) · [Limits](#limits) · [Messages](#messages) ·
[Migrating](#migrating)

## Where keybinds live

Every hotkey is in one file, `%APPDATA%\Kuroko\keybinds.toml` (tray → **Edit keybinds**, or `/keybinds` in the overlay),
and nowhere else. One rule without exceptions:

* `settings.toml` says how Kuroko behaves (theme, providers, overlay, markers …). No keys.
* `prompts.toml` says what the prompts do. No `hotkey`.
* `keybinds.toml` says which key does what.

| What | Where in `keybinds.toml` | Default |
|---|---|---|
| Open the overlay | `overlay` in `[app]` | `Ctrl+Shift+Space` |
| Undo the last replacement (while `undo_history` in `settings.toml` is above 0) | `undo` in `[app]` | `Ctrl+Alt+Z` |
| Copy the result card | `result_copy` in `[app]` | `Ctrl+Alt+C` |
| Run a prompt directly | `[prompts]`: prompt name = hotkey | `Universal = "Ctrl+Alt+U"`, `Correction = "Ctrl+Alt+K"` |
| Open a [submap](#submaps) of plain keys | `open` of a `[submaps.<name>]` | none |
| Name a modifier combination | `[mods]` | none |

Changes apply as soon as the file is saved. A hotkey with an error is reported with file and line, and the last
working keybinds stay active. If `keybinds.toml` is missing at start, Kuroko creates it: from the hotkeys of an older
`settings.toml` and `prompts.toml` if there are any (see [Migrating](#from-settingstoml-and-promptstoml)), otherwise with the
defaults above. A commented template with every key is [`config/keybinds.example.toml`](../config/keybinds.example.toml).

Whether a prompt replaces the text or shows a card is its `output` in `prompts.toml`, not its hotkey: `[mods]` only gives
modifier combinations a name and decides nothing. Calling one of them `card` is just a convention.

## Quick start

A hotkey is modifiers and one key, joined with `+`. The app's own hotkeys go in `[app]`, a prompt's hotkey in `[prompts]`
under the prompt's name from `prompts.toml`:

```toml
# keybinds.toml
[app]
overlay = "Ctrl+Shift+Space"   # modifiers + one key
undo = "Ctrl+Alt+ü"            # a key written as the character printed on it

[prompts]
Correction = "Ctrl+Alt+K"
"Übersetzen DE↔EN" = "Ctrl+Alt+T"   # a name with spaces or other characters than A-Z, 0-9, _ and - is quoted
```

Prompt names are not case-sensitive. A name that `prompts.toml` does not have is an error that names the line, so renaming
a prompt is noticed at once. `""` means no hotkey.

Once you have more than a few prompt hotkeys, give your modifier combinations a name in `[mods]` and use it as
`$name`. Changing one line then moves a whole group of hotkeys:

```toml
# keybinds.toml
[mods]
replace = "Shift+Alt"   # prompts that replace the text
card = "Ctrl+Alt"       # prompts that show a card

[prompts]
Correction = "$replace+C"   # Shift+Alt+C
Summary = "$card+S"         # Ctrl+Alt+S
```

For many prompts, a [submap](#submaps) needs only one hotkey: press it, then a plain key (`P`, `L`, …) picks the
prompt.

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
`keybinds.toml` and write `$name` in any hotkey instead of the modifiers:

```toml
# keybinds.toml
[mods]
main = "Ctrl"
card = "Ctrl+Alt"

[app]
overlay = "$main+Shift+Space"   # Ctrl+Shift+Space

[prompts]
Summary = "$card+S"             # Ctrl+Alt+S
```

### How it works

* Before a hotkey is read, every `$name` is replaced by its modifiers. `$card+S` then is exactly `Ctrl+Alt+S`, with
  all the rules of [Keys](#keys).
* Variables work in **every** hotkey of `keybinds.toml`: `[app]`, `[prompts]` and the `open` hotkey of each submap.
* **Names:** letters, digits and `_` (`main`, `card_2`), not case-sensitive (`$Main` is `$main`), like the modifier
  names.
* **Values:** modifiers only (`Ctrl`, `Alt`, `Shift`, `Win` and their other spellings), at least one, joined with `+`.
  A value cannot contain a key (`"Ctrl+K"`) or another variable (`"$main+Alt"`): variables are not nested.
* **Saving `[mods]`** applies at once and registers all hotkeys again. If a hotkey still uses a variable you removed or
  renamed, the new `keybinds.toml` is rejected with an error on that hotkey's line, and the last working version stays
  active.
* The log, the tray tooltip and the prompt list in the overlay show hotkeys with the variables filled in and in one
  fixed spelling: `$card+s` appears as `Ctrl+Alt+S`.

### Main modifier and sub-modifiers

Like `$mainMod` in Hyprland: one variable is your **main** modifier, and each group of hotkeys adds one more
modifier to it. Change `main` and everything moves along.

```toml
[mods]
main = "Ctrl"

[prompts]
Correction = "$main+Alt+K"   # Ctrl+Alt+K
Rewrite = "$main+Shift+K"    # Ctrl+Shift+K
```

**Combining** is free: a variable plus fixed modifiers (`$main+Alt+K`), or two variables (`$main+$extra+K`), as
long as no modifier appears twice after filling in. `$card+Ctrl+K` with `card = "Ctrl+Alt"` is an error
("Modifier 'Ctrl' appears twice"), and the message shows both what you wrote and what it became.

### Things to keep in mind

* **Ctrl alone takes app shortcuts.** With `main = "Ctrl"`, `$main+S` is `Ctrl+S`, and no app can save any more while
  Kuroko runs (`$main+C`, `$main+V` and `$main+A` are refused, see [App shortcuts](#app-shortcuts)). Use `main` only together with a second modifier (`$main+Alt+…`, `$main+Shift+…`), or make `main` a
  pair from the start (`main = "Ctrl+Shift"`).
* **Ctrl+Alt is AltGr.** A variable such as `card = "Ctrl+Alt"` runs into [the AltGr trap](#the-altgr-trap) for the
  keys that type something with AltGr (`$card+Q` takes `@` on German). Kuroko warns about each such hotkey.
* **Win as main** is mostly taken by Windows itself: `Win+E`, `Win+D`, `Win+L`, `Win+V`, `Win+.` and many more. Some
  cannot be registered at all, others stop working in Windows. `Win+Alt` and `Win+Shift` are freer, but not
  completely (see the [Win recipe](#win-as-main)).
* **Only Ctrl, Alt, Shift and Win are modifiers.** A variable cannot make `Tab` or `CapsLock` a modifier
  (see [Limits](#limits)).

## Submaps

A submap is one hotkey that opens a small set of plain keys for a moment, like a leader key in Vim or a submap in
Hyprland. Press the `open` hotkey, the pill shows the submap's label, then press one of its keys (`P`, `L`, …) to run
what it stands for. Ten prompts then need one hotkey instead of ten.

```toml
# keybinds.toml
[mods]
replace = "Shift+Alt"

[submaps.tone]
open = "$replace+^"                     # Alt+Shift+^ opens the submap
label = "Ton: P professionell · L locker · K kürzer · A ausführlicher"
timeout = 3000                          # milliseconds, default 2000
keys = { P = "Professioneller", L = "Lockerer", K = "Kürzen", A = "Ausführlicher" }
```

`Alt+Shift+^`, then `P`, runs the prompt "Professioneller" on the selected text, exactly as its own hotkey would.

### Fields

| Field | Meaning | Default |
|---|---|---|
| `open` | The hotkey that opens the submap. Written like any hotkey: modifiers, `$variables` from `[mods]`, every key name. Pressed again while the submap is open, it closes it. | required |
| `label` | The text the pill shows while the submap is open. Best used to say which key does what. | the submap's name |
| `timeout` | How long the submap stays open without a key, in milliseconds, from 100 to 60000. | `2000` |
| `keys` | Each key and what it runs: `key = "target"`. At least one. | required |

The name after `submaps.` is free: letters, digits, `_` and `-` (`tone`, `my-tools`), not case-sensitive. Each
submap has its own `[submaps.<name>]` block in `keybinds.toml`.

`keys` can also be written as a table of its own, one key per line, which is easier to read for long lists:

```toml
[submaps.tone]
open = "$replace+^"

[submaps.tone.keys]
P = "Professioneller"
L = "Lockerer"
"<" = "Kürzen"          # characters other than letters and digits need quotes
```

### Keys

A key in a submap is written **alone, without modifiers**: `P`, `5`, `F5`, `Space`, `Num5`, `0xE2`, or the
character printed on it (`"<"`, `"ü"`, `"#"`), exactly as the key part of a hotkey (see [Keys](#keys)). Any key may
stand alone here, not only F1–F24, because it is a hotkey only while the submap is open. Not allowed:

* modifiers (`"Ctrl+P"`): the submap takes the plain key only;
* `Esc`: it closes the submap;
* the same key twice (`p` and `P`, or `"<"` and `0xE2`);
* a key that is already a hotkey on its own elsewhere (an `F8` prompt hotkey and `F8` in a submap).

### Targets

| Target | Example | Runs |
|---|---|---|
| A prompt name from `prompts.toml` | `P = "Professioneller"` | the prompt, exactly as its own hotkey would: it reads the selection (or the whole field), and replaces it or shows the card. Names are not case-sensitive. The prompt needs no hotkey of its own. |
| A command with `/` | `U = "/undo"` | the command, as the tray menu does. |

The commands are the ones of the overlay's `/` list, plus `/undo`:

| Command | Does |
|---|---|
| `/undo` | undo the last replacement, as the `undo` hotkey does (with `undo_history = 0` there is nothing to undo) |
| `/copy` | copy the last result |
| `/prompts`, `/settings`, `/keybinds` | open `prompts.toml`, `settings.toml` or `keybinds.toml` in the editor |
| `/folder` | open the config folder |
| `/reload` | reload the configuration |
| `/keys` | open the API key window |
| `/test` | test the connection to the default provider |
| `/autostart` | switch autostart on or off |
| `/about` | show the version |
| `/quit` | quit Kuroko |

A prompt a submap names must exist: if you rename or remove it in `prompts.toml`, that `prompts.toml` is rejected
with a message on the submap's line in `keybinds.toml`, and the last working version stays active until you change the
submap too.

### While a submap is open

* **The pill** shows the label, where pills appear (`pill_position`), until the submap closes. A click on it closes
  the submap.
* **Taken from other apps:** only the listed keys and `Esc`, without modifiers, and only until the submap closes, at
  most for `timeout` milliseconds (default 2 seconds). Pressing them goes to Kuroko, not to the app in front.
* **Everything else types as usual.** A key that is not listed, or a listed key with a modifier (`Shift+P`), reaches
  the app in front; the submap stays open until a listed key, `Esc` or the timeout.
* **A listed key** closes the submap first (all its keys are released) and then runs its target.
* **`Esc` or the timeout** closes the submap; nothing runs.
* **Other hotkeys** still work. Each one closes the submap first, as do the `open` hotkey of another submap, a
  configuration reload and a message in the pill.
* **While a prompt runs or the overlay is open**, the `open` hotkey is ignored, like the other hotkeys.
* **A key another app already holds** (it registered the same plain key itself) is left out of the submap: the log
  says `Submap '…': key … is left out, it could not be registered`, and the other keys still work.

### Limits of submaps

Kuroko has no keyboard hook (see [Limits](#limits)), so a submap is a set of hotkeys that exist for a moment, not a
mode that sees every key:

* It cannot react to keys that are not listed: they are not swallowed and do not close the submap.
* Only one key after `open`: a target cannot be another submap, and there are no sequences of more than two keys.
* The listed keys are taken as they are pressed, without modifiers: `P` and `Shift+P` cannot do different things.
* A key that Windows or another app keeps for itself cannot be taken (see the log line above).

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
A `prompts.toml` created by an older version had `Ctrl+Alt+M`; it moves to `keybinds.toml` with the other hotkeys and
shows the warning there. Change it in `keybinds.toml` if you type `µ` with AltGr+M.

Ways around it: another letter (`Ctrl+Alt+K`, `Ctrl+Alt+Z` are free on German), `Ctrl+Shift+…`, `Alt+Shift+…`,
`Ctrl+Alt+Shift+…` or `Win+Alt+…`.

### App shortcuts

* Avoid shortcuts you use in your apps: `Ctrl+Shift+T` (reopen tab), `Ctrl+Shift+N` (new window), `Ctrl+Shift+V`
  (paste as text), `Ctrl+Alt+Delete`, and the `Win+letter` shortcuts of Windows (many cannot be taken at all, e.g.
  `Win+L`).
* Plain `Ctrl+A`, `Ctrl+C` and `Ctrl+V` cannot be any hotkey (`[app]`, `[prompts]` or a submap's `open`, also not
  through a variable such as `$main+C` with `main = "Ctrl"`): Kuroko sends those keys itself to read and replace text,
  so it could no longer read or paste, and Ctrl+C/V would stop working in every app while Kuroko runs. Such a hotkey is
  an error with its line.
* `F12` is reserved by Windows for debuggers and may fail to register.
* If another app already owns a combination, Kuroko says so at start: "Hotkey … is already used by another app".

## Recipes

### By output: replace or card

One modifier pair for prompts that replace the text, another for prompts that show a card. The letter says what the
prompt does; the modifiers say where the result goes. This is the scheme Kuroko's author uses. The names are only a
convention: where the result goes is still the prompt's `output` in `prompts.toml`.

```toml
# keybinds.toml
[mods]
replace = "Shift+Alt"   # the result replaces the text
card = "Ctrl+Alt"       # the result appears in a card

[prompts]
Correct = "$replace+C"      # Alt+Shift+C
Shorten = "$replace+S"      # Alt+Shift+S
Summarize = "$card+S"       # Ctrl+Alt+S, output = "overlay" in prompts.toml
"Fact check" = "$card+F"    # Ctrl+Alt+F, output = "overlay" in prompts.toml
```

`S` appears in both groups (shorten, summarize) without a clash, because the modifiers differ. On German, avoid
`$card+Q`, `E`, `M`, `2`, `3`, `7`–`0`, `ß`, `+` and `<` ([the AltGr trap](#the-altgr-trap)).

### Ctrl as main

```toml
# keybinds.toml
[mods]
main = "Ctrl"

[app]
overlay = "$main+Shift+Space"   # Ctrl+Shift+Space
undo = "$main+Alt+Z"            # Ctrl+Alt+Z
result_copy = "$main+Alt+C"     # Ctrl+Alt+C

[prompts]
Correction = "$main+Alt+K"      # Ctrl+Alt+K
Rewrite = "$main+Shift+1"       # a second group: Ctrl+Shift+1, 2, 3 …
```

Always add a second modifier to `$main`; `$main+K` alone would be `Ctrl+K` and steal an app shortcut.
`Ctrl+Shift+<digit>` is rarely used by apps, which makes it a good group for many prompts.

### Win as main

```toml
# keybinds.toml
[mods]
main = "Win+Alt"

[app]
overlay = "$main+Space"   # Win+Alt+Space
undo = "$main+Z"          # Win+Alt+Z
result_copy = "$main+C"   # Win+Alt+C

[prompts]
Correction = "$main+J"    # Win+Alt+J
```

`Win+Alt` keeps all Ctrl and Alt shortcuts of your apps free, and there is no AltGr trap. Windows itself uses a few
`Win+Alt` combinations, so avoid them: `Win+Alt+D` (date and time), `Win+Alt+K` (microphone in calls), `Win+Alt+R`,
`Win+Alt+G`, `Win+Alt+PrintScreen` (Game Bar), `Win+Alt+B` (HDR), `Win+Alt+Up/Down` (snap), and
`Win+Alt+<digit>` (jump list of a taskbar app), so use letters for prompts. Plain `Win+<letter>` is
mostly taken by Windows; do not use `main = "Win"` alone.

### A tone submap

One hotkey for four rewrites of the selected text. `^` is the key left of `1` on German; on US, take `` ` `` or
another key.

```toml
# keybinds.toml
[mods]
replace = "Shift+Alt"

[submaps.tone]
open = "$replace+^"   # Alt+Shift+^
label = "Ton: P professionell · L locker · K kürzer · A ausführlicher"
timeout = 3000
keys = { P = "Professioneller", L = "Lockerer", K = "Kürzen", A = "Ausführlicher" }
```

```toml
# prompts.toml: the four prompts must exist (names are not case-sensitive); they need no hotkey of their own
[[prompt]]
name = "Kürzen"
prompt = "..."

[[prompt]]
name = "Ausführlicher"
prompt = "..."
```

"Professioneller" and "Lockerer" are built-in prompts of a new `prompts.toml`; "Kürzen" and "Ausführlicher" are
examples you write yourself (or point `K` and `A` at the built-in "Condense" and "Expand").

### A tools submap with commands

Undo, copy and reload on one hotkey, next to a prompt:

```toml
# keybinds.toml
[submaps.tools]
open = "Ctrl+Alt+Shift+K"
label = "U undo · C copy last result · R reload · K correct"
keys = { U = "/undo", C = "/copy", R = "/reload", K = "Correction" }
```

`Ctrl+Alt+Shift+K`, then `U`, undoes the last replacement. With such a submap, the `undo` hotkey can move to a
combination you never press by accident.

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

Messages appear in a pill and the tray, with file and line, e.g. `keybinds.toml, line 12: [app] undo: …` or
`keybinds.toml, line 30: [prompts] 'Correction': …`. Errors keep the last working configuration active; warnings do not
block anything.

| Message | Meaning | Fix |
|---|---|---|
| Hotkey is empty. | A hotkey in `[app]` (or a submap's `open`) is `""`. | Write a hotkey, or remove the line to get the default. (Under `[prompts]`, `""` just means "no hotkey".) |
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
| hotkey … is used twice: … and … | Two settings, prompts or submap `open` hotkeys are the same (also when written differently, e.g. `<` and `0xE2`, or `$card+S` and `Ctrl+Alt+S`). | Give one of them another hotkey. |
| Unknown variable '$…' in hotkey '…'. Defined in [mods] of keybinds.toml: … | The hotkey uses a `$name` that `[mods]` does not define (typo, or removed from `[mods]`). | Fix the name, or add it to `[mods]`. Until then the last working `keybinds.toml` stays active. |
| Unknown variable '$…' in hotkey '…'. No variables are defined: add them to a [mods] table in keybinds.toml. | The hotkey uses `$name`, but `keybinds.toml` has no `[mods]` (or it is empty). | Add `[mods]` with that name, see [Modifiers and variables](#modifiers-and-variables). |
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
| submaps must contain tables: [submaps.name] with open, label, timeout and keys | `submaps = …` was written as a single value. | Write one `[submaps.<name>]` block per submap, see [Submaps](#submaps). |
| submaps.… must be a table ([submaps.…]) | A line `name = …` directly under `[submaps]`. | Write `[submaps.<name>]` and put `open`, `keys` … below it. |
| [submaps.…] name '…' is not allowed: use letters, digits, _ and -. | The name in `[submaps."…"]` has other characters (a space, a dot). | Rename it, e.g. `my-tools`. |
| [submaps.…] name '…' is used twice (names are not case-sensitive) | Two submaps whose names differ only in case. | Rename one. |
| [submaps.…] open is missing: the hotkey that opens the submap | The block has no `open`. | Add `open = "…"` with a hotkey. |
| [submaps.…] open: *any hotkey message above* | The `open` hotkey has an error, e.g. no modifier or an unknown variable. | Fix it as the message says; `open` follows all rules of a hotkey. |
| [submaps.…] label must not be empty | `label = ""`. | Write a label, or remove the line to show the name. |
| [submaps.…] timeout must be between 100 and 60000 (milliseconds) | `timeout` is too small, too large or not a whole number. | Write milliseconds: `timeout = 3000` for three seconds. |
| [submaps.…] keys must list at least one key and what it runs | `keys` is missing, empty or not a table. | `keys = { P = "Professioneller" }`. |
| [submaps.…] Submap key '…' has modifiers: inside a submap a key is written alone (P, not Ctrl+P). | A key with `+` (`"Ctrl+P"`). | Write the plain key; the modifiers belong in `open`. |
| [submaps.…] Submap key '…' is Esc, which closes the submap; choose another key. | `Esc` in `keys`. | Esc always closes the submap; use another key. |
| [submaps.…] Submap key '…' is a modifier: a modifier alone cannot be a key. | `Shift`, `Ctrl`, … in `keys`. | Use a real key. |
| [submaps.…] Unknown key '…' / No key on the active keyboard layout … / '…' is Shift+… | The key itself is wrong: the same messages and fixes as for hotkeys above. | See the rows above. |
| [submaps.…] key '…' is listed twice (also as '…') | Two entries mean the same key (`p` and `P`, `"<"` and `0xE2`). | Keep one. |
| [submaps.…] key '…' must name a prompt from prompts.toml or a command such as "/undo" | The target is empty (or just `/`) or not text. | Write a prompt name or a command. |
| [submaps.…] key '…': unknown command '/…' (commands: …) | A target with `/` that is not a command. | Use one of the commands the message lists, see [Targets](#targets). |
| [submaps.…] key '…' runs '…', but prompts.toml has no prompt of that name (prompts: …) | No prompt has that name: a typo, or the prompt was renamed or removed. The message lists the prompts. | Fix the name in `keybinds.toml`, or add the prompt. If you just renamed it in `prompts.toml`, that file stays rejected until the submap uses the new name. |
| [submaps.…] key '…' is also the hotkey of …; give one of them another key | A submap key that is already a hotkey without modifiers (e.g. an `F8` prompt hotkey and `F8` in a submap). | Change one of them. |
| *(warning)* [submaps.…] unknown setting '…' | A field other than `open`, `label`, `timeout`, `keys` (often a typo such as `lable`). | Fix the name. |
| *…*: Ctrl+… is used by the app to read and replace text (Ctrl+C, Ctrl+A, Ctrl+V); choose another hotkey | A hotkey (`[app]`, `[prompts]` or a submap's `open`) is plain `Ctrl+C`, `Ctrl+A` or `Ctrl+V`, also when it comes from a variable (`$main+C` with `main = "Ctrl"`). See [App shortcuts](#app-shortcuts). | Choose another hotkey, e.g. the default `Ctrl+Alt+C` for `result_copy`. |
| [prompts] no prompt '…' in prompts.toml (prompts: …) | A name under `[prompts]` is not a prompt in `prompts.toml`: a typo, or the prompt was renamed or removed. Names are not case-sensitive. | Fix the name in `keybinds.toml`, or add the prompt. If you just renamed it in `prompts.toml`, that file stays rejected until `[prompts]` uses the new name; saving both files together works. |
| [prompts] '…' is listed twice (prompt names are not case-sensitive) | Two lines under `[prompts]` name the same prompt (`Fix` and `fix`). | Keep one. |
| [prompts] '…' must be a hotkey in quotes, e.g. "Ctrl+Alt+K" | The value is a number or a list. | Put the hotkey in quotes. `""` means no hotkey. |
| app must be a table: [app] with lines like overlay = "…" / prompts must be a table: … | `app = …` or `prompts = …` was written as a single value. | Write an `[app]` or `[prompts]` table, one hotkey per line. |
| *(warning)* [app] unknown hotkey '…' (known: overlay, undo, result_copy) | A line in `[app]` other than the three app hotkeys (often a typo). | Fix the name. Prompt hotkeys go under `[prompts]`. |
| *(warning)* unknown entry '…' | Something at the top of `keybinds.toml` that is not `[mods]`, `[app]`, `[prompts]` or `[submaps.…]`, e.g. `overlay = …` above `[app]`. | Move it into the right table. |
| Hotkey … (…) is already used by another app. | Shown at start or reload: Windows refused the hotkey because another program registered it first. | Choose another hotkey, or quit the other program. |
| Hotkey … (…) is invalid: … | Shown at start or reload: the hotkey could not be read when it was registered, most often a character such as `ü` while a keyboard layout without that key is active. The reason follows the colon. | Switch to the layout the hotkey was written for and reload, or write the key's name or code (see [Which keyboard layout counts](#which-keyboard-layout-counts)). |
| *(warning)* overlay_hotkey / undo_hotkey / result_copy_hotkey / [mods] / [submaps] in settings.toml is no longer read; keybinds live in keybinds.toml | An old key in `settings.toml`. It was taken over into `keybinds.toml` once and is ignored since then. | Delete it from `settings.toml`; change the hotkey in `keybinds.toml`. |
| *(warning)* prompt '…': hotkey in prompts.toml is no longer read; keybinds live in keybinds.toml | An old `hotkey` line in `prompts.toml`, ignored like the above. | Delete it; the prompt's hotkey is under `[prompts]` in `keybinds.toml`. |
| *(note)* keybinds.toml: created from the hotkeys in settings.toml and prompts.toml (…) | Shown once, at the first start of a version with `keybinds.toml`. Lists what was taken over. | Nothing to do. See [Migrating](#from-settingstoml-and-promptstoml). |
| keybinds.toml: not created yet: settings.toml cannot be read (…) | `keybinds.toml` is missing and `settings.toml` (or `prompts.toml`) has a syntax error, so its hotkeys cannot be taken over yet. Until then the default hotkeys apply. | Fix the file the message names; on the next save `keybinds.toml` is created from it. |

## Migrating

### From settings.toml and prompts.toml

Up to version 1.1, the hotkeys were spread over two files: `overlay_hotkey`, `undo_hotkey`, `result_copy_hotkey`, `[mods]`
and `[submaps]` in `settings.toml`, and a `hotkey` line in each `[[prompt]]` of `prompts.toml`. Now they all live in
`keybinds.toml`.

* **Nothing to do by hand.** At the first start of the new version, `keybinds.toml` is missing, and Kuroko builds it from
  those values: the three app hotkeys go to `[app]` (as `overlay`, `undo`, `result_copy`), every prompt `hotkey` to
  `[prompts]` under the prompt's name, `[mods]` and every `[submaps.<name>]` move as they are. Hotkeys are taken over as
  written, `$variables` included. A note in the pill and the log says what was taken over.
* **Your old files are not changed.** As with the move from the old product names, Kuroko never rewrites them. The old
  keys stay where they are, are no longer read, and each one gives a warning with file and line until you delete it.
  Changing an old key has no effect any more.
* **An existing `keybinds.toml` is never overwritten.** The move happens only while the file is missing. Deleting
  `keybinds.toml` creates it again (from the old keys if they are still there, otherwise with the defaults).
* **A new install** gets a `keybinds.toml` with the defaults and the hotkeys of the built-in prompts
  (`Universal = "Ctrl+Alt+U"`, `Correction = "Ctrl+Alt+K"`).
* **If an old file has a syntax error**, its hotkeys cannot be read: `keybinds.toml` is not created yet, a message says
  so, and the default hotkeys apply until the file is fixed.

Before:

```toml
# settings.toml
overlay_hotkey = "Ctrl+Shift+Space"
[mods]
replace = "Shift+Alt"
```

```toml
# prompts.toml
[[prompt]]
name = "Correction"
hotkey = "$replace+C"
prompt = "..."
```

After (the new file; delete the old lines whenever you like):

```toml
# keybinds.toml
[mods]
replace = "Shift+Alt"

[app]
overlay = "Ctrl+Shift+Space"
undo = "Ctrl+Alt+Z"
result_copy = "Ctrl+Alt+C"

[prompts]
Correction = "$replace+C"
```

### US characters of punctuation keys

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
