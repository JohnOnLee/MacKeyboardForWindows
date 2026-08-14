using MacKeyboard.Core;

namespace MacKeyboard.Native;

/// <summary>
/// Turns the remapper's decisions into real keystrokes.
///
/// Each call to <see cref="Execute"/> goes out as a single <c>SendInput</c> batch, which is what
/// makes a <see cref="Tap"/> atomic: Windows will not interleave anything between the modifier
/// down and the modifier up, so a tap cannot be caught half-applied.
/// </summary>
sealed class InputSender(Action<string>? log = null)
{
    readonly Lock _gate = new();
    Win32.INPUT[] _buffer = new Win32.INPUT[16];

    public void Execute(IReadOnlyList<OutputOp> ops)
    {
        if (ops.Count == 0) return;

        lock (_gate)
        {
            var n = 0;
            foreach (var op in ops)
            {
                switch (op)
                {
                    case Down d:
                        Append(ref n, d.Vk, up: false);
                        break;

                    case Up u:
                        Append(ref n, u.Vk, up: true);
                        break;

                    case Tap t:
                        var mods = t.Mods.ToVks();
                        foreach (var m in mods) Append(ref n, m, up: false);
                        Append(ref n, t.Vk, up: false);
                        Append(ref n, t.Vk, up: true);
                        for (var i = mods.Length - 1; i >= 0; i--) Append(ref n, mods[i], up: true);
                        break;

                    // Run is dispatched to the worker thread by the caller; it never reaches here.
                }
            }

            if (n == 0) return;

            var sent = Win32.SendInput((uint)n, _buffer, System.Runtime.InteropServices.Marshal.SizeOf<Win32.INPUT>());
            if (sent != n)
                log?.Invoke($"SendInput dropped {n - sent} event(s) — likely UIPI (elevated window focused)");
        }
    }

    void Append(ref int n, int vk, bool up)
    {
        if (n == _buffer.Length) Array.Resize(ref _buffer, n * 2);

        var flags = up ? Win32.KEYEVENTF_KEYUP : 0u;
        if (Win32.IsExtendedKey(vk)) flags |= Win32.KEYEVENTF_EXTENDEDKEY;

        _buffer[n].type = Win32.INPUT_KEYBOARD;
        _buffer[n].u.ki = new Win32.KEYBDINPUT
        {
            wVk = (ushort)vk,
            wScan = 0,
            dwFlags = flags,
            time = 0,
            dwExtraInfo = Win32.InjectedSignature,
        };
        n++;
    }

    /// <summary>True while any of the physical keys the remapper tracks is actually down.</summary>
    public static bool IsPhysicallyDown(int vk) => (Win32.GetAsyncKeyState(vk) & 0x8000) != 0;
}
