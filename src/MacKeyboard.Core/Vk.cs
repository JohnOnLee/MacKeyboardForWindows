namespace MacKeyboard.Core;

/// <summary>
/// Windows virtual-key codes. Defined here rather than taken from System.Windows.Forms.Keys
/// so this assembly stays platform-neutral and its tests run anywhere.
/// </summary>
public static class Vk
{
    // Modifiers (side-specific). The generic VK_SHIFT/CONTROL/MENU are deliberately absent:
    // the remapper always works with the side-specific codes so every down pairs with its own up.
    public const int LShift = 0xA0;
    public const int RShift = 0xA1;
    public const int LControl = 0xA2;
    public const int RControl = 0xA3;
    public const int LMenu = 0xA4;   // left Alt  — physical Option on a Mac keyboard
    public const int RMenu = 0xA5;   // right Alt — physical Option on a Mac keyboard
    public const int LWin = 0x5B;    // physical Command on a Mac keyboard
    public const int RWin = 0x5C;    // physical Command on a Mac keyboard

    // Editing / navigation
    public const int Back = 0x08;
    public const int Tab = 0x09;
    public const int Return = 0x0D;
    public const int Escape = 0x1B;
    public const int Space = 0x20;
    public const int Prior = 0x21;   // Page Up
    public const int Next = 0x22;    // Page Down
    public const int End = 0x23;
    public const int Home = 0x24;
    public const int Left = 0x25;
    public const int Up = 0x26;
    public const int Right = 0x27;
    public const int Down = 0x28;
    public const int Snapshot = 0x2C; // Print Screen
    public const int Insert = 0x2D;
    public const int Delete = 0x2E;

    // Digits
    public const int D0 = 0x30;
    public const int D3 = 0x33;
    public const int D4 = 0x34;
    public const int D5 = 0x35;

    // Letters
    public const int A = 0x41;
    public const int C = 0x43;
    public const int D = 0x44;
    public const int E = 0x45;
    public const int F = 0x46;
    public const int G = 0x47;
    public const int H = 0x48;
    public const int I = 0x49;
    public const int J = 0x4A;
    public const int K = 0x4B;
    public const int L = 0x4C;
    public const int M = 0x4D;
    public const int Q = 0x51;
    public const int R = 0x52;
    public const int S = 0x53;
    public const int T = 0x54;
    public const int U = 0x55;
    public const int V = 0x56;
    public const int W = 0x57;
    public const int Y = 0x59;
    public const int Z = 0x5A;

    // Function row
    public const int F3 = 0x72;
    public const int F4 = 0x73;
    public const int F5 = 0x74;
    public const int F11 = 0x7A;
    public const int F12 = 0x7B;

    // OEM keys
    public const int OemMinus = 0xBD;    // -
    public const int OemPlus = 0xBB;     // =
    public const int OemPeriod = 0xBE;   // .
    public const int Oem4 = 0xDB;        // [
    public const int Oem6 = 0xDD;        // ]
    public const int Oem3 = 0xC0;        // ` (backtick)

    // Media
    public const int VolumeMute = 0xAD;
    public const int VolumeDown = 0xAE;
    public const int VolumeUp = 0xAF;
    public const int MediaNextTrack = 0xB0;
    public const int MediaPrevTrack = 0xB1;
    public const int MediaPlayPause = 0xB3;

    /// <summary>True for the side-specific modifier keys the remapper owns.</summary>
    public static bool IsModifier(int vk) => vk is
        LShift or RShift or LControl or RControl or LMenu or RMenu or LWin or RWin;
}
