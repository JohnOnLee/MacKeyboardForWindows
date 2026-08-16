using System.Collections.Concurrent;
using System.Diagnostics;
using MacKeyboard.Core;
using MacKeyboard.Native;
using MacKeyboard.Windows;
using Microsoft.Win32;

namespace MacKeyboard;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        using var single = new Mutex(true, @"Local\MacKeyboardForWindows", out var isFirst);

        ApplicationConfiguration.Initialize();

        if (!isFirst)
        {
            // Exiting quietly here is a trap: "Start with Windows" launches a copy from wherever it
            // was registered, so double-clicking a newer build looks like it did nothing, and the
            // old one keeps running against its own config.ini next to it.
            MessageBox.Show(
                "MacKeyboard is already running.\n\n" +
                "It may be an older copy started at login — check the tray icon (it can be hidden " +
                "under the ^ arrow) and use Exit there before starting this one.",
                "MacKeyboard", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var app = new MacKeyboardApp(args);
        Application.Run(app);
    }
}

/// <summary>
/// Wiring.
///
/// The hook callback and the reconciler both run on the UI thread — the reconciler is a WinForms
/// timer, not a threadpool one. That makes <see cref="Remapper"/> single-threaded by construction,
/// so there is no lock and no window in which its state can be read half-updated. The only work
/// that crosses a thread boundary is <see cref="AppCommand"/> dispatch, which never touches the
/// remapper, and which must be off the hook thread anyway: window enumeration inside the callback
/// is what lets Windows drop the hook for exceeding LowLevelHooksTimeout.
/// </summary>
sealed class MacKeyboardApp : ApplicationContext
{
    const int ReconcileIntervalMs = 200;

    /// <summary>Both Shift keys held for this many ticks (1 s) releases everything.</summary>
    const int PanicTicks = 1000 / ReconcileIntervalMs;

    static readonly TimeSpan IdleBeforeHookRefresh = TimeSpan.FromSeconds(60);

    // Not readonly: logging can be switched on from the tray while the program is running.
    Logger? _log;

    readonly InputSender _sender;
    readonly RectangleEngine _rectangle;
    readonly WindowCycler _cycler;
    readonly KeyboardHook _hook;
    readonly TrayIcon _tray;
    readonly BlockingCollection<AppCommand> _commands = new();
    readonly System.Windows.Forms.Timer _timer;

    AppConfig _config;
    Remapper _remapper;
    int _bothShiftTicks;
    bool _paused;

    // Set from SystemEvents threads, acted on by the UI-thread timer.
    volatile bool _releaseRequested;
    volatile bool _reinstallRequested;

    public MacKeyboardApp(string[] args)
    {
        AppConfig.WriteDefaultIfMissing();
        _config = AppConfig.Load();

        _remapper = NewRemapper();
        _sender = new InputSender(Log);
        _rectangle = new RectangleEngine(Log);
        _cycler = new WindowCycler(Log);

        _hook = new KeyboardHook(OnKey, Log);
        _hook.Install();

        new Thread(WorkerLoop) { IsBackground = true, Name = "MacKeyboard.Worker" }.Start();

        _timer = new System.Windows.Forms.Timer { Interval = ReconcileIntervalMs };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();

        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;

        _tray = new TrayIcon(
            new TrayActions(
                TogglePause, ReleaseStuckKeys, ToggleLogging, ReloadConfig, OpenConfig, OpenLog, Quit),
            AppConfig.FilePath);
        _tray.SetStatus(Status());

        SetLogging(_config.Log || args.Contains("--log", StringComparer.OrdinalIgnoreCase));

        if (_config.ShowNotification)
            _tray.Notify("MacKeyboard", $"{Status()} — {_remapper.BindingCount} bindings.");
    }

    // ---------------------------------------------------------------- logging

    /// <summary>
    /// The sink handed to every component at construction. It reads <see cref="_log"/> on each call
    /// rather than capturing it, so switching logging on later reaches all of them without
    /// rebuilding anything.
    /// </summary>
    void Log(string message) => _log?.Write(message);

    void SetLogging(bool on)
    {
        if (on != (_log is not null))
        {
            if (on)
            {
                _log = new Logger();
                _log.Write($"config: {AppConfig.FilePath} -> {Status()}, {_remapper.BindingCount} bindings");
            }
            else
            {
                var previous = _log;
                _log = null;
                previous?.Dispose();
            }
        }

        _tray.SetLogging(on);
    }

    void ToggleLogging()
    {
        SetLogging(_log is null);

        _tray.Notify("MacKeyboard", _log is not null
            ? "Logging every key event. Reproduce the problem, then use “Open log…”."
            : "Logging off.");
    }

    /// <summary>
    /// Reported in the tray so the live configuration is visible. An unbound chord falls through to
    /// Windows and does something unrelated — ⌥⌘← becomes Win+Ctrl+← and switches virtual desktop —
    /// so "which preset is loaded" is not a question the keyboard can answer.
    /// </summary>
    string Status() => _config.EnableRectangle
        ? $"Rectangle preset: {_config.Preset}"
        : "window management off";

    Remapper NewRemapper() => new(_config.ToBindingOptions())
    {
        Suspended = _paused || !_config.EnableRemapping,
    };

    // ---------------------------------------------------------------- hook (UI thread)

