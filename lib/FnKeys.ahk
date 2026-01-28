; FnKeys.ahk - Fn key combinations for Mac keyboards
; Handles Fn+Delete (forward delete), Fn+Arrows (Page navigation)

#Requires AutoHotkey v2.0

if !Config["EnableFnKeys"]
    return

; ============ Fn Key Handling ============
; On Mac keyboards connected to Windows, the Fn key modifies other keys
; at the hardware level for some combinations, but we can add more.

; The Mac keyboard's Fn key typically:
; - Fn+Delete = Forward Delete (this often works at hardware level)
; - Fn+Up = Page Up (hardware level on most Mac keyboards)
; - Fn+Down = Page Down (hardware level on most Mac keyboards)
; - Fn+Left = Home (hardware level on most Mac keyboards)
; - Fn+Right = End (hardware level on most Mac keyboards)

; Most Mac keyboards handle Fn+key at hardware level, so these may already work.
; However, if your keyboard doesn't, or for additional functionality:

; ============ Additional Fn-style Behaviors ============
; Some Mac keyboards send different scancodes when Fn is pressed
; The exact behavior depends on your specific keyboard model

; ============ Fn Key Attempts ============
; Try common scancodes for Fn key if exposed
#HotIf !GetKeyState("LWin", "P")
try {
    SC063 & Backspace::Send("{Delete}")
}
try {
    VKFF & Backspace::Send("{Delete}")
}
#HotIf

; Alternative: Use Right Ctrl as Fn equivalent
; NOTE: On Mac Keyboard, physical 'Right Command' is mapped to 'Right Ctrl'
; If you want to enable this fallback, uncomment below:
; >^Backspace::Send("{Delete}")      ; RCtrl+Backspace = Forward Delete
; >^Up::Send("{PgUp}")               ; RCtrl+Up = Page Up
; >^Down::Send("{PgDn}")             ; RCtrl+Down = Page Down
; >^Left::Send("{Home}")             ; RCtrl+Left = Home
; >^Right::Send("{End}")             ; RCtrl+Right = End

; ============ Insert Key Behavior ============
; Mac keyboards don't have Insert key - if needed, you can map it:
; Fn+Enter = Insert (if Fn is detectable)
; Or use a different key combination:

; Shift+Fn+Delete could be mapped if Fn is detectable
; For now, we provide an alternative using Ctrl+Shift+I
; ^+i::Send("{Insert}")

; ============ Function Row (F1-F12) ============
; Mac keyboards can toggle between F-keys and media keys with Fn
; This is usually handled at hardware/BIOS level
; If you need to swap them in software, uncomment below:

; To make F1-F12 always send F-keys (when normally they send media keys):
; This depends on your keyboard's default behavior

; To make media keys the default and require Fn for F-keys:
; (Most Mac users prefer this behavior)
; F1::Send("{Volume_Mute}")    ; etc.

; ============ Globe/Fn Key as Emoji Picker ============
; Newer Mac keyboards have Globe key that can open emoji picker
; If your keyboard has this and it's detectable:
; The Globe key often sends VK_F16 or similar

; Map a key to Windows emoji picker (Win+.)
; This provides similar functionality to Mac's emoji picker
; Already available as Win+. in Windows, but you can add:
; ^+Space::Send("#.")  ; Cmd+Shift+Space for emoji picker
