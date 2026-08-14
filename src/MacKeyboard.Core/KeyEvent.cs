namespace MacKeyboard.Core;

/// <summary>A physical key event as delivered by the low-level keyboard hook.</summary>
/// <param name="Vk">Side-specific virtual-key code.</param>
/// <param name="IsDown">True for key-down (including auto-repeat), false for key-up.</param>
public readonly record struct KeyEvent(int Vk, bool IsDown)
{
    public static KeyEvent Down(int vk) => new(vk, true);
    public static KeyEvent Up(int vk) => new(vk, false);

    public override string ToString() => $"{(IsDown ? "v" : "^")}{Vk:X2}";
}

/// <summary>The Mac-side modifiers, as the user thinks of them.</summary>
[Flags]
public enum MacMods
{
    None = 0,
    Command = 1 << 0,  // physical ⌘ — arrives as VK_LWIN / VK_RWIN
    Option = 1 << 1,  // physical ⌥ — arrives as VK_LMENU / VK_RMENU
    Control = 1 << 2,  // physical ⌃ — arrives as VK_LCONTROL / VK_RCONTROL
    Shift = 1 << 3,
}

/// <summary>The Windows-side modifiers a rule output may carry.</summary>
[Flags]
public enum WinMods
{
    None = 0,
    Ctrl = 1 << 0,
    Alt = 1 << 1,
    Shift = 1 << 2,
    Win = 1 << 3,
}

public static class WinModsExtensions
{
    /// <summary>
    /// The keys a <see cref="Tap"/>'s modifiers expand to, in press order (release is the reverse).
    /// Lives here rather than in the sender so the tests model exactly what gets injected.
    /// </summary>
    public static int[] ToVks(this WinMods mods)
    {
        if (mods == WinMods.None) return [];

        var list = new List<int>(4);
        if ((mods & WinMods.Ctrl) != 0) list.Add(Vk.LControl);
        if ((mods & WinMods.Alt) != 0) list.Add(Vk.LMenu);
        if ((mods & WinMods.Shift) != 0) list.Add(Vk.LShift);
        if ((mods & WinMods.Win) != 0) list.Add(Vk.LWin);
        return [.. list];
    }
}

public static class MacModsExtensions
{
    /// <summary>Which Mac modifier a physical key belongs to, or null if it is not a modifier.</summary>
    public static MacMods? ToMacMod(int vk) => vk switch
    {
        Vk.LWin or Vk.RWin => MacMods.Command,
        Vk.LMenu or Vk.RMenu => MacMods.Option,
        Vk.LControl or Vk.RControl => MacMods.Control,
        Vk.LShift or Vk.RShift => MacMods.Shift,
        _ => null,
    };

    /// <summary>
    /// What a physical modifier emits on Windows. The mapping is a 3-cycle —
    /// ⌘→Ctrl, ⌥→Win, ⌃→Alt — with the left/right side preserved so every
    /// down has exactly one matching up.
    /// </summary>
    public static int ToMappedVk(int physicalVk) => physicalVk switch
    {
        Vk.LWin => Vk.LControl,
        Vk.RWin => Vk.RControl,
        Vk.LMenu => Vk.LWin,
        Vk.RMenu => Vk.RWin,
        Vk.LControl => Vk.LMenu,
        Vk.RControl => Vk.RMenu,
        Vk.LShift => Vk.LShift,
        Vk.RShift => Vk.RShift,
        _ => physicalVk,
    };
}
