using System.Diagnostics.CodeAnalysis;

namespace MacKeyboard.Core;

public enum RectanglePreset
{
    /// <summary>Rectangle's own defaults — ⌃⌥ based.</summary>
    Rectangle,

    /// <summary>Rectangle's "use Spectacle shortcuts" option — ⌥⌘ based.</summary>
    Spectacle,
}

public sealed record BindingOptions
{
    public bool EnableMacShortcuts { get; init; } = true;
    public bool EnableWindowSwitcher { get; init; } = true;
    public bool EnableRectangle { get; init; } = true;

    /// <summary>⌘Tab held-to-cycle app switching. Handled by the remapper, not the binding table.</summary>
    public bool EnableAltTab { get; init; } = true;

    public RectanglePreset Preset { get; init; } = RectanglePreset.Rectangle;
}

/// <summary>
/// The binding spec as data. Lookup is an <em>exact</em> match on the set of Mac modifiers held,
/// so ⌘Q and ⌃⌘Q are distinct entries and can never shadow each other.
///
/// Every entry produces <see cref="Tap"/>s or <see cref="Run"/>s — never a bare <see cref="Down"/>.
/// That is deliberate: a tap is emitted as one atomic batch that leaves nothing held, so no binding
/// can leak a stuck modifier no matter when the user releases the physical keys.
/// </summary>
public sealed class Bindings
{
    readonly Dictionary<(MacMods Mods, int Vk), OutputOp[]> _table;

    Bindings(Dictionary<(MacMods, int), OutputOp[]> table) => _table = table;

    public bool TryGet(MacMods mods, int vk, [MaybeNullWhen(false)] out OutputOp[] ops)
        => _table.TryGetValue((mods, vk), out ops);

    public int Count => _table.Count;

