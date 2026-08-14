using MacKeyboard.Core;
using MacKeyboard.Native;

namespace MacKeyboard.Windows;

/// <summary>
/// Rectangle's window management, reimplemented directly rather than driven through Windows Snap.
///
/// Snap cannot express most of this: it has no top/bottom half, its quarters need a chained
/// <c>Win+←</c>+<c>Win+↑</c> that trips Snap Assist, and it has no thirds, centring or undo at all.
/// Computing the frame and calling <c>SetWindowPos</c> is both simpler and an exact match for what
/// Rectangle does on the Mac.
///
/// Runs on the worker thread — never on the hook thread.
/// </summary>
sealed class RectangleEngine(Action<string>? log = null)
{
    /// <summary>The size cycle a repeated half-command walks: half, two thirds, one third.</summary>
    static readonly double[] HalfCycle = [1.0 / 2.0, 2.0 / 3.0, 1.0 / 3.0];

    /// <summary>Windows are considered "untouched since we placed them" within this many pixels.</summary>
    const int Tolerance = 4;

    readonly Dictionary<nint, Win32.RECT> _restore = [];

    nint _lastHwnd;
    AppCommand _lastCommand;
    Win32.RECT _lastApplied;
    int _cycleIndex;

    public void Execute(AppCommand command)
    {
        var hwnd = Win32.GetForegroundWindow();
        if (hwnd == nint.Zero) return;

        var monitors = GetMonitors();
        if (monitors.Count == 0) return;

        var monitorHandle = Win32.MonitorFromWindow(hwnd, Win32.MONITOR_DEFAULTTONEAREST);
        var monitorIndex = monitors.FindIndex(m => m.Handle == monitorHandle);
        if (monitorIndex < 0) monitorIndex = 0;
        var work = monitors[monitorIndex].Work;

        if (!Win32.GetWindowRect(hwnd, out var current)) return;

        RememberRestorePoint(hwnd);

        if (command == AppCommand.RectRestore)
        {
            if (_restore.Remove(hwnd, out var original)) Apply(hwnd, original, record: false);
            _lastHwnd = nint.Zero;
            return;
        }

        // A repeat only counts if it is the same command, on the same window, and the window has
        // not been moved since we placed it — otherwise the cycle restarts at half.
        var isRepeat = hwnd == _lastHwnd
                       && command == _lastCommand
                       && Matches(current, _lastApplied);

        _cycleIndex = isRepeat ? _cycleIndex + 1 : 0;

        var target = command switch
        {
            AppCommand.RectLeftHalf => Fraction(work, HalfCycle[_cycleIndex % 3], Edge.Left),
            AppCommand.RectRightHalf => Fraction(work, HalfCycle[_cycleIndex % 3], Edge.Right),
            AppCommand.RectTopHalf => Fraction(work, HalfCycle[_cycleIndex % 3], Edge.Top),
            AppCommand.RectBottomHalf => Fraction(work, HalfCycle[_cycleIndex % 3], Edge.Bottom),

            AppCommand.RectTopLeft => Quarter(work, left: true, top: true),
            AppCommand.RectTopRight => Quarter(work, left: false, top: true),
            AppCommand.RectBottomLeft => Quarter(work, left: true, top: false),
            AppCommand.RectBottomRight => Quarter(work, left: false, top: false),

            AppCommand.RectFirstThird => Column(work, 0, 1),
            AppCommand.RectCenterThird => Column(work, 1, 1),
            AppCommand.RectLastThird => Column(work, 2, 1),
            AppCommand.RectFirstTwoThirds => Column(work, 0, 2),
            AppCommand.RectLastTwoThirds => Column(work, 1, 2),

            // Spectacle's third cycle steps across the screen instead of resizing in place.
            AppCommand.RectNextThird => Column(work, _cycleIndex % 3, 1),
            AppCommand.RectPrevThird => Column(work, 2 - (_cycleIndex % 3), 1),

            AppCommand.RectMaximize => work,
            AppCommand.RectMaximizeHeight => new Win32.RECT
            {
                Left = current.Left,
                Right = current.Right,
                Top = work.Top,
                Bottom = work.Bottom,
            },
            AppCommand.RectCenter => Centered(work, current.Width, current.Height),
            AppCommand.RectLarger => Scaled(work, current, 1.05),
            AppCommand.RectSmaller => Scaled(work, current, 1 / 1.05),

            AppCommand.RectNextDisplay => OnDisplay(monitors, monitorIndex, +1, work, current),
            AppCommand.RectPrevDisplay => OnDisplay(monitors, monitorIndex, -1, work, current),

            _ => current,
        };

        _lastHwnd = hwnd;
        _lastCommand = command;
        Apply(hwnd, target, record: true);
    }

