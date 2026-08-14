using System.Diagnostics;

namespace MacKeyboard;

/// <summary>
/// "Start with Windows", registered as a scheduled task rather than a Startup-folder shortcut.
///
/// MacKeyboard requires administrator rights (without them, UIPI silently drops injected keys
/// whenever an elevated window has focus). A shortcut in the Startup folder would therefore raise
/// a UAC prompt at every single login; a scheduled task with the highest run level does not.
/// </summary>
static class StartupTask
{
    const string TaskName = "MacKeyboard";

    public static bool IsEnabled() => Run($"/query /tn \"{TaskName}\"") == 0;

    public static bool Enable()
    {
        var exe = Environment.ProcessPath;
        if (exe is null) return false;

        return Run($"/create /tn \"{TaskName}\" /tr \"\\\"{exe}\\\"\" /sc onlogon /rl highest /f") == 0;
    }

    public static bool Disable() => Run($"/delete /tn \"{TaskName}\" /f") == 0;

    static int Run(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("schtasks.exe", arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });

            if (process is null) return -1;
            process.WaitForExit(10_000);
            return process.HasExited ? process.ExitCode : -1;
        }
        catch
        {
            return -1;
        }
    }
}
