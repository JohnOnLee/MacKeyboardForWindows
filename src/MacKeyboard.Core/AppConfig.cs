namespace MacKeyboard.Core;

/// <summary>
/// <c>config.ini</c>, kept next to the executable. Section and key names carry over from the
/// AutoHotkey build so an existing file keeps working.
/// </summary>
public sealed class AppConfig
{
    public bool ShowNotification { get; private init; } = true;
    public bool Log { get; private init; }

    public bool EnableRemapping { get; private init; } = true;
    public bool EnableMacShortcuts { get; private init; } = true;
    public bool EnableAltTab { get; private init; } = true;
    public bool EnableWindowSwitcher { get; private init; } = true;
    public bool EnableRectangle { get; private init; } = true;

    public RectanglePreset Preset { get; private init; } = RectanglePreset.Rectangle;
    public IReadOnlyCollection<string> WindowSwitcherBlacklist { get; private init; } = [];

    public BindingOptions ToBindingOptions() => new()
    {
        EnableMacShortcuts = EnableMacShortcuts,
        EnableWindowSwitcher = EnableWindowSwitcher,
        EnableRectangle = EnableRectangle,
        EnableAltTab = EnableAltTab,
        Preset = Preset,
    };

    public static string FilePath => System.IO.Path.Combine(AppContext.BaseDirectory, "config.ini");

    public static AppConfig Load() =>
        File.Exists(FilePath) ? FromLines(File.ReadAllLines(FilePath)) : new AppConfig();

    /// <summary>Parsing split from reading so it can be tested without a filesystem.</summary>
    public static AppConfig FromLines(IEnumerable<string> lines)
    {
        var ini = Parse(lines);

        return new AppConfig
        {
            ShowNotification = Bool(ini, "General", "ShowNotification", true),
            Log = Bool(ini, "General", "Log", false),

            EnableRemapping = Bool(ini, "KeyRemapping", "EnableRemapping", true),
            EnableMacShortcuts = Bool(ini, "MacShortcuts", "EnableMacShortcuts", true),
            EnableAltTab = Bool(ini, "MacShortcuts", "EnableAltTab", true),
            EnableWindowSwitcher = Bool(ini, "WindowSwitcher", "EnableWindowSwitcher", true),
            EnableRectangle = Bool(ini, "Rectangle", "EnableRectangle", true),

            Preset = Enum.TryParse<RectanglePreset>(
                Value(ini, "Rectangle", "Preset"), ignoreCase: true, out var preset)
                ? preset
                : RectanglePreset.Rectangle,

            WindowSwitcherBlacklist = (Value(ini, "WindowSwitcher", "Blacklist") ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(StripExe)
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
        };
    }

    public static void WriteDefaultIfMissing()
    {
        if (File.Exists(FilePath)) return;
        File.WriteAllText(FilePath, Template);
    }

    // The blacklist is matched against process names, which carry no extension.
    static string StripExe(string name) =>
        name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;

    static Dictionary<string, string> Parse(IEnumerable<string> lines)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var section = "";

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] is ';' or '#') continue;

            if (line[0] == '[' && line[^1] == ']')
            {
                section = line[1..^1].Trim();
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq <= 0) continue;

            // Trailing "; comment" after a value is common in the shipped template.
            var value = line[(eq + 1)..];
            var comment = value.IndexOf(';');
            if (comment >= 0) value = value[..comment];

            result[$"{section}.{line[..eq].Trim()}"] = value.Trim();
        }

        return result;
    }

    static string? Value(Dictionary<string, string> ini, string section, string key) =>
        ini.TryGetValue($"{section}.{key}", out var v) ? v : null;

    static bool Bool(Dictionary<string, string> ini, string section, string key, bool fallback) =>
        Value(ini, section, key) switch
        {
            null or "" => fallback,
            var v => v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1",
        };

    /// <summary>The file written on first run.</summary>
    public const string Template = """
        [General]
        ; Show a notification when MacKeyboard starts
        ShowNotification=true
        ; Write every physical and emitted key event to %LOCALAPPDATA%\MacKeyboard\input.log.
        ; Turn this on if a key ever sticks — the log shows exactly which press was never released.
        Log=false

        [KeyRemapping]
        ; Physical ⌘→Ctrl, ⌥→Win, ⌃→Alt. Turn off if another tool already remaps modifiers.
        EnableRemapping=true

        [MacShortcuts]
        ; ⌘←/→ Home/End, ⌘Q quit, ⌘Delete, screenshots, and the rest of the Mac bindings
        EnableMacShortcuts=true
        ; ⌘Tab held-to-cycle app switching
        EnableAltTab=true

        [WindowSwitcher]
        ; ⌘` to cycle windows within the current app
        EnableWindowSwitcher=true
        ; Apps to leave alone, comma separated, e.g. game,vmware
        Blacklist=

        [Rectangle]
        ; Window management. Repeating a half command cycles 1/2 -> 2/3 -> 1/3.
        EnableRectangle=true
        ;
        ; Chords match the PHYSICAL Mac key, not what it is remapped to. The same key has three
        ; names, so to be unambiguous:
        ;   key printed 'control' = Windows calls it Ctrl = acts as Alt
        ;   key printed 'option'  = Windows calls it Alt  = acts as Win
        ;   key printed 'command' = Windows calls it Win  = acts as Ctrl
        ;
        ; Rectangle  = Rectangle's own defaults     — halves on control+option+arrow
        ; Spectacle  = Rectangle's Spectacle option — halves on option+command+arrow
        Preset=Rectangle

        """;
}
