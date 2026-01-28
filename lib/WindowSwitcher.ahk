; WindowSwitcher.ahk - Switch between windows of the same application
; Cmd+` (backtick) to cycle through windows of the current app
; NOTE: The hotkey is defined in KeyRemapper.ahk to avoid conflicts

#Requires AutoHotkey v2.0

if !Config["EnableWindowSwitcher"]
    return

; ============ Window Switching Logic ============
SwitchToNextWindowOfSameApp(reverse := false) {
    ; Get the currently active window
    try {
        activeHwnd := WinGetID("A")
        activeExe := WinGetProcessName("A")
    } catch {
        return
    }
    
    ; Check blacklist
    if Config["WindowSwitcherBlacklist"] != "" {
        blacklist := StrSplit(Config["WindowSwitcherBlacklist"], ",")
        for exe in blacklist {
            if (Trim(exe) = activeExe)
                return
        }
    }
    
    ; Get all windows of the same EXECUTABLE (not just PID)
    ; This handles apps like Chrome that may have multiple processes
    windows := []
    
    ; Use WinGetList to get all windows in z-order (front to back)
    allWindows := WinGetList()
    
    for hwnd in allWindows {
        try {
            ; Check if same executable
            exe := WinGetProcessName(hwnd)
            if (exe != activeExe)
                continue
            
            ; Check if it's a real window (has title)
            title := WinGetTitle(hwnd)
            if (title = "")
                continue
            
            ; Check if window is visible
            style := WinGetStyle(hwnd)
            if !(style & 0x10000000)  ; WS_VISIBLE
                continue
            
            ; Skip tool windows
            exStyle := WinGetExStyle(hwnd)
            if (exStyle & 0x80)  ; WS_EX_TOOLWINDOW
                continue
            
            ; Skip windows owned by other windows (child/popup windows)
            if DllCall("GetWindow", "Ptr", hwnd, "UInt", 4, "Ptr")  ; GW_OWNER
                continue
                
            windows.Push(hwnd)
        } catch {
            continue
        }
    }
    
    ; Need at least 2 windows to switch
    ; Need at least 2 windows to switch
    if (windows.Length < 2)
        return
    
    if !reverse {
        ; FORWARD CYCLE (Cmd+`):
        ; To cycle continuously through A->B->C->A, we need to rotate the stack.
        ; Move the current top window to the bottom, revealing the next one.
        WinMoveBottom(activeHwnd)
        
        ; Activate the new top window (which was previously second)
        ; This ensures focus transfers correctly
        try {
            ; After moving top to bottom, the 2nd window in the list becomes the top
            ; windows[1] is the one we just moved to bottom? 
            ; No, windows[] list is a snapshot from BEFORE the move.
            ; So windows[1] is activeHwnd. windows[2] is the next one.
            if (windows.Length >= 2)
                WinActivate(windows[2])
        }
    } else {
        ; REVERSE CYCLE (Cmd+Shift+`):
        ; Bring the bottom-most window to the top.
        ; The existing logic finds the last window and activates it, which brings it to top.
        
        ; Find current window index in the snapshot
        currentIndex := 0
        for i, hwnd in windows {
            if (hwnd = activeHwnd) {
                currentIndex := i
                break
            }
        }
        
        ; If active window not in list (edge case), assume we want last one
        if (currentIndex = 0)
            currentIndex := 1
            
        ; Previous window (wrapping around)
        ; Since windows are Z-ordered (1=Top, Length=Bottom)
        ; "Previous" in cycle sequence A->B->C is C (switching backwards)
        ; So we want the bottom-most window (Length)
        
        nextIndex := windows.Length
        
        try {
            nextHwnd := windows[nextIndex]
            
            ; Restore if minimized
            if WinGetMinMax(nextHwnd) = -1
                WinRestore(nextHwnd)
            
            WinActivate(nextHwnd)
        }
    }
}
