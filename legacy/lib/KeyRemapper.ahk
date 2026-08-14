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

; ============ Safer Cmd+Tab Handling (Timer Based) ============
$*Tab::{
    if GetKeyState("LWin", "P") {
        ; Release remapped Ctrl to avoid conflict
        if GetKeyState("LCtrl") 
            Send("{Blind}{LCtrl up}")
            
        ; If Alt is not yet down, press it down
        if !GetKeyState("LAlt")
            Send("{Blind}{SC038 down}")
            
        ; Send the Tab used for switching
        Send("{Blind}{Tab}")
        
        ; Start a timer to check for Cmd release
        SetTimer(CheckCmdRelease, 10)
        return
    }
    
    if GetKeyState("RWin", "P") {
        if GetKeyState("RCtrl")
            Send("{Blind}{RCtrl up}")
            
        if !GetKeyState("LAlt")
            Send("{Blind}{SC038 down}")
            
        Send("{Blind}{Tab}")
        SetTimer(CheckCmdRelease, 10)
        return
    }
    
    ; Normal Tab behavior
    Send("{Blind}{Tab}")
}

CheckCmdRelease() {
    ; If neither Win key is held, cleanup and stop timer
    if !GetKeyState("LWin", "P") && !GetKeyState("RWin", "P") {
        Send("{Blind}{SC038 up}") ; Release Alt
        SetTimer(CheckCmdRelease, 0) ; Stop timer
    }
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
