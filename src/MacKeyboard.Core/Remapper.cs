namespace MacKeyboard.Core;

/// <summary>
/// The whole remapping decision, with no Win32 anywhere in it.
///
/// The design exists to make a stuck key structurally impossible, and there are two ways one can
/// happen. Both are handled here rather than at the call sites.
///
/// <b>1. A modifier we pressed is never released.</b> Answered by lazy emission: pressing a
/// physical modifier emits nothing. Only when a following key misses the binding table do we
/// "flush" — emit the mapped modifier down and hold it. A binding that hits (⌘Q, ⌘←, …) emits a
/// self-contained <see cref="Tap"/> instead, so the modifier is never pressed and there is nothing
/// to leave behind. The old AutoHotkey build leaked exactly here: it held Ctrl eagerly, then lifted
/// and re-pressed it around every shortcut, and releasing ⌘ inside that window lost the up event.
///
/// <b>2. A key the application saw go down never gets its up.</b> Every non-modifier key is in one
/// of two states — passed through (the app owns it) or captured by a binding (we own it) — and it
/// can move between them mid-press, because auto-repeat re-evaluates against whatever modifiers are
/// held right now. Both crossings inject the missing half: see <see cref="Capture"/> and
/// <see cref="Disown"/>.
///
/// Whatever still escapes is caught by <see cref="Reconcile"/>, which the app runs every 200 ms.
/// </summary>
public sealed class Remapper
{
    sealed class Slot(int physicalVk, MacMods mod)
    {
        public readonly int PhysicalVk = physicalVk;
        public readonly int MappedVk = MacModsExtensions.ToMappedVk(physicalVk);
        public readonly MacMods Mod = mod;

        /// <summary>The physical key is held.</summary>
        public bool Down;

        /// <summary>We have emitted <see cref="MappedVk"/> down and it is still held.</summary>
        public bool Flushed;
    }

    readonly Slot[] _slots =
    [
        new(Vk.LWin, MacMods.Command),
        new(Vk.RWin, MacMods.Command),
        new(Vk.LMenu, MacMods.Option),
        new(Vk.RMenu, MacMods.Option),
        new(Vk.LControl, MacMods.Control),
        new(Vk.RControl, MacMods.Control),
        new(Vk.LShift, MacMods.Shift),
        new(Vk.RShift, MacMods.Shift),
    ];

    /// <summary>Synthetic keys we are currently holding down: mapped VK → the physical VK that owns it.</summary>
    readonly Dictionary<int, int> _held = new();

    /// <summary>Non-modifier keys whose down a binding consumed, so whose up must be swallowed too.</summary>
    readonly HashSet<int> _ruleOwned = [];

    /// <summary>
    /// Non-modifier keys we let through on the way down, so the focused application believes they
    /// are held. If a binding later captures one of these mid-press, the app is owed an up.
    /// </summary>
    readonly HashSet<int> _passedDown = [];

    readonly Bindings _bindings;
    readonly BindingOptions _options;

    bool _altTabActive;

    public Remapper(BindingOptions options)
    {
        _options = options;
        _bindings = Bindings.Build(options);
    }

    /// <summary>When suspended every event passes through untouched.</summary>
    public bool Suspended { get; set; }

    /// <summary>The synthetic keys currently held. Empty is the healthy resting state.</summary>
    public IReadOnlyDictionary<int, int> Held => _held;

    public bool AltTabActive => _altTabActive;

    public int BindingCount => _bindings.Count;

    public RemapResult Process(KeyEvent e)
    {
        if (Suspended) return RemapResult.PassThrough;

        var slot = FindSlot(e.Vk);
        return slot is not null
            ? ProcessModifier(e, slot)
            : ProcessKey(e);
    }

    // ---------------------------------------------------------------- modifiers

    RemapResult ProcessModifier(KeyEvent e, Slot slot)
    {
        if (e.IsDown)
        {
            // Emit nothing — this is the lazy half of the design. Auto-repeat lands here too and
            // is equally ignored; modifiers have no use for repeat.
            slot.Down = true;
            return new RemapResult(true, []);
        }

        slot.Down = false;
        var ops = new List<OutputOp>(2);

        // The ⌘Tab session ends when the last Command key comes up. Driven by this event rather
        // than by a polling timer, which is what the previous build got wrong.
        if (_altTabActive && slot.Mod == MacMods.Command && !AnyDown(MacMods.Command))
            EndAltTab(ops);

        if (slot.Flushed)
        {
            ops.Add(new Up(slot.MappedVk));
            _held.Remove(slot.MappedVk);
            slot.Flushed = false;
        }

        return new RemapResult(true, ops);
    }

    // ---------------------------------------------------------------- ordinary keys