    /// <summary>Forget a window's stored restore point — called when it is closed.</summary>
    public void Forget(nint hwnd)
    {
        _restore.Remove(hwnd);
        if (_lastHwnd == hwnd) _lastHwnd = nint.Zero;
    }

    // ---------------------------------------------------------------- geometry

    enum Edge { Left, Right, Top, Bottom }

    static Win32.RECT Fraction(Win32.RECT work, double fraction, Edge edge)
    {
        var w = (int)Math.Round(work.Width * fraction);
        var h = (int)Math.Round(work.Height * fraction);

        return edge switch
        {
            Edge.Left => new Win32.RECT { Left = work.Left, Top = work.Top, Right = work.Left + w, Bottom = work.Bottom },
            Edge.Right => new Win32.RECT { Left = work.Right - w, Top = work.Top, Right = work.Right, Bottom = work.Bottom },
            Edge.Top => new Win32.RECT { Left = work.Left, Top = work.Top, Right = work.Right, Bottom = work.Top + h },
            _ => new Win32.RECT { Left = work.Left, Top = work.Bottom - h, Right = work.Right, Bottom = work.Bottom },
        };
    }

    static Win32.RECT Quarter(Win32.RECT work, bool left, bool top)
    {
        var w = work.Width / 2;
        var h = work.Height / 2;
        var x = left ? work.Left : work.Left + w;
        var y = top ? work.Top : work.Top + h;
        return new Win32.RECT { Left = x, Top = y, Right = x + w, Bottom = y + h };
    }

    /// <summary>A vertical band <paramref name="span"/> thirds wide, starting at third <paramref name="start"/>.</summary>
    static Win32.RECT Column(Win32.RECT work, int start, int span)
    {
        var third = work.Width / 3;
        var x = work.Left + third * start;
        return new Win32.RECT { Left = x, Top = work.Top, Right = x + third * span, Bottom = work.Bottom };
    }

    static Win32.RECT Centered(Win32.RECT work, int width, int height)
    {
        var x = work.Left + (work.Width - width) / 2;
        var y = work.Top + (work.Height - height) / 2;
        return new Win32.RECT { Left = x, Top = y, Right = x + width, Bottom = y + height };
    }

    static Win32.RECT Scaled(Win32.RECT work, Win32.RECT current, double factor)
    {
        var w = Math.Min((int)Math.Round(current.Width * factor), work.Width);
        var h = Math.Min((int)Math.Round(current.Height * factor), work.Height);
        var centred = Centered(new Win32.RECT
        {
            Left = current.Left,
            Top = current.Top,
            Right = current.Right,
            Bottom = current.Bottom,
        }, w, h);

        return Clamp(centred, work);
    }

    static Win32.RECT Clamp(Win32.RECT r, Win32.RECT work)
    {
        var w = Math.Min(r.Width, work.Width);
        var h = Math.Min(r.Height, work.Height);
        var x = Math.Clamp(r.Left, work.Left, work.Right - w);
        var y = Math.Clamp(r.Top, work.Top, work.Bottom - h);
        return new Win32.RECT { Left = x, Top = y, Right = x + w, Bottom = y + h };
    }