    bool OnKey(KeyEvent e)
    {
        var result = _remapper.Process(e);

        // Both halves of the story on one line: the physical key, what we did with it, and what
        // went out instead. If a key ever sticks, the unmatched press is visible directly.
        _log?.Write(
            $"{(e.IsDown ? "down" : "up  ")} {e.Vk:X2} {(result.Suppress ? "eaten " : "passed")}" +
            (result.Ops.Count > 0 ? $" -> {string.Join(" ", result.Ops)}" : ""));

        if (result.Ops.Count == 0) return result.Suppress;

        _sender.Execute(result.Ops);

        foreach (var op in result.Ops)
        {
            if (op is not Run run) continue;

            // Adding after CompleteAdding throws, and shutdown can land between these two lines.
            try { _commands.Add(run.Command); }
            catch (InvalidOperationException) { }
        }

        return result.Suppress;
    }

    // ---------------------------------------------------------------- reconciler (UI thread)

    void Tick()
    {
        if (_releaseRequested)
        {
            _releaseRequested = false;
            ReleaseStuckKeys();
        }

        var stranded = _remapper.Reconcile(InputSender.IsPhysicallyDown);
        if (stranded.Count > 0)
        {
            // Named, because the reconciler firing during a ⌘Tab session would tear the switcher
            // down and look like ⌘Tab simply not working.
            _log?.Write($"reconciler released {string.Join(" ", stranded)}");
            _sender.Execute(stranded);
        }

        CheckPanicGesture();
        CheckHookHealth();
    }

    void CheckPanicGesture()
    {
        // Hook state, not the OS: Shift is suppressed like every other modifier, so
        // GetAsyncKeyState would report it up and the gesture could never fire.
        var both = _remapper.BothShiftsDown;
        if (!both)
        {
            _bothShiftTicks = 0;
            return;
        }

        if (++_bothShiftTicks != PanicTicks) return;

        _log?.Write("panic gesture (both shifts held) — releasing everything");
        ReleaseStuckKeys();
    }

    void CheckHookHealth()
    {
        if (!_reinstallRequested && _hook.SinceLastEvent < IdleBeforeHookRefresh) return;

        _reinstallRequested = false;

        // Windows silently stops calling a low-level hook that overruns LowLevelHooksTimeout, and
        // offers no way to ask whether that has happened.
        //
        // This is also the only recovery for a modifier whose release we never saw. The reconciler
        // cannot spot one: the OS does not track keys we suppress, so it has no second opinion to
        // offer. So release everything before putting the hook back, rather than skipping the
        // refresh whenever something is held — which was exactly backwards.
        ReleaseStuckKeys();

        try
        {
            _hook.Reinstall();
        }
        catch (Exception ex)
        {
            _log?.Write($"hook reinstall failed: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------- worker thread

    void WorkerLoop()
    {
        foreach (var command in _commands.GetConsumingEnumerable())
        {
            try
            {
                Dispatch(command);
            }
            catch (Exception ex)
            {
                _log?.Write($"{command} failed: {ex}");
            }
        }
    }

    void Dispatch(AppCommand command)
    {
        switch (command)
        {
            case AppCommand.CycleWindowForward:
                _cycler.Cycle(backward: false, _config.WindowSwitcherBlacklist);
                break;

            case AppCommand.CycleWindowBackward:
                _cycler.Cycle(backward: true, _config.WindowSwitcherBlacklist);
                break;

            case AppCommand.LaunchTaskManager:
                Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true });
                break;

            default:
                _rectangle.Execute(command);
                break;
        }
    }

    // ---------------------------------------------------------------- tray actions (UI thread)

    void ReleaseStuckKeys()
    {
        var ops = _remapper.ReleaseAll();
        if (ops.Count == 0) return;

        _log?.Write($"released {ops.Count} held key(s)");
        _sender.Execute(ops);
    }

    void TogglePause()
    {
        _paused = !_paused;
        ReleaseStuckKeys();          // never cross the boundary with a key still held
        _remapper.Suspended = _paused || !_config.EnableRemapping;
        _tray.SetPaused(_paused);
    }

    void ReloadConfig()
    {
        ReleaseStuckKeys();
        _config = AppConfig.Load();
        _remapper = NewRemapper();

        // The file can switch logging on, but never off — otherwise a reload would silently undo
        // the tray toggle mid-diagnosis.
        if (_config.Log) SetLogging(true);

        _tray.SetStatus(Status());
        _tray.Notify("Config reloaded", $"{Status()} — {_remapper.BindingCount} bindings.");
    }

    void OpenConfig()
    {
        AppConfig.WriteDefaultIfMissing();
        Open(AppConfig.FilePath);
    }

    void OpenLog()
    {
        if (_log is null)
        {
            _tray.Notify("MacKeyboard", "Switch on “Write log” first, then reproduce the problem.");
            return;
        }

        Open(_log.FilePath);
    }

    static void Open(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch
        {
            // Nothing useful to do if the shell refuses to open it.
        }
    }

    void Quit()
    {
        ReleaseStuckKeys();
        ExitThread();
    }

    // ---------------------------------------------------------------- system events (other threads)

    void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        _releaseRequested = true;
        if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.SessionLogon)
            _reinstallRequested = true;
    }

    void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode is not (PowerModes.Resume or PowerModes.Suspend)) return;

        _releaseRequested = true;
        if (e.Mode == PowerModes.Resume) _reinstallRequested = true;
    }

    // ---------------------------------------------------------------- teardown

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;

            _timer.Stop();
            _timer.Dispose();
            // Not disposed: the worker may still be inside GetConsumingEnumerable, and disposing
            // underneath it would throw on a thread nobody is watching. CompleteAdding is enough
            // to end the loop, and the process is going away regardless.
            _commands.CompleteAdding();

            _hook.Dispose();
            _tray.Dispose();
            _log?.Dispose();
        }

        base.Dispose(disposing);
    }
}