    RemapResult ProcessKey(KeyEvent e)
    {
        if (!e.IsDown) return ReleaseKey(e.Vk);

        var mods = CurrentMods();

        if (_options.EnableAltTab && e.Vk == Vk.Tab && (_altTabActive || IsAltTabEntry(mods)))
            return StepAltTab(mods);

        // While the switcher is up, ⌘ must stay unflushed — pressing some unrelated key mid-cycle
        // must not put Ctrl down underneath the Alt we are holding.
        if (_altTabActive) return PassThroughDown(e.Vk, null);

        // Fast path: no Mac modifier held, so nothing to decide. Plain typing never touches
        // SendInput at all, which keeps the hook callback well under LowLevelHooksTimeout.
        if (mods == MacMods.None) return PassThroughDown(e.Vk, null);

        if (_bindings.TryGet(mods, e.Vk, out var bound))
        {
            var ops = new List<OutputOp>(bound.Length + 5);

            // A binding's output is self-contained, so any modifier we are already holding from an
            // earlier flush has to come off first — otherwise ⌘C then ⌘← would send Ctrl+Home.
            // We do not re-press it: the next key that misses the table flushes again naturally.
            UnflushAll(ops);

            Capture(e.Vk, ops);
            ops.AddRange(bound);
            return new RemapResult(true, ops);
        }

        // Miss: promote the pending modifiers to real held keys, then let the original key
        // through on top of them.
        var flush = new List<OutputOp>(4);
        foreach (var slot in _slots)
        {
            if (!slot.Down || slot.Flushed) continue;
            flush.Add(new Down(slot.MappedVk, slot.PhysicalVk));
            _held[slot.MappedVk] = slot.PhysicalVk;
            slot.Flushed = true;
        }

        return PassThroughDown(e.Vk, flush);
    }

    RemapResult ReleaseKey(int vk)
    {
        // A key whose down we swallowed must have its up swallowed too, or the app sees an
        // unpaired release.
        if (_ruleOwned.Count != 0 && _ruleOwned.Remove(vk))
            return new RemapResult(true, []);

        _passedDown.Remove(vk);
        return RemapResult.PassThrough;
    }

    /// <summary>
    /// Take ownership of a key a binding is consuming. If the application already saw this key go
    /// down — it was pressed bare, and a modifier only joined in before auto-repeat re-evaluated it
    /// — that down is now orphaned, because from here on both the repeats and the final release are
    /// swallowed. Injecting the up closes it.
    /// </summary>
    void Capture(int vk, List<OutputOp> ops)
    {
        if (_passedDown.Remove(vk)) ops.Add(new Up(vk));
        _ruleOwned.Add(vk);
    }

    /// <summary>
    /// The mirror case: a key a binding used to own is now passing through untouched, because the
    /// modifier came up first. Its release must pass through as well, so ownership has to be
    /// dropped here — otherwise the up is swallowed and the key sticks down in the application.
    /// </summary>
    RemapResult PassThroughDown(int vk, IReadOnlyList<OutputOp>? ops)
    {
        if (_ruleOwned.Count != 0) _ruleOwned.Remove(vk);
        _passedDown.Add(vk);
        return new RemapResult(false, ops ?? []);
    }

    // ---------------------------------------------------------------- ⌘Tab session

    static bool IsAltTabEntry(MacMods mods) =>
        mods == MacMods.Command || mods == (MacMods.Command | MacMods.Shift);

    RemapResult StepAltTab(MacMods mods)
    {
        var ops = new List<OutputOp>(3);

        if (!_altTabActive)
        {
            // Hold Alt for the duration of the session. It is owned by the physical ⌘ that started
            // it, so if that key's release is ever missed the reconciler releases Alt within 200 ms.
            var source = FirstDown(MacMods.Command);
            if (source is null) return PassThroughDown(Vk.Tab, null);

            UnflushAll(ops);
            Capture(Vk.Tab, ops);

            ops.Add(new Down(Vk.LMenu, source.PhysicalVk));
            _held[Vk.LMenu] = source.PhysicalVk;
            _altTabActive = true;
        }
        else
        {
            Capture(Vk.Tab, ops);
        }

        // Direction is re-read on every press, so holding ⌘ and toggling Shift reverses mid-cycle.
        var reverse = (mods & MacMods.Shift) != 0;
        ops.Add(new Tap(reverse ? WinMods.Shift : WinMods.None, Vk.Tab));

        return new RemapResult(true, ops);
    }

    void EndAltTab(List<OutputOp> ops)
    {
        if (!_altTabActive) return;
        _altTabActive = false;
        if (!_held.Remove(Vk.LMenu)) return;
        ops.Add(new Up(Vk.LMenu));
    }

    // ---------------------------------------------------------------- recovery