    static Win32.RECT OnDisplay(
        List<Display> monitors, int index, int step, Win32.RECT from, Win32.RECT current)
    {
        if (monitors.Count < 2) return current;

        var target = monitors[((index + step) % monitors.Count + monitors.Count) % monitors.Count].Work;

        // Carry the window's proportional place on the old screen over to the new one, so a
        // left-half window stays a left-half window across displays of different sizes.
        double relX = (double)(current.Left - from.Left) / from.Width;
        double relY = (double)(current.Top - from.Top) / from.Height;
        double relW = (double)current.Width / from.Width;
        double relH = (double)current.Height / from.Height;

        var moved = new Win32.RECT
        {
            Left = target.Left + (int)Math.Round(relX * target.Width),
            Top = target.Top + (int)Math.Round(relY * target.Height),
        };
        moved.Right = moved.Left + (int)Math.Round(relW * target.Width);
        moved.Bottom = moved.Top + (int)Math.Round(relH * target.Height);

        return Clamp(moved, target);
    }

    // ---------------------------------------------------------------- application

    void Apply(nint hwnd, Win32.RECT target, bool record)
    {
        // A maximized window ignores SetWindowPos, so it has to come down first.
        if (Win32.IsIconic(hwnd) || IsMaximized(hwnd))
            Win32.ShowWindow(hwnd, Win32.SW_RESTORE);

        var ok = Win32.SetWindowPos(
            hwnd, nint.Zero, target.Left, target.Top, target.Width, target.Height,
            Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);

        if (!ok)
        {
            log?.Invoke($"SetWindowPos failed for {hwnd:X}");
            return;
        }

        if (!record) return;

        // Read back rather than trusting the request: apps with a minimum size, and DPI rounding,
        // both land somewhere other than asked, and the repeat-cycle check compares against reality.
        _lastApplied = Win32.GetWindowRect(hwnd, out var actual) ? actual : target;
    }

    void RememberRestorePoint(nint hwnd)
    {
        if (_restore.ContainsKey(hwnd)) return;

        var placement = new Win32.WINDOWPLACEMENT { length = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Win32.WINDOWPLACEMENT>() };
        if (Win32.GetWindowPlacement(hwnd, ref placement))
            _restore[hwnd] = placement.rcNormalPosition;
        else if (Win32.GetWindowRect(hwnd, out var rect))
            _restore[hwnd] = rect;
    }

    static bool IsMaximized(nint hwnd)
    {
        var placement = new Win32.WINDOWPLACEMENT { length = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Win32.WINDOWPLACEMENT>() };
        return Win32.GetWindowPlacement(hwnd, ref placement) && placement.showCmd == 3; // SW_SHOWMAXIMIZED
    }

    static bool Matches(Win32.RECT a, Win32.RECT b) =>
        Math.Abs(a.Left - b.Left) <= Tolerance &&
        Math.Abs(a.Top - b.Top) <= Tolerance &&
        Math.Abs(a.Right - b.Right) <= Tolerance &&
        Math.Abs(a.Bottom - b.Bottom) <= Tolerance;

    // ---------------------------------------------------------------- monitors

    readonly record struct Display(nint Handle, Win32.RECT Work);

    static List<Display> GetMonitors()
    {
        var found = new List<Display>(2);

        Win32.EnumDisplayMonitors(nint.Zero, nint.Zero, (h, _, _, _) =>
        {
            var info = new Win32.MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Win32.MONITORINFO>() };
            if (Win32.GetMonitorInfoW(h, ref info)) found.Add(new Display(h, info.rcWork));
            return true;
        }, nint.Zero);

        // Left-to-right, so "next display" follows the physical arrangement.
        found.Sort((a, b) => a.Work.Left != b.Work.Left
            ? a.Work.Left.CompareTo(b.Work.Left)
            : a.Work.Top.CompareTo(b.Work.Top));

        return found;
    }
}
