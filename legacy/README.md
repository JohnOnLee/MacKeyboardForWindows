# legacy — the original AutoHotkey build

Kept as a fallback while the rewrite proves itself on a real work machine. Delete this folder once
the new `MacKeyboard.exe` has run clean for a week.

## Why it was replaced

AutoHotkey remaps by *synthesizing* keystrokes, which gives no guarantee that a synthetic key-down
is ever matched by a key-up. Three places leaked, and the symptom was a sticky Ctrl:

| Where | What it does |
|---|---|
| [`lib/KeyRemapper.ahk:33`](lib/KeyRemapper.ahk) | Force-releases `LCtrl` mid-hold, then leaves a 10 ms polling timer responsible for releasing Alt. Starve the timer and Alt stays down. |
| [`lib/MacShortcuts.ahk:11`](lib/MacShortcuts.ahk) | Every `^x::Send(...)` lifts the held Ctrl, sends, and re-presses it. Releasing ⌘ inside that window loses the up event. |
| [`lib/WindowSwitcher.ahk:34`](lib/WindowSwitcher.ahk) | Enumerates every window on the desktop *inside the hook callback*. Overrunning `LowLevelHooksTimeout` (300 ms) makes Windows silently stop calling the hook, freezing whatever was down at that instant. |

The replacement does not patch these. It removes the shape of the bug: modifiers are emitted
lazily, so shortcuts like ⌘Q and ⌘← never press Ctrl at all and have nothing to leak, and a
reconciler releases anything still held whose physical key is gone.

## Running it

Requires [AutoHotkey v2](https://www.autohotkey.com/download/ahk-v2.exe). Double-click
`MacKeyboard.ahk`.
