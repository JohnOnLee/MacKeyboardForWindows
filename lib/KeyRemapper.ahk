; KeyRemapper.ahk - Modifier key remapping for Mac keyboard on Windows
; Maps physical Mac keyboard keys to Windows equivalents

#Requires AutoHotkey v2.0

; Only apply remapping if enabled in config
if !Config["EnableRemapping"]
    return

; ============ Mac Keyboard Modifier Remapping ============
; Physical Mac Keyboard Layout:  Ctrl | Option | Cmd | Space | Cmd | Option
; Desired Windows Behavior:      Alt  | Win    | Ctrl| Space | Ctrl| Win

; Remap Left side modifiers
*LCtrl::LAlt      ; Control → Alt
*LAlt::LWin       ; Option → Win  
*LWin::LCtrl      ; Command → Ctrl

; Remap Right side modifiers
*RCtrl::RAlt      ; Control → Alt
*RAlt::RWin       ; Option → Win
*RWin::RCtrl      ; Command → Ctrl

; ============ Special Cmd+Tab handling ============
; We intercept Tab and detect if LWin (Physical Cmd) is held
; This allows correct behavior without making LWin a prefix key

$*Tab::{
    if GetKeyState("LWin", "P") {
        ; Release the remapped Ctrl first
        Send("{Blind}{LCtrl up}")
        
        ; Start App Switcher (Alt+Tab)
        Send("{Blind}{SC038 down}{Tab}")
        
        ; Wait for initial Tab release to prevent rapid cycling
        KeyWait("Tab")
        
        ; Loop while Cmd is held to cycle through apps
        while GetKeyState("LWin", "P") {
            if GetKeyState("Tab", "P") {
                if GetKeyState("Shift", "P")
                    Send("{Blind}{Shift down}{Tab}{Shift up}")
                else
                    Send("{Blind}{Tab}")
                
                ; Wait for Tab release so we don't cycle too fast
                KeyWait("Tab")
            }
            Sleep(10)
        }
        
        ; Cmd released, close switcher
        Send("{Blind}{SC038 up}")
        return
    }
    
    if GetKeyState("RWin", "P") {
        Send("{Blind}{RCtrl up}")
        Send("{Blind}{SC038 down}{Tab}")
        KeyWait("Tab")
        
        while GetKeyState("RWin", "P") {
            if GetKeyState("Tab", "P") {
                if GetKeyState("Shift", "P")
                    Send("{Blind}{Shift down}{Tab}{Shift up}")
                else
                    Send("{Blind}{Tab}")
                KeyWait("Tab")
            }
            Sleep(10)
        }
        Send("{Blind}{SC038 up}")
        return
    }
    
    ; Normal Tab behavior - pass through with modifiers
    Send("{Blind}{Tab}")
}

; ============ Special Cmd+` handling ============
$*SC029::{
    if GetKeyState("LWin", "P") || GetKeyState("RWin", "P") {
        ; Cmd+` detected - Window Switching
        if GetKeyState("Shift", "P")
            SwitchToNextWindowOfSameApp(true)
        else
            SwitchToNextWindowOfSameApp(false)
        return
    }
    
    ; Normal Backtick behavior
    Send("{Blind}{SC029}")
}
