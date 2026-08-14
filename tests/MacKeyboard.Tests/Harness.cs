using MacKeyboard.Core;

namespace MacKeyboard.Tests;

/// <summary>
/// Drives the remapper the way the hook does, and models what the focused application ends up
/// seeing.
///
/// That second part is the whole point. The app's input stream is the interleaving of two things:
/// the events we inject, and the physical events we chose not to suppress. Checking either one on
/// its own proves nothing — a key can be left down by a passed-through press that our injected
/// stream never balances. <see cref="AppView"/> merges them in the order the app receives them:
/// injected first, then the original event if it was allowed through, which is the order
/// <c>OnKey</c> produces in the real program.
/// </summary>
sealed class Harness(BindingOptions? options = null)
{
    readonly HashSet<int> _physical = [];

    public Remapper Remapper { get; } = new(options ?? new BindingOptions());

    /// <summary>Everything the remapper asked to emit, in order.</summary>
    public List<OutputOp> Ops { get; } = [];

    /// <summary>Physical events the remapper let through untouched.</summary>
    public List<KeyEvent> PassedThrough { get; } = [];

    /// <summary>Every key event the focused application receives, injected and physical alike.</summary>
    public List<KeyEvent> AppView { get; } = [];

    public void Press(int vk)
    {
        _physical.Add(vk);
        Feed(KeyEvent.Down(vk));
    }

    public void Release(int vk)
    {
        _physical.Remove(vk);
        Feed(KeyEvent.Up(vk));
    }

    /// <summary>
    /// Drop a physical key without delivering its up event — what happens when Windows drops the
    /// hook for exceeding LowLevelHooksTimeout, or when UIPI eats the release.
    /// </summary>
    public void LoseKeyUp(int vk) => _physical.Remove(vk);

    public IReadOnlyList<OutputOp> Reconcile()
    {
        var ops = Remapper.Reconcile(_physical.Contains);
        Record(ops);
        return ops;
    }

    public IReadOnlyList<OutputOp> ReleaseAll()
    {
        var ops = Remapper.ReleaseAll();
        Record(ops);
        return ops;
    }

    /// <summary>
    /// Keys the application would still believe are held: the last event it saw for them was a
    /// down. Must be empty once the user has let go of everything.
    /// </summary>
    public IEnumerable<int> KeysLeftDownInApp() => AppView
        .GroupBy(e => e.Vk)
        .Where(g => g.Last().IsDown)
        .Select(g => g.Key);

    void Feed(KeyEvent e)
    {
        var result = Remapper.Process(e);
        Record(result.Ops);

        if (result.Suppress) return;

        PassedThrough.Add(e);
        AppView.Add(e);
    }

    void Record(IReadOnlyList<OutputOp> ops)
    {
        Ops.AddRange(ops);

        foreach (var op in ops)
        {
            switch (op)
            {
                case Down d:
                    AppView.Add(KeyEvent.Down(d.Vk));
                    break;

                case Up u:
                    AppView.Add(KeyEvent.Up(u.Vk));
                    break;

                case Tap t:
                    // Expanded exactly as InputSender does, via the shared helper.
                    var mods = t.Mods.ToVks();
                    foreach (var m in mods) AppView.Add(KeyEvent.Down(m));
                    AppView.Add(KeyEvent.Down(t.Vk));
                    AppView.Add(KeyEvent.Up(t.Vk));
                    for (var i = mods.Length - 1; i >= 0; i--) AppView.Add(KeyEvent.Up(mods[i]));
                    break;
            }
        }
    }
}
