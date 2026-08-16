using MacKeyboard.Core;
using Xunit;

namespace MacKeyboard.Tests;

/// <summary>
/// The regression guard for the bug this rewrite exists to kill.
///
/// A stuck modifier is not a scenario you can enumerate — it comes from an ordering nobody thought
/// of. So instead of listing cases, these tests hammer the remapper with randomised event streams
/// and assert the one property that must always hold: <b>when the user is not touching the
/// keyboard, nothing is held.</b> Neither by us (the ledger) nor by the focused app (unpaired
/// passthrough downs).
/// </summary>
public class LedgerInvariantTests
{
    static readonly int[] Keys =
    [
        Vk.LWin, Vk.RWin, Vk.LMenu, Vk.RMenu, Vk.LControl, Vk.RControl, Vk.LShift, Vk.RShift,
        Vk.Tab, Vk.Left, Vk.Right, Vk.Up, Vk.Down, Vk.Back, Vk.Delete, Vk.Return, Vk.Escape,
        Vk.Space, Vk.Oem3, Vk.Oem4, Vk.Oem6, Vk.OemPlus, Vk.OemMinus,
        Vk.A, Vk.C, Vk.D, Vk.E, Vk.F, Vk.G, Vk.H, Vk.I, Vk.J, Vk.K, Vk.M, Vk.Q, Vk.R,
        Vk.S, Vk.T, Vk.U, Vk.V, Vk.Z, Vk.D3, Vk.D4, Vk.D5,
    ];

    [Theory]
    [InlineData(RectanglePreset.Rectangle)]
    [InlineData(RectanglePreset.Spectacle)]
    public void OnceEveryPhysicalKeyIsReleased_NothingIsLeftHeld(RectanglePreset preset)
    {
        for (var seed = 0; seed < 500; seed++)
        {
            var h = Fuzz(seed, preset, out var stillDown);
            foreach (var vk in stillDown) h.Release(vk);

            Assert.True(h.Remapper.Held.Count == 0,
                $"seed {seed} ({preset}): we are still holding {Describe(h.Remapper.Held)}");

            Assert.False(h.Remapper.AltTabActive,
                $"seed {seed} ({preset}): the ⌘Tab session never ended");

            var stuck = h.KeysLeftDownInApp().ToArray();
            Assert.True(stuck.Length == 0,
                $"seed {seed} ({preset}): the app still thinks {Hex(stuck)} are held");
        }
    }

    [Theory]
    [InlineData(RectanglePreset.Rectangle)]
    [InlineData(RectanglePreset.Spectacle)]
    public void WhenKeyUpsAreLostEntirely_ReleaseAllClearsEverything(RectanglePreset preset)
    {
        // Simulates Windows dropping the hook mid-chord, or UIPI swallowing releases while an
        // elevated window has focus: the ups simply never arrive.
        //
        // The reconciler cannot settle this one. It could only notice by asking the OS, and the OS
        // does not track keys the hook suppressed — asking anyway is what used to kill the ⌘Tab
        // session. So recovery runs through ReleaseAll instead, which the hook watchdog, the panic
        // gesture and the tray item all reach.
        for (var seed = 0; seed < 300; seed++)
        {
            var h = Fuzz(seed, preset, out var stillDown);
            foreach (var vk in stillDown) h.LoseKeyUp(vk);

            h.ReleaseAll();

            Assert.True(h.Remapper.Held.Count == 0,
                $"seed {seed} ({preset}): {Describe(h.Remapper.Held)} still held");
            Assert.False(h.Remapper.AltTabActive, $"seed {seed} ({preset}): alt-tab survived");

            var stuck = h.KeysLeftDownInApp().ToArray();
            Assert.True(stuck.Length == 0,
                $"seed {seed} ({preset}): the app still thinks {Hex(stuck)} are held");
        }
    }