    public static Bindings Build(BindingOptions options)
    {
        var t = new Dictionary<(MacMods, int), OutputOp[]>();

        void Key(MacMods mods, int vk, params OutputOp[] ops) => t[(mods, vk)] = ops;
        void Send(MacMods mods, int vk, WinMods outMods, int outVk) => Key(mods, vk, new Tap(outMods, outVk));
        void Cmd(MacMods mods, int vk, AppCommand command) => Key(mods, vk, new Run(command));

        const MacMods C = MacMods.Command;
        const MacMods O = MacMods.Option;
        const MacMods K = MacMods.Control;
        const MacMods S = MacMods.Shift;

        if (options.EnableMacShortcuts)
        {
            // ---- Cursor movement ----
            // ⌘↑/⌘↓ are PgUp/PgDn rather than document start/end: this keyboard has no usable fn
            // key, so PgUp/PgDn would otherwise be unreachable.
            Send(C, Vk.Left, WinMods.None, Vk.Home);
            Send(C, Vk.Right, WinMods.None, Vk.End);
            Send(C, Vk.Up, WinMods.None, Vk.Prior);
            Send(C, Vk.Down, WinMods.None, Vk.Next);

            Send(O, Vk.Left, WinMods.Ctrl, Vk.Left);
            Send(O, Vk.Right, WinMods.Ctrl, Vk.Right);
            Send(O, Vk.Up, WinMods.Ctrl, Vk.Up);
            Send(O, Vk.Down, WinMods.Ctrl, Vk.Down);

            // ---- Selection ----
            Send(C | S, Vk.Left, WinMods.Shift, Vk.Home);
            Send(C | S, Vk.Right, WinMods.Shift, Vk.End);
            Send(C | S, Vk.Up, WinMods.Shift, Vk.Prior);
            Send(C | S, Vk.Down, WinMods.Shift, Vk.Next);

            Send(O | S, Vk.Left, WinMods.Ctrl | WinMods.Shift, Vk.Left);
            Send(O | S, Vk.Right, WinMods.Ctrl | WinMods.Shift, Vk.Right);
            Send(O | S, Vk.Up, WinMods.Ctrl | WinMods.Shift, Vk.Up);
            Send(O | S, Vk.Down, WinMods.Ctrl | WinMods.Shift, Vk.Down);

            // ---- Deletion ----
            Key(C, Vk.Back, new Tap(WinMods.Shift, Vk.Home), new Tap(WinMods.None, Vk.Back));
            Send(O, Vk.Back, WinMods.Ctrl, Vk.Back);
            Send(O, Vk.Delete, WinMods.Ctrl, Vk.Delete);

            // ---- Editing ----
            Send(C | S, Vk.Z, WinMods.Ctrl, Vk.Y);                          // redo
            Send(C | O | S, Vk.V, WinMods.Ctrl | WinMods.Shift, Vk.V);      // paste and match style
            Send(C, Vk.G, WinMods.None, Vk.F3);                             // find next
            Send(C | S, Vk.G, WinMods.Shift, Vk.F3);                        // find previous

            // ---- Application control ----
            Send(C, Vk.Q, WinMods.Alt, Vk.F4);                              // quit
            Send(C, Vk.M, WinMods.Win, Vk.Down);                            // minimize
            Send(C, Vk.H, WinMods.Win, Vk.Down);                            // hide
            Send(C | O, Vk.H, WinMods.Win, Vk.Home);                        // hide others
            Send(C | K, Vk.F, WinMods.None, Vk.F11);                        // full screen
            Cmd(C | O, Vk.Escape, AppCommand.LaunchTaskManager);            // force quit

            // ---- System ----
            Send(C, Vk.Space, WinMods.Win, Vk.S);                           // Spotlight
            Send(C | O, Vk.Space, WinMods.Win, Vk.E);                       // Finder search
            Send(C | K, Vk.Q, WinMods.Win, Vk.L);                           // lock screen

            // ---- Screenshots ----
            Send(C | S, Vk.D3, WinMods.Win, Vk.Snapshot);
            Send(C | S, Vk.D4, WinMods.Win | WinMods.Shift, Vk.S);
            Send(C | S, Vk.D5, WinMods.Win | WinMods.Shift, Vk.S);

            // ---- Browser ----
            Send(C, Vk.Oem4, WinMods.Alt, Vk.Left);                         // ⌘[ back
            Send(C, Vk.Oem6, WinMods.Alt, Vk.Right);                        // ⌘] forward
            Send(C | S, Vk.Oem4, WinMods.Ctrl | WinMods.Shift, Vk.Tab);     // previous tab
            Send(C | S, Vk.Oem6, WinMods.Ctrl, Vk.Tab);                     // next tab
            Send(C, Vk.R, WinMods.None, Vk.F5);                             // reload
            Send(C | S, Vk.R, WinMods.Ctrl, Vk.F5);                         // hard reload
            Send(C | O, Vk.I, WinMods.None, Vk.F12);                        // dev tools
        }

        if (options.EnableWindowSwitcher)
        {
            Cmd(C, Vk.Oem3, AppCommand.CycleWindowForward);
            Cmd(C | S, Vk.Oem3, AppCommand.CycleWindowBackward);
        }

        if (options.EnableRectangle)
        {
            // Next/previous display is the same chord in both presets.
            Cmd(K | O | C, Vk.Right, AppCommand.RectNextDisplay);
            Cmd(K | O | C, Vk.Left, AppCommand.RectPrevDisplay);

            if (options.Preset == RectanglePreset.Rectangle)
            {
                Cmd(K | O, Vk.Left, AppCommand.RectLeftHalf);
                Cmd(K | O, Vk.Right, AppCommand.RectRightHalf);
                Cmd(K | O, Vk.Up, AppCommand.RectTopHalf);
                Cmd(K | O, Vk.Down, AppCommand.RectBottomHalf);

                Cmd(K | O, Vk.U, AppCommand.RectTopLeft);
                Cmd(K | O, Vk.I, AppCommand.RectTopRight);
                Cmd(K | O, Vk.J, AppCommand.RectBottomLeft);
                Cmd(K | O, Vk.K, AppCommand.RectBottomRight);

                Cmd(K | O, Vk.D, AppCommand.RectFirstThird);
                Cmd(K | O, Vk.F, AppCommand.RectCenterThird);
                Cmd(K | O, Vk.G, AppCommand.RectLastThird);
                Cmd(K | O, Vk.E, AppCommand.RectFirstTwoThirds);
                Cmd(K | O, Vk.T, AppCommand.RectLastTwoThirds);

                Cmd(K | O, Vk.Return, AppCommand.RectMaximize);
                Cmd(K | O | S, Vk.Up, AppCommand.RectMaximizeHeight);
                Cmd(K | O, Vk.C, AppCommand.RectCenter);
                Cmd(K | O, Vk.OemPlus, AppCommand.RectLarger);
                Cmd(K | O, Vk.OemMinus, AppCommand.RectSmaller);
                Cmd(K | O, Vk.Back, AppCommand.RectRestore);
            }
            else
            {
                Cmd(O | C, Vk.Left, AppCommand.RectLeftHalf);
                Cmd(O | C, Vk.Right, AppCommand.RectRightHalf);
                Cmd(O | C, Vk.Up, AppCommand.RectTopHalf);
                Cmd(O | C, Vk.Down, AppCommand.RectBottomHalf);

                Cmd(K | C, Vk.Left, AppCommand.RectTopLeft);
                Cmd(K | C, Vk.Right, AppCommand.RectTopRight);
                Cmd(K | C | S, Vk.Left, AppCommand.RectBottomLeft);
                Cmd(K | C | S, Vk.Right, AppCommand.RectBottomRight);

                Cmd(K | O, Vk.Right, AppCommand.RectNextThird);
                Cmd(K | O, Vk.Left, AppCommand.RectPrevThird);

                Cmd(O | C, Vk.F, AppCommand.RectMaximize);
                Cmd(O | C, Vk.C, AppCommand.RectCenter);
                Cmd(O | C, Vk.Z, AppCommand.RectRestore);
            }
        }

        return new Bindings(t);
    }
}
