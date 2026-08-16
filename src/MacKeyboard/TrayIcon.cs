namespace MacKeyboard;

sealed record TrayActions(
    Action TogglePause,
    Action ReleaseStuckKeys,
    Action ReloadConfig,
    Action OpenConfig,
    Action OpenLog,
    Action Exit);

sealed class TrayIcon : IDisposable
{
    readonly NotifyIcon _icon;
    readonly ToolStripMenuItem _header;
    readonly ToolStripMenuItem _pause;
    readonly ToolStripMenuItem _startup;

    string _status = "";
    bool _paused;

    public TrayIcon(TrayActions actions, bool logEnabled, string configPath)
    {
        // Which preset is live is not something you can tell by pressing keys — an unbound chord
        // just falls through to Windows and does something unrelated. Showing it here turns
        // "did my config edit take?" into a glance.
        _header = new ToolStripMenuItem("MacKeyboard") { Enabled = false };

        _pause = new ToolStripMenuItem("Pause", null, (_, _) => actions.TogglePause());

        _startup = new ToolStripMenuItem("Start with Windows", null, (_, _) => ToggleStartup())
        {
            Checked = StartupTask.IsEnabled(),
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_header);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_pause);
        menu.Items.Add(new ToolStripMenuItem("Release stuck keys", null, (_, _) => actions.ReleaseStuckKeys()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Reload config", null, (_, _) => actions.ReloadConfig()));

        // The exact file being read, spelled out. Editing a config.ini that belongs to a different
        // copy of the exe is invisible otherwise — the settings simply appear not to work.
        menu.Items.Add(new ToolStripMenuItem("Open config…", null, (_, _) => actions.OpenConfig())
        {
            ToolTipText = configPath,
        });
        menu.Items.Add(new ToolStripMenuItem(Shorten(configPath)) { Enabled = false });
        if (logEnabled)
            menu.Items.Add(new ToolStripMenuItem("Open log…", null, (_, _) => actions.OpenLog()));
        menu.Items.Add(_startup);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => actions.Exit()));

        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "MacKeyboard",
            ContextMenuStrip = menu,
            Visible = true,
        };

        _icon.DoubleClick += (_, _) => actions.ReleaseStuckKeys();
    }

    /// <summary>What the program is currently doing, e.g. "Rectangle preset: Spectacle".</summary>
    public void SetStatus(string status)
    {
        _status = status;
        Refresh();
    }

    public void SetPaused(bool paused)
    {
        _paused = paused;
        _pause.Text = paused ? "Resume" : "Pause";
        Refresh();
    }

    void Refresh()
    {
        var text = _paused
            ? "paused"
            : _status.Length == 0 ? "running" : _status;

        _header.Text = $"MacKeyboard — {text}";

        // NotifyIcon.Text throws above 63 characters.
        var tip = $"MacKeyboard — {text}";
        _icon.Text = tip.Length <= 63 ? tip : tip[..63];
    }

    public void Notify(string title, string text)
    {
        _icon.BalloonTipTitle = title;
        _icon.BalloonTipText = text;
        _icon.ShowBalloonTip(3000);
    }

    /// <summary>Keeps a long path readable in a menu by dropping the middle of it.</summary>
    static string Shorten(string path, int max = 52) =>
        path.Length <= max ? path : $"{path[..(max / 2 - 2)]}…{path[^(max / 2 - 1)..]}";

    void ToggleStartup()
    {
        var ok = _startup.Checked ? StartupTask.Disable() : StartupTask.Enable();
        if (!ok)
        {
            Notify("MacKeyboard", "Could not change the startup setting.");
            return;
        }

        _startup.Checked = !_startup.Checked;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
