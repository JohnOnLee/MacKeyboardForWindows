; MacKeyboard - All-in-one Mac keyboard behavior for Windows
; AutoHotkey v2 Script
; https://github.com/your-username/MacKeyboard

#Requires AutoHotkey v2.0
#SingleInstance Force
SendMode("Input")
SetWorkingDir(A_ScriptDir)

; Load configuration
global Config := LoadConfig()

; Include modules
#Include "lib\KeyRemapper.ahk"
#Include "lib\MacShortcuts.ahk"
#Include "lib\WindowSwitcher.ahk"
#Include "lib\FnKeys.ahk"

; Setup tray menu
SetupTrayMenu()

; Show startup notification
if Config.Has("ShowNotification") && Config["ShowNotification"]
    TrayTip("MacKeyboard is running", "Mac keyboard behavior enabled", 1)

; ============ Configuration Loading ============
LoadConfig() {
    config := Map()
    iniPath := A_ScriptDir "\config.ini"
    
    ; Defaults
    config["EnableRemapping"] := true
    config["EnableMacShortcuts"] := true
    config["EnableWindowSwitcher"] := true
    config["EnableFnKeys"] := true
    config["ShowTrayIcon"] := true
    config["ShowNotification"] := true
    config["WindowSwitcherBlacklist"] := ""
    
    if FileExist(iniPath) {
        config["EnableRemapping"] := IniRead(iniPath, "KeyRemapping", "EnableRemapping", "true") = "true"
        config["EnableMacShortcuts"] := IniRead(iniPath, "MacShortcuts", "EnableMacShortcuts", "true") = "true"
        config["EnableWindowSwitcher"] := IniRead(iniPath, "WindowSwitcher", "EnableWindowSwitcher", "true") = "true"
        config["EnableFnKeys"] := IniRead(iniPath, "FnKeys", "EnableFnKeys", "true") = "true"
        config["ShowTrayIcon"] := IniRead(iniPath, "General", "ShowTrayIcon", "true") = "true"
        config["ShowNotification"] := IniRead(iniPath, "General", "ShowNotification", "true") = "true"
        config["WindowSwitcherBlacklist"] := IniRead(iniPath, "WindowSwitcher", "Blacklist", "")
    }
    
    return config
}

; ============ Tray Menu Setup ============
SetupTrayMenu() {
    if !Config["ShowTrayIcon"] {
        A_IconHidden := true
        return
    }
    
    tray := A_TrayMenu
    tray.Delete()
    tray.Add("MacKeyboard", (*) => "")
    tray.Disable("MacKeyboard")
    tray.Add()
    tray.Add("Pause/Resume", TogglePause)
    tray.Add("Reload", (*) => Reload())
    tray.Add("Open Config", OpenConfig)
    tray.Add()
    tray.Add("Start with Windows", ToggleStartup)
    if FileExist(A_Startup "\MacKeyboard.lnk")
        tray.Check("Start with Windows")
    tray.Add()
    tray.Add("Exit", (*) => ExitApp())
    
    tray.Default := "Pause/Resume"
}

TogglePause(*) {
    Suspend(-1)
    if A_IsSuspended
        TrayTip("MacKeyboard Paused", "Keyboard remapping disabled", 2)
    else
        TrayTip("MacKeyboard Resumed", "Keyboard remapping enabled", 1)
}

OpenConfig(*) {
    iniPath := A_ScriptDir "\config.ini"
    if !FileExist(iniPath)
        CreateDefaultConfig(iniPath)
    Run(iniPath)
}

ToggleStartup(*) {
    shortcutPath := A_Startup "\MacKeyboard.lnk"
    if FileExist(shortcutPath) {
        FileDelete(shortcutPath)
        A_TrayMenu.Uncheck("Start with Windows")
        TrayTip("Startup Disabled", "MacKeyboard will not start with Windows", 2)
    } else {
        FileCreateShortcut(A_ScriptFullPath, shortcutPath)
        A_TrayMenu.Check("Start with Windows")
        TrayTip("Startup Enabled", "MacKeyboard will start with Windows", 1)
    }
}

CreateDefaultConfig(path) {
    config := "
    (
[General]
; Show tray icon
ShowTrayIcon=true
; Show notification on startup
ShowNotification=true

[KeyRemapping]
; Enable modifier key remapping (Ctrl<->Alt, Alt<->Win)
EnableRemapping=true

[MacShortcuts]
; Enable Mac-style keyboard shortcuts
EnableMacShortcuts=true

[WindowSwitcher]
; Enable Cmd+` window switching
EnableWindowSwitcher=true
; Apps to ignore (comma-separated exe names)
Blacklist=

[FnKeys]
; Enable Fn key combinations (Fn+Delete, Fn+Arrows)
EnableFnKeys=true
    )"
    FileAppend(config, path)
}
