; MacShortcuts.ahk - Mac-style keyboard shortcuts
; After KeyRemapper: physical Cmd sends Ctrl, physical Option sends Win

#Requires AutoHotkey v2.0

if !Config["EnableMacShortcuts"]
    return

; ============ Application Control ============
; Cmd+Q = Quit application (Alt+F4)
^q::Send("!{F4}")

; Cmd+H = Hide/Minimize window
^h::WinMinimize("A")

; Cmd+M = Minimize window
^m::WinMinimize("A")

; Cmd+W = Close window/tab
; Note: ^w already sends Ctrl+W which most apps use for close tab

; ============ Cursor Movement ============
; Cmd+Left = Home (beginning of line)
^Left::Send("{Home}")

; Cmd+Right = End (end of line)
^Right::Send("{End}")

; Cmd+Up = Page Up (User requested)
^Up::Send("{PgUp}")

; Cmd+Down = Page Down (User requested)
^Down::Send("{PgDn}")

; ============ Cursor Movement with Selection ============
; Cmd+Shift+Left = Select to beginning of line
+^Left::Send("+{Home}")

; Cmd+Shift+Right = Select to end of line
+^Right::Send("+{End}")

; Cmd+Shift+Up = Select to beginning of document
+^Up::Send("+^{Home}")

; Cmd+Shift+Down = Select to end of document
+^Down::Send("+^{End}")

; ============ Word Movement (Option+Arrow) ============
; After remapping: physical Option sends Win key
; Option+Left = Ctrl+Left (previous word)
#Left::Send("^{Left}")

; Option+Right = Ctrl+Right (next word)
#Right::Send("^{Right}")

; Option+Shift+Left = Select previous word
+#Left::Send("+^{Left}")

; Option+Shift+Right = Select next word
+#Right::Send("+^{Right}")

; ============ Text Editing ============
; Cmd+Delete = Delete line before cursor (to beginning of line)
^Backspace::Send("+{Home}{Delete}")

; Option+Delete = Delete word before cursor
#Backspace::Send("^{Backspace}")

; Cmd+Shift+Z = Redo (Windows uses Ctrl+Y)
+^z::Send("^y")

; NOTE: Cmd+Tab and Cmd+` are handled in KeyRemapper.ahk to avoid conflicts

; ============ System Shortcuts ============
; Cmd+Space = Windows search (like Spotlight)
; After remapping, this is Ctrl+Space - we map to Win+S
^Space::Send("#s")

; Cmd+Option+Esc = Task Manager (like Force Quit)
^#Escape::Run("taskmgr.exe")

; Cmd+Shift+3 = Screenshot (full screen)
+^3::Send("#{PrintScreen}")

; Cmd+Shift+4 = Screenshot (region) - uses Snipping Tool
+^4::Send("#s")

; Cmd+Shift+5 = Snipping Tool
+^5::Send("#+s")

; ============ Finder/File Explorer Shortcuts ============
; Cmd+Shift+N = New folder (works in Explorer)
; Already Ctrl+Shift+N in Windows, so no remap needed

; Cmd+Shift+. = Show hidden files (toggle in Explorer)
+^.::Send("^h")  ; This doesn't work in Explorer, but some apps use it

; ============ Browser Shortcuts ============
; These mostly work the same after remapping since browsers use Ctrl+key
; Cmd+L = Focus address bar (Ctrl+L) - works automatically
; Cmd+T = New tab (Ctrl+T) - works automatically
; Cmd+N = New window (Ctrl+N) - works automatically
; Cmd+R = Refresh (Ctrl+R) - works automatically
