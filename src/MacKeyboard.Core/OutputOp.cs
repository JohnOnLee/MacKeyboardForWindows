namespace MacKeyboard.Core;

/// <summary>
/// One instruction produced by the remapper. The Windows layer executes these; the remapper
/// itself never touches Win32, which is what makes it testable off-Windows.
/// </summary>
public abstract record OutputOp;

/// <summary>
/// Press a key and leave it down. Only lazily-flushed modifiers and the Alt+Tab session's Alt
/// ever use this — everything else is a self-contained <see cref="Tap"/>. <paramref name="SourceVk"/>
/// is the physical key responsible, so the reconciler can ask whether it is still held.
/// </summary>
public sealed record Down(int Vk, int SourceVk) : OutputOp
{
    public override string ToString() => $"v{Vk:X2}(from {SourceVk:X2})";
}

/// <summary>Release a key previously pressed by <see cref="Down"/>.</summary>
public sealed record Up(int Vk) : OutputOp
{
    public override string ToString() => $"^{Vk:X2}";
}

/// <summary>
/// A complete keystroke — modifiers down, key down, key up, modifiers up — emitted as one
/// atomic batch. Because it never leaves anything held, a tap can never leak a stuck modifier.
/// Every binding in <see cref="Bindings"/> produces taps.
/// </summary>
public sealed record Tap(WinMods Mods, int Vk) : OutputOp
{
    public override string ToString() => Mods == WinMods.None ? $"[{Vk:X2}]" : $"[{Mods}+{Vk:X2}]";
}

/// <summary>Hand off to the worker thread. Never executed on the hook thread.</summary>
public sealed record Run(AppCommand Command) : OutputOp
{
    public override string ToString() => $"<{Command}>";
}

/// <summary>Work that must not run inside the hook callback (window enumeration, window moves).</summary>
public enum AppCommand
{
    CycleWindowForward,
    CycleWindowBackward,
    LaunchTaskManager,

    RectLeftHalf,
    RectRightHalf,
    RectTopHalf,
    RectBottomHalf,
    RectTopLeft,
    RectTopRight,
    RectBottomLeft,
    RectBottomRight,
    RectFirstThird,
    RectCenterThird,
    RectLastThird,
    RectFirstTwoThirds,
    RectLastTwoThirds,
    RectNextThird,
    RectPrevThird,
    RectMaximize,
    RectMaximizeHeight,
    RectCenter,
    RectLarger,
    RectSmaller,
    RectRestore,
    RectNextDisplay,
    RectPrevDisplay,
}

/// <summary>
/// What the hook should do with the physical event, plus what to emit in its place.
/// <paramref name="Suppress"/> false with an empty op list is the fast path for plain typing:
/// the original event flows through untouched.
/// </summary>
public readonly record struct RemapResult(bool Suppress, IReadOnlyList<OutputOp> Ops)
{
    public static readonly RemapResult PassThrough = new(false, Array.Empty<OutputOp>());
}
