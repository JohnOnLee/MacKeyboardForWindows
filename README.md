# MacKeyboard

Mac key bindings on Windows, for a Mac keyboard. A single `.exe` — no AutoHotkey, no .NET runtime,
nothing to install.

Includes [Rectangle](https://rectangleapp.com)'s window management, repeat-cycle and all.

## Install

1. Download `MacKeyboard.exe` from [Releases](../../releases).
2. Run it. It asks for administrator rights — see [why](#why-administrator) below.
3. Right-click the tray icon → **Start with Windows**.

## Key bindings

Physical modifiers: **⌃ Control → Alt**, **⌥ Option → Win**, **⌘ Command → Ctrl**.

Anything not listed follows from that mapping — ⌘C, ⌘V, ⌘S, ⌘T, ⌘1–9 and friends need no rule and
just work.

### Cursor & selection

| Mac | Windows |
|-----|---------|
| ⌘← / ⌘→ | Home / End |
| ⌘↑ / ⌘↓ | Page Up / Page Down |
| ⌥← / ⌥→ | Previous / next word |
| ⌥↑ / ⌥↓ | Previous / next paragraph |

Add ⇧ to any of the above to select.

> ⌘↑/⌘↓ are Page Up/Down rather than document start/end, because a Mac keyboard on Windows has no
> usable fn key and those would otherwise be unreachable. Document start/end remains ⇧⌘↑/⇧⌘↓'s
> Windows equivalent via `Ctrl+Home`/`Ctrl+End` in apps that support it.

### Editing

| Mac | Windows |
|-----|---------|
| ⌘⌫ | Delete to start of line |
| ⌥⌫ | Delete previous word |
| ⌥⌦ | Delete next word |
| ⇧⌘Z | Redo |
| ⌥⇧⌘V | Paste and match style |
| ⌘G / ⇧⌘G | Find next / previous |

### Apps & windows

| Mac | Windows |
|-----|---------|
| ⌘Q | Quit (Alt+F4) |
| ⌘M / ⌘H | Minimize |
| ⌥⌘H | Hide others |
| ⌘Tab / ⇧⌘Tab | App switcher — hold ⌘ to keep cycling |
| ⌘` / ⇧⌘` | Cycle windows within the current app |
| ⌥⌘Esc | Task Manager |
| ⌃⌘F | Full screen (F11) |

### System & screenshots

| Mac | Windows |
|-----|---------|
| ⌘Space | Search (Win+S) |
| ⌥⌘Space | File Explorer |
| ⌃⌘Q | Lock screen |
| ⇧⌘3 | Screenshot, full screen |
| ⇧⌘4 / ⇧⌘5 | Screenshot, region |

### Browser

| Mac | Windows |
|-----|---------|
| ⌘[ / ⌘] | Back / forward |
| ⇧⌘[ / ⇧⌘] | Previous / next tab |
| ⌘R / ⇧⌘R | Reload / hard reload |
| ⌥⌘I | DevTools |

## Window management (Rectangle)

> **Which key is which.** Shortcuts below name the keys as they are printed on the Mac keyboard,
> and they match on the physical key — not on what it has been remapped to. The same key has three
> names depending on who is talking, which is worth having in front of you:
>
> | Printed on the key | What Windows calls it | What this program makes it do |
> |---|---|---|
> | ⌃ `control` | Ctrl | Alt |
> | ⌥ `option` | Alt | Win |
> | ⌘ `command` | Win | Ctrl |
>
> So the Spectacle preset's ⌥⌘ is `option`+`command` — which Windows reports as Alt+Win, and which
> behaves as Win+Ctrl once remapped. All three describe the same two keys.

Two presets, chosen with `Preset=` in `config.ini`.

| Action | `Preset=Rectangle` | `Preset=Spectacle` |
|--------|--------------------|--------------------|
| Left / right half | ⌃⌥← / ⌃⌥→ | ⌥⌘← / ⌥⌘→ |
| Top / bottom half | ⌃⌥↑ / ⌃⌥↓ | ⌥⌘↑ / ⌥⌘↓ |
| Top-left / top-right quarter | ⌃⌥U / ⌃⌥I | ⌃⌘← / ⌃⌘→ |
| Bottom-left / bottom-right quarter | ⌃⌥J / ⌃⌥K | ⇧⌃⌘← / ⇧⌃⌘→ |
| First / center / last third | ⌃⌥D / ⌃⌥F / ⌃⌥G | ⌃⌥← / ⌃⌥→ steps through |
| First / last two thirds | ⌃⌥E / ⌃⌥T | — |
| Maximize | ⌃⌥↩ | ⌥⌘F |
| Maximize height | ⌃⌥⇧↑ | — |
| Center | ⌃⌥C | ⌥⌘C |
| Larger / smaller | ⌃⌥= / ⌃⌥- | — |
| Undo | ⌃⌥⌫ | ⌥⌘Z |
| Next / previous display | ⌃⌥⌘→ / ⌃⌥⌘← | ⌃⌥⌘→ / ⌃⌥⌘← |

**Press a half command repeatedly to cycle 1/2 → 2/3 → 1/3**, exactly as Rectangle does. The cycle
restarts if you move the window in between. If a half command appears to be resizing rather than
snapping, that is the cycle — press it a third time to come back round to half.

Windows Snap is not used for any of this — it has no top/bottom half, its quarters fight with Snap
Assist, and it has no thirds, centering or undo. Frames are computed and applied directly.

## Configuration

`config.ini`, created next to the exe on first run. Tray → **Reload config** applies changes
without restarting.

```ini
[Rectangle]
Preset=Rectangle        ; or Spectacle

[WindowSwitcher]
Blacklist=game,vmware   ; apps ⌘` should leave alone

[General]
Log=false               ; see Troubleshooting
```

## Troubleshooting

**A key feels stuck.** It should not happen, and if it does the program fixes itself within 200 ms.
If you catch one anyway, any of these clears it immediately:

- Hold **both Shift keys** for one second.
- Double-click the tray icon.
- Tray → **Release stuck keys**.

Then set `Log=true` in `config.ini`, reload, and reproduce it. `%LOCALAPPDATA%\MacKeyboard\input.log`
records every key pressed and every key emitted, so the unmatched press is visible directly.

**Nothing is being remapped.** Another tool may own the keyboard first (PowerToys Keyboard Manager,
a vendor utility). Turn one of them off. If the program was started without administrator rights,
remapping also stops working inside elevated windows.

**Why administrator.** Windows blocks injected input from a lower-privilege process into a
higher-privilege window (UIPI). Without elevation, focusing Task Manager or an installer means the
key-down we injected arrives and the key-up does not — which is precisely how a modifier gets
stuck. "Start with Windows" registers a scheduled task rather than a Startup shortcut, so elevation
does not prompt at every login.

**Fn key combinations do nothing.** Windows never sees the fn key on a Mac keyboard without
Apple's driver, and with the driver installed fn is handled below us — either way the program
cannot bind it. That is why ⌘↑/⌘↓ cover Page Up/Down.

## Building

```bash
dotnet publish src/MacKeyboard/MacKeyboard.csproj -c Release -o publish
```

Produces one self-contained `publish/MacKeyboard.exe`, about 47 MB — it carries the whole .NET
runtime so the target machine needs nothing installed. This cross-builds from macOS and Linux;
`EnableWindowsTargeting` is set in the project file. Requires the .NET 10 SDK.

The exe is compressed but deliberately not trimmed: WinForms resolves a lot by reflection, and a
bad trim shows up as a runtime crash rather than a build error.

```bash
dotnet test
```

The remapper is deliberately free of Win32, so its tests run on any platform. Among them is a
randomised invariant check: after any sequence of key events, once every physical key is released,
nothing may be left held — neither by us nor by the focused application. That property is what the
old AutoHotkey build violated, and it is the reason this one is structured the way it is.

## Layout

```
src/MacKeyboard.Core/     the remapper — pure state machine, no Win32
  Remapper.cs             lazy modifier emission, ⌘Tab session, reconciler
  Bindings.cs             the binding table, as data
src/MacKeyboard/          the Windows layer
  Native/                 hook, SendInput
  Windows/                Rectangle engine, ⌘` window cycler
tests/                    runs on macOS/Linux
legacy/                   the previous AutoHotkey build, kept as a fallback
```

## License

MIT
