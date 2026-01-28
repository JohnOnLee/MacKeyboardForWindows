# MacKeyboard

All-in-one Mac keyboard behavior for Windows. Use your Mac keyboard on Windows with familiar shortcuts and key mappings.

## Features

- **Modifier Key Remapping**: Cmd→Ctrl, Option→Win, Control→Alt
- **Mac Shortcuts**: Cmd+Q quit, Cmd+arrows navigation, Cmd+Shift+Z redo
- **Window Switcher**: Cmd+` to cycle windows of the same app
  - `Cmd+`` cycles **Forward** (moves current window to bottom)
  - `Cmd+Shift+`` cycles **Backward** (activates previous window)
- **Fn Keys**: Fn+Delete forward delete (hardware dependent)

## Requirements

- [AutoHotkey v2](https://www.autohotkey.com/download/ahk-v2.exe)

## Installation

1. Install AutoHotkey v2
2. Double-click `MacKeyboard.ahk` to run

### Auto-Start with Windows
To run automatically at startup:
1. Right-click the **MacKeyboard tray icon**
2. Select **"Start with Windows"**

## Key Mappings

### Modifier Keys

| Physical Key | Windows Equivalent | Purpose |
|--------------|-------------------|---------|
| ⌘ Command    | Ctrl              | Standard shortcuts |
| ⌥ Option     | Win               | System shortcuts |
| ⌃ Control    | Alt               | Alternate functions |

### Navigation

| Shortcut | Action |
|----------|--------|
| ⌘+← | Beginning of line (Home) |
| ⌘+→ | End of line (End) |
| ⌘+↑ | Page Up |
| ⌘+↓ | Page Down |
| ⌥+← | Previous word |
| ⌥+→ | Next word |

Add Shift to any of the above to select text.

### Application Control

| Shortcut | Action | Behavior |
|----------|--------|----------|
| ⌘+Tab | Alt+Tab | Holds Alt while Cmd is held to cycle apps |
| ⌘+Q | Alt+F4 | Quit application |
| ⌘+H | Win+Down | Minimize window |
| ⌘+` | Switch in app | Forward cycle (Rotate window stack) |
| ⌘+⇧+` | Switch in app | Backward cycle |
| ⌘+Space | Win+S | Windows Search |
| ⌘+⌥+Esc | Task Manager | Force Quit equivalent |

### Text Editing

| Shortcut | Action |
|----------|--------|
| ⌘+Delete | Delete to beginning of line |
| ⌥+Delete | Delete previous word |
| ⌘+⇧+Z | Redo (Ctrl+Y) |
| Fn+Delete | Forward Delete (If keyboard supports it) |

### Screenshots

| Shortcut | Action |
|----------|--------|
| ⌘+⇧+3 | Screenshot (full screen) |
| ⌘+⇧+4 | Screenshot (region select) |
| ⌘+⇧+5 | Snipping Tool |

## Configuration

Edit `config.ini` to customize:

```ini
[KeyRemapping]
EnableRemapping=true    ; Set false if using PowerToys

[WindowSwitcher]
Blacklist=game.exe      ; Apps to ignore
```

## Troubleshooting

**Keys not working (e.g. not typing)?**
Right-click tray icon -> Reload.
The script uses a robust detection method (`*Tab` and `*Backtick` hotkeys) to avoid standard remapping conflicts.

**Fn key not working?**
Most Mac keyboards connected to Windows (without Bootcamp drivers) do not send a signal for the Fn key. To fix this, you need to install the specific Apple Keyboard driver.

**How to install the driver (without full Bootcamp):**
1. Download [Brigadier](https://github.com/timsutton/brigadier/releases) (free tool).
2. Open Command Prompt as Admin and run: `brigadier.exe -m MacBookPro16,3`
3. Go to the `BootCamp/Drivers/Apple` folder it creates.
4. **If you see `AppleKeyboardInstaller64.exe`, run it.**
5. **If NOT**:
   - Go to `AppleKeyboardMagic2` (for newer) or `AppleKeyboard` (for older).
   - Right-click `Keymagic2.inf` (or `Keymagic64.inf`).
   - Select **"Install"**.
6. Restart your PC.

## License

MIT License - Feel free to modify and share!
