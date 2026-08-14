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
    readonly ToolStripMenuItem _pause;
    readonly ToolStripMenuItem _startup;

    public TrayIcon(TrayActions actions, bool logEnabled)
    {
        _pause = new ToolStripMenuItem("Pause", null, (_, _) => actions.TogglePause());

        _startup = new ToolStripMenuItem("Start with Windows", null, (_, _) => ToggleStartup())
        {
            Checked = StartupTask.IsEnabled(),
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("MacKeyboard") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_pause);
        menu.Items.Add(new ToolStripMenuItem("Release stuck keys", null, (_, _) => actions.ReleaseStuckKeys()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Reload config", null, (_, _) => actions.ReloadConfig()));
        menu.Items.Add(new ToolStripMenuItem("Open config…", null, (_, _) => actions.OpenConfig()));
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

    public void SetPaused(bool paused)
    {
        _pause.Text = paused ? "Resume" : "Pause";
        _icon.Text = paused ? "MacKeyboard (paused)" : "MacKeyboard";
    }

    public void Notify(string title, string text)
    {
        _icon.BalloonTipTitle = title;
        _icon.BalloonTipText = text;
        _icon.ShowBalloonTip(3000);
    }

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