    [Theory]
    [InlineData(RectanglePreset.Rectangle)]
    [InlineData(RectanglePreset.Spectacle)]
    public void ReconcilingRepeatedlyNeverReleasesAKeyTheUserIsStillHolding(RectanglePreset preset)
    {
        // The OS reports every suppressed key as up, so a reconciler that believed it would strip
        // modifiers out from under the user's fingers. Ticking must change nothing while keys are
        // still held.
        for (var seed = 0; seed < 300; seed++)
        {
            var h = Fuzz(seed, preset, out _);
            var before = h.Remapper.Held.Count;

            for (var tick = 0; tick < 5; tick++)
                Assert.True(h.ReconcileAsWindowsAnswers().Count == 0,
                    $"seed {seed} ({preset}): reconcile released a key that is still held");

            Assert.Equal(before, h.Remapper.Held.Count);
        }
    }

    [Theory]
    [InlineData(RectanglePreset.Rectangle)]
    [InlineData(RectanglePreset.Spectacle)]
    public void ReleaseAll_AlwaysEmptiesTheLedger(RectanglePreset preset)
    {
        for (var seed = 0; seed < 200; seed++)
        {
            var h = Fuzz(seed, preset, out _);
            var held = h.Remapper.Held.Count;

            var ops = h.ReleaseAll();

            // Releases both what we hold and what the app is holding on our behalf, so the count
            // covers the ledger and then some.
            Assert.True(ops.Count >= held, $"seed {seed} ({preset}): released fewer keys than held");
            Assert.All(ops, o => Assert.IsType<Up>(o));
            Assert.Empty(h.Remapper.Held);
            Assert.Empty(h.ReleaseAll());   // idempotent
        }
    }

    /// <summary>
    /// Every key we press and hold is eventually released, and none is pressed twice over. This is
    /// the property the AutoHotkey build violated.
    ///
    /// Note that an <see cref="Up"/> with no preceding <see cref="Down"/> is legitimate: it is how
    /// a binding balances a key the application had already seen go down before the binding
    /// captured it.
    /// </summary>
    [Fact]
    public void EveryKeyWeHoldDown_IsEventuallyReleased()
    {
        for (var seed = 0; seed < 500; seed++)
        {
            var h = Fuzz(seed, RectanglePreset.Rectangle, out var stillDown);
            foreach (var vk in stillDown) h.Release(vk);

            var holding = new HashSet<int>();
            foreach (var op in h.Ops)
            {
                switch (op)
                {
                    case Down d:
                        Assert.True(holding.Add(d.Vk),
                            $"seed {seed}: {d.Vk:X2} pressed again while already held");
                        break;
                    case Up u:
                        holding.Remove(u.Vk);
                        break;
                }
            }

            Assert.True(holding.Count == 0, $"seed {seed}: never released {Hex(holding)}");
        }
    }

    // ---------------------------------------------------------------------------------

    static Harness Fuzz(int seed, RectanglePreset preset, out List<int> stillDown)
    {
        var rng = new Random(seed);
        var h = new Harness(new BindingOptions { Preset = preset });
        var down = new List<int>();

        for (var step = 0; step < 300; step++)
        {
            var roll = rng.Next(100);

            if (down.Count > 0 && roll < 40)
            {
                var i = rng.Next(down.Count);
                h.Release(down[i]);
                down.RemoveAt(i);
            }
            else if (down.Count > 0 && roll < 55)
            {
                h.Press(down[rng.Next(down.Count)]);   // auto-repeat of a held key
            }
            else
            {
                var vk = Keys[rng.Next(Keys.Length)];
                h.Press(vk);
                if (!down.Contains(vk)) down.Add(vk);
            }
        }

        stillDown = down;
        return h;
    }

    static string Describe(IReadOnlyDictionary<int, int> held) =>
        string.Join(", ", held.Select(kv => $"{kv.Key:X2}(from {kv.Value:X2})"));

    static string Hex(IEnumerable<int> vks) => string.Join(", ", vks.Select(v => $"{v:X2}"));
}
