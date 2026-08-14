using System.Diagnostics;
using System.Runtime.InteropServices;
using MacKeyboard.Core;

namespace MacKeyboard.Native;

/// <summary>
/// The low-level keyboard hook, and nothing else — it does not decide anything.
///
/// The one hard rule here is that <see cref="Callback"/> must return fast. Windows gives a
/// low-level hook <c>LowLevelHooksTimeout</c> (300 ms by default, under
/// <c>HKCU\Control Panel\Desktop</c>) and silently stops calling a hook that overruns it, freezing
/// whatever keys were down at that moment. The previous AutoHotkey build enumerated every window
/// on the desktop inside this callback; anything that heavy is queued to a worker instead.
/// </summary>
sealed class KeyboardHook : IDisposable
{
    readonly Func<KeyEvent, bool> _onKey;
    readonly Action<string>? _log;

    // Held in a field so the GC cannot collect the delegate while Windows still holds the pointer.
    readonly Win32.LowLevelKeyboardProc _proc;

    nint _handle;
    long _lastCallbackTicks;

    public KeyboardHook(Func<KeyEvent, bool> onKey, Action<string>? log = null)
    {
        _onKey = onKey;
        _log = log;
        _proc = Callback;
    }

    public bool IsInstalled => _handle != nint.Zero;

    // Stopwatch ticks are not TimeSpan ticks — GetElapsedTime does the frequency conversion.
    public TimeSpan SinceLastEvent => Stopwatch.GetElapsedTime(Interlocked.Read(ref _lastCallbackTicks));

    public void Install()
    {
        if (_handle != nint.Zero) return;

        var module = Win32.GetModuleHandleW(null);
        _handle = Win32.SetWindowsHookExW(Win32.WH_KEYBOARD_LL, _proc, module, 0);

        if (_handle == nint.Zero)
            throw new InvalidOperationException(
                $"SetWindowsHookEx failed: {Marshal.GetLastWin32Error()}");

        Interlocked.Exchange(ref _lastCallbackTicks, Stopwatch.GetTimestamp());
        _log?.Invoke("hook installed");
    }

    public void Uninstall()
    {
        if (_handle == nint.Zero) return;
        Win32.UnhookWindowsHookEx(_handle);
        _handle = nint.Zero;
        _log?.Invoke("hook removed");
    }

    /// <summary>
    /// Tear the hook down and put it back. The caller releases every held key first, because a
    /// hook that was silently dropped may have missed the releases that would have paired them.
    /// </summary>
    public void Reinstall()
    {
        Uninstall();
        Install();
    }

    nint Callback(int nCode, nint wParam, nint lParam)
    {
        if (nCode != Win32.HC_ACTION)
            return Win32.CallNextHookEx(_handle, nCode, wParam, lParam);

        Interlocked.Exchange(ref _lastCallbackTicks, Stopwatch.GetTimestamp());

        var data = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);

        // Our own output must not be remapped again, or ⌘← would rewrite the Home we just sent.
        if (data.dwExtraInfo == Win32.InjectedSignature)
            return Win32.CallNextHookEx(_handle, nCode, wParam, lParam);

        var msg = (int)wParam;
        var isDown = msg is Win32.WM_KEYDOWN or Win32.WM_SYSKEYDOWN;
        var isUp = msg is Win32.WM_KEYUP or Win32.WM_SYSKEYUP;
        if (!isDown && !isUp)
            return Win32.CallNextHookEx(_handle, nCode, wParam, lParam);

        bool suppress;
        try
        {
            suppress = _onKey(new KeyEvent((int)data.vkCode, isDown));
        }
        catch (Exception ex)
        {
            // Never let an exception escape into Windows' hook dispatch. Failing open — passing the
            // key through unchanged — is always safer than swallowing the user's keystroke.
            _log?.Invoke($"hook callback threw, passing key through: {ex}");
            return Win32.CallNextHookEx(_handle, nCode, wParam, lParam);
        }

        return suppress ? 1 : Win32.CallNextHookEx(_handle, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();
}
