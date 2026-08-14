using System.Diagnostics;
using MacKeyboard.Native;

namespace MacKeyboard.Windows;

/// <summary>
/// ⌘` — cycle between windows of the frontmost application.
///
/// Runs on the worker thread. Enumerating every top-level window takes long enough that doing it
/// inside the hook callback risks blowing LowLevelHooksTimeout, which is how the previous build
/// managed to freeze modifiers while switching windows.
/// </summary>
sealed class WindowCycler(Action<string>? log = null)
{
    public void Cycle(bool backward, IReadOnlyCollection<string> blacklist)
    {
        var active = Win32.GetForegroundWindow();
        if (active == nint.Zero) return;

        var appName = ProcessNameOf(active);
        if (appName is null) return;

        if (blacklist.Contains(appName, StringComparer.OrdinalIgnoreCase))
            return;

        var windows = WindowsOf(appName);
        if (windows.Count < 2) return;

        if (backward)
        {
            // Bring the bottom-most window of the app to the front.
            Activate(windows[^1]);
        }
        else
        {
            // Rotate the stack rather than just activating the second window: without the demotion
            // the z-order flips back on the next press and cycling ping-pongs between two windows
            // instead of walking all of them.
            Win32.SetWindowPos(active, Win32.HWND_BOTTOM, 0, 0, 0, 0,
                Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
            Activate(windows[1]);
        }
    }

    static void Activate(nint hwnd)
    {
        if (Win32.IsIconic(hwnd)) Win32.ShowWindow(hwnd, Win32.SW_RESTORE);
        Win32.SetForegroundWindow(hwnd);
    }

    /// <summary>Top-level windows of the given executable, in z-order, front first.</summary>
    List<nint> WindowsOf(string appName)
    {
        var result = new List<nint>(4);
        var names = new Dictionary<uint, string?>();

        Win32.EnumWindows((hwnd, _) =>
        {
            if (!Win32.IsWindowVisible(hwnd)) return true;
            if (Win32.GetWindowTextLengthW(hwnd) == 0) return true;

            // Child and popup windows belong to a parent that is already in the list.
            if (Win32.GetWindow(hwnd, Win32.GW_OWNER) != nint.Zero) return true;

            var exStyle = (long)Win32.GetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE);
            if ((exStyle & Win32.WS_EX_TOOLWINDOW) != 0) return true;

            // Suspended UWP apps and windows parked on other virtual desktops pass every check
            // above but are not on screen.
            if (Win32.IsCloaked(hwnd)) return true;

            Win32.GetWindowThreadProcessId(hwnd, out var pid);
            if (!names.TryGetValue(pid, out var name))
            {
                name = ProcessName(pid);
                names[pid] = name;
            }

            // Compared by executable, not by process id: Chrome and friends spread their windows
            // across several processes.
            if (string.Equals(name, appName, StringComparison.OrdinalIgnoreCase))
                result.Add(hwnd);

            return true;
        }, nint.Zero);

        log?.Invoke($"cycle: {result.Count} window(s) of {appName}");
        return result;
    }

    static string? ProcessNameOf(nint hwnd)
    {
        Win32.GetWindowThreadProcessId(hwnd, out var pid);
        return ProcessName(pid);
    }

    static string? ProcessName(uint pid)
    {
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch
        {
            // The process can exit between enumeration and lookup, and protected processes refuse
            // the query outright. Either way it is simply not a candidate.
            return null;
        }
    }
}