    /// <summary>
    /// The self-healing net: release anything still held whose physical key is no longer down.
    ///
    /// The two halves ask different authorities, and the difference is not cosmetic.
    ///
    /// For keys we <b>suppress</b> — every modifier — Windows never records the press at all, so
    /// <c>GetAsyncKeyState</c> reports them as up the entire time the user is holding them. Only
    /// the hook knows, because it sees the event before it is swallowed. Asking the OS here is what
    /// tore the ⌘Tab session down 165 ms after it opened: the reconciler saw ⌘ as released while it
    /// was still held, and dropped the Alt underneath the switcher.
    ///
    /// For keys we <b>pass through</b>, Windows does record them, and its answer is the trustworthy
    /// one — it is the only thing that can notice an up we never received.
    /// </summary>
    /// <param name="isPhysicallyDown">
    /// The OS view. Consulted only for passed-through keys, where it is meaningful.
    /// </param>
    public IReadOnlyList<OutputOp> Reconcile(Func<int, bool> isPhysicallyDown)
    {
        List<OutputOp>? ops = null;

        if (_held.Count > 0)
        {
            List<(int Mapped, int Source)>? stale = null;
            foreach (var (mappedVk, sourceVk) in _held)
            {
                var alive = IsSourceDown(sourceVk);

                // The switcher survives on either ⌘, not just the one that opened it — otherwise
                // holding both and releasing one would tear the session down mid-cycle.
                if (!alive && _altTabActive && mappedVk == Vk.LMenu)
                    alive = AnyDown(MacMods.Command);

                if (alive) continue;
                (stale ??= []).Add((mappedVk, sourceVk));
            }

            if (stale is not null)
            {
                ops = new List<OutputOp>(stale.Count);
                foreach (var (mapped, source) in stale)
                {
                    ops.Add(new Up(mapped));
                    _held.Remove(mapped);

                    // The ⌘Tab session's Alt is owned by a Command key, so it is not any slot's own
                    // mapped key — match on the physical source, never on the mapped VK alone.
                    // (⌘ maps to Ctrl; matching on LMenu would wrongly clear the physical ⌃ slot.)
                    if (_altTabActive && mapped == Vk.LMenu &&
                        MacModsExtensions.ToMacMod(source) == MacMods.Command)
                        _altTabActive = false;

                    foreach (var slot in _slots)
                    {
                        if (slot.PhysicalVk != source) continue;
                        slot.Down = false;
                        if (slot.MappedVk == mapped) slot.Flushed = false;
                    }
                }
            }
        }

        if (_passedDown.Count > 0)
        {
            List<int>? orphaned = null;
            foreach (var vk in _passedDown)
                if (!isPhysicallyDown(vk))
                    (orphaned ??= []).Add(vk);

            if (orphaned is not null)
            {
                ops ??= [];
                foreach (var vk in orphaned)
                {
                    _passedDown.Remove(vk);
                    ops.Add(new Up(vk));
                }
            }
        }

        return ops ?? (IReadOnlyList<OutputOp>)[];
    }

    /// <summary>
    /// Drop everything anyone is holding on our account. Used on pause, exit, session lock, resume
    /// from sleep, hook reinstall, and the panic gesture.
    /// </summary>
    public IReadOnlyList<OutputOp> ReleaseAll()
    {
        var ops = new List<OutputOp>(_held.Count + _passedDown.Count);

        foreach (var mappedVk in _held.Keys) ops.Add(new Up(mappedVk));
        foreach (var vk in _passedDown) ops.Add(new Up(vk));

        _held.Clear();
        _passedDown.Clear();
        _ruleOwned.Clear();
        _altTabActive = false;
        foreach (var slot in _slots) slot.Flushed = false;

        return ops;
    }

    // ---------------------------------------------------------------- helpers

    void UnflushAll(List<OutputOp> ops)
    {
        foreach (var slot in _slots)
        {
            if (!slot.Flushed) continue;
            ops.Add(new Up(slot.MappedVk));
            _held.Remove(slot.MappedVk);
            slot.Flushed = false;
        }
    }

    /// <summary>
    /// Whether a physical modifier is held, according to the hook. This is the only reliable
    /// answer for a key we suppress — see <see cref="Reconcile"/>.
    /// </summary>
    public bool IsSourceDown(int vk) => FindSlot(vk)?.Down ?? false;

    /// <summary>
    /// Both Shift keys held — the panic gesture. Answered from hook state for the same reason:
    /// Shift is suppressed, so the OS would never report it and the gesture could never fire.
    /// </summary>
    public bool BothShiftsDown => IsSourceDown(Vk.LShift) && IsSourceDown(Vk.RShift);

    Slot? FindSlot(int vk)
    {
        foreach (var slot in _slots)
            if (slot.PhysicalVk == vk) return slot;
        return null;
    }

    Slot? FirstDown(MacMods mod)
    {
        foreach (var slot in _slots)
            if (slot.Down && slot.Mod == mod) return slot;
        return null;
    }

    bool AnyDown(MacMods mod) => FirstDown(mod) is not null;

    MacMods CurrentMods()
    {
        var mods = MacMods.None;
        foreach (var slot in _slots)
            if (slot.Down) mods |= slot.Mod;
        return mods;
    }
}
