using MacKeyboard.Core;
using Xunit;

namespace MacKeyboard.Tests;

public class RemapperTests
{
    // ------------------------------------------------------------------ the sequences that leak today

    [Fact]
    public void CommandQ_SendsAltF4_AndNeverPressesCtrlAtAll()
    {
        var h = new Harness();
        h.Press(Vk.LWin);
        h.Press(Vk.Q);
        h.Release(Vk.LWin);   // ⌘ released before Q — the ordering that strands Ctrl in the AHK build
        h.Release(Vk.Q);

        Assert.Contains(h.Ops, o => o is Tap { Mods: WinMods.Alt, Vk: Vk.F4 });
        Assert.DoesNotContain(h.Ops, o => o is Down);
        Assert.Empty(h.Remapper.Held);
    }

    [Fact]
    public void CommandLeft_SendsHome_AndHoldsNothing()
    {
        var h = new Harness();
        h.Press(Vk.LWin);
        h.Press(Vk.Left);
        h.Release(Vk.Left);
        h.Release(Vk.LWin);

        Assert.Contains(h.Ops, o => o is Tap { Mods: WinMods.None, Vk: Vk.Home });
        Assert.DoesNotContain(h.Ops, o => o is Down);
        Assert.Empty(h.Remapper.Held);
    }

    [Fact]
    public void CommandArrow_ReleasedOutOfOrder_DoesNotStrandTheArrowKey()
    {
        // Hold ⌘←, let go of ⌘ first, keep the arrow held so it auto-repeats, then release it.
        // The repeat passes through untouched, so its up must pass through too.
        var h = new Harness();
        h.Press(Vk.LWin);
        h.Press(Vk.Left);
        h.Release(Vk.LWin);
        h.Press(Vk.Left);     // auto-repeat, now with no modifier held
        h.Release(Vk.Left);

        Assert.Equal(2, h.PassedThrough.Count(e => e.Vk == Vk.Left));  // the repeat down and its up
        Assert.Empty(h.Remapper.Held);
    }

    // ------------------------------------------------------------------ lazy flush

    [Fact]
    public void CommandC_FlushesCtrl_AndLetsCThrough()
    {
        var h = new Harness();
        h.Press(Vk.LWin);
        Assert.Empty(h.Ops);                       // pressing ⌘ on its own emits nothing

        h.Press(Vk.C);
        Assert.Contains(h.Ops, o => o is Down { Vk: Vk.LControl, SourceVk: Vk.LWin });
        Assert.Contains(h.PassedThrough, e => e.Vk == Vk.C);

        h.Release(Vk.C);
        h.Release(Vk.LWin);
        Assert.Contains(h.Ops, o => o is Up { Vk: Vk.LControl });
        Assert.Empty(h.Remapper.Held);
    }

    [Fact]
    public void CommandC_ThenCommandLeft_LiftsCtrlBeforeSendingHome()
    {
        var h = new Harness();
        h.Press(Vk.LWin);
        h.Press(Vk.C);                             // miss → Ctrl goes down
        h.Release(Vk.C);
        h.Press(Vk.Left);                          // hit → Ctrl must come off first, or this is Ctrl+Home

        var ctrlUp = h.Ops.FindIndex(o => o is Up { Vk: Vk.LControl });
        var home = h.Ops.FindIndex(o => o is Tap { Mods: WinMods.None, Vk: Vk.Home });
        Assert.True(ctrlUp >= 0, "Ctrl was never released");
        Assert.True(ctrlUp < home, "Home was sent while Ctrl was still down");
        Assert.Empty(h.Remapper.Held);

        h.Release(Vk.Left);
        h.Release(Vk.LWin);
        Assert.Empty(h.Remapper.Held);
    }

    [Fact]
    public void CommandV_AfterARuleFired_FlushesCtrlAgain()
    {
        // ⌘ stays down across ⌘C ⌘← ⌘V. The rule in the middle drops Ctrl; the next miss must
        // put it back, otherwise paste silently stops working while ⌘ is held.
        var h = new Harness();
        h.Press(Vk.LWin);
        h.Press(Vk.C); h.Release(Vk.C);
        h.Press(Vk.Left); h.Release(Vk.Left);
        h.Press(Vk.V);

        Assert.Equal(Vk.LWin, h.Remapper.Held[Vk.LControl]);
        Assert.Contains(h.PassedThrough, e => e.Vk == Vk.V);

        h.Release(Vk.V);
        h.Release(Vk.LWin);
        Assert.Empty(h.Remapper.Held);
    }

    [Fact]
    public void PlainTyping_EmitsNothingAndPassesStraightThrough()
    {
        var h = new Harness();
        h.Press(Vk.A);
        h.Release(Vk.A);

        Assert.Empty(h.Ops);
        Assert.Equal(new[] { Vk.A, Vk.A }, h.PassedThrough.Select(e => e.Vk));
    }

    // ------------------------------------------------------------------ ⌘Tab

    [Fact]
    public void CommandTab_HoldsAltForTheSession_AndReleasesItWhenCommandComesUp()
    {
        var h = new Harness();
        h.Press(Vk.LWin);
        h.Press(Vk.Tab);
        h.Release(Vk.Tab);

        Assert.True(h.Remapper.AltTabActive);
        Assert.Equal(Vk.LWin, h.Remapper.Held[Vk.LMenu]);

        h.Press(Vk.Tab);          // cycle once more
        h.Release(Vk.Tab);
        h.Release(Vk.LWin);

        Assert.False(h.Remapper.AltTabActive);
        Assert.Empty(h.Remapper.Held);
        Assert.Equal(2, h.Ops.Count(o => o is Tap { Mods: WinMods.None, Vk: Vk.Tab }));
        Assert.Contains(h.Ops, o => o is Up { Vk: Vk.LMenu });
    }

    [Fact]
    public void CommandShiftTab_CyclesBackward_AndDirectionIsRereadEachPress()
    {
        var h = new Harness();
        h.Press(Vk.LWin);
        h.Press(Vk.LShift);
        h.Press(Vk.Tab); h.Release(Vk.Tab);
        h.Release(Vk.LShift);
        h.Press(Vk.Tab); h.Release(Vk.Tab);       // shift let go mid-session → forward again
        h.Release(Vk.LWin);

        Assert.Contains(h.Ops, o => o is Tap { Mods: WinMods.Shift, Vk: Vk.Tab });
        Assert.Contains(h.Ops, o => o is Tap { Mods: WinMods.None, Vk: Vk.Tab });
        Assert.Empty(h.Remapper.Held);
    }

    [Fact]
    public void OtherKeysDuringAltTab_DoNotPutCtrlDownUnderTheHeldAlt()
    {
        var h = new Harness();
        h.Press(Vk.LWin);
        h.Press(Vk.Tab); h.Release(Vk.Tab);
        h.Press(Vk.A); h.Release(Vk.A);

        Assert.DoesNotContain(h.Ops, o => o is Down { Vk: Vk.LControl });
        Assert.Single(h.Remapper.Held);            // only the session's Alt

        h.Release(Vk.LWin);
        Assert.Empty(h.Remapper.Held);
    }

    // ------------------------------------------------------------------ bindings

    [Fact]
    public void CommandBackspace_DeletesToStartOfLine()
    {
        var h = new Harness();
        h.Press(Vk.LWin);
        h.Press(Vk.Back);

        Assert.Contains(h.Ops, o => o is Tap { Mods: WinMods.Shift, Vk: Vk.Home });
        Assert.Contains(h.Ops, o => o is Tap { Mods: WinMods.None, Vk: Vk.Back });

        h.Release(Vk.Back);
        h.Release(Vk.LWin);
        Assert.Empty(h.Remapper.Held);
    }

    [Fact]
    public void OptionArrow_MovesByWord()
    {
        var h = new Harness();
        h.Press(Vk.LMenu);
        h.Press(Vk.Right);
        Assert.Contains(h.Ops, o => o is Tap { Mods: WinMods.Ctrl, Vk: Vk.Right });

        h.Release(Vk.Right);
        h.Release(Vk.LMenu);
        Assert.Empty(h.Remapper.Held);
    }

    [Fact]
    public void ShiftCommandArrow_SelectsToLineEdge()
    {
        var h = new Harness();
        h.Press(Vk.LShift);
        h.Press(Vk.LWin);
        h.Press(Vk.Right);
        Assert.Contains(h.Ops, o => o is Tap { Mods: WinMods.Shift, Vk: Vk.End });

        h.Release(Vk.Right);
        h.Release(Vk.LWin);
        h.Release(Vk.LShift);
        Assert.Empty(h.Remapper.Held);
    }

    [Fact]
    public void CommandQ_AndControlCommandQ_DoNotShadowEachOther()
    {
        var quit = new Harness();
        quit.Press(Vk.LWin);
        quit.Press(Vk.Q);
        Assert.Contains(quit.Ops, o => o is Tap { Mods: WinMods.Alt, Vk: Vk.F4 });

        var lockScreen = new Harness();
        lockScreen.Press(Vk.LControl);
        lockScreen.Press(Vk.LWin);
        lockScreen.Press(Vk.Q);
        Assert.Contains(lockScreen.Ops, o => o is Tap { Mods: WinMods.Win, Vk: Vk.L });
        Assert.DoesNotContain(lockScreen.Ops, o => o is Tap { Mods: WinMods.Alt, Vk: Vk.F4 });
    }

    [Fact]
    public void CommandBacktick_CyclesWindowsOfTheSameApp()
    {
        var h = new Harness();
        h.Press(Vk.LWin);
        h.Press(Vk.Oem3);
        Assert.Contains(h.Ops, o => o is Run { Command: AppCommand.CycleWindowForward });

        h.Release(Vk.Oem3);
        h.Release(Vk.LWin);
        Assert.Empty(h.Remapper.Held);
    }

    // ------------------------------------------------------------------ Rectangle

    [Fact]
    public void RectanglePreset_ControlOptionLeft_IsLeftHalf()
    {
        var h = new Harness();
        h.Press(Vk.LControl);
        h.Press(Vk.LMenu);
        h.Press(Vk.Left);

        Assert.Contains(h.Ops, o => o is Run { Command: AppCommand.RectLeftHalf });

        h.Release(Vk.Left);
        h.Release(Vk.LMenu);
        h.Release(Vk.LControl);
        Assert.Empty(h.Remapper.Held);
    }

    [Fact]
    public void SpectaclePreset_OptionCommandLeft_IsLeftHalf()
    {
        var h = new Harness(new BindingOptions { Preset = RectanglePreset.Spectacle });
        h.Press(Vk.LMenu);
        h.Press(Vk.LWin);
        h.Press(Vk.Left);

        Assert.Contains(h.Ops, o => o is Run { Command: AppCommand.RectLeftHalf });
        Assert.DoesNotContain(h.Ops, o => o is Down);   // never becomes Win+Ctrl+← (switch desktop)
    }

    [Fact]
    public void DisplayMove_IsTheSameChordInBothPresets()
    {
        foreach (var preset in new[] { RectanglePreset.Rectangle, RectanglePreset.Spectacle })
        {
            var h = new Harness(new BindingOptions { Preset = preset });
            h.Press(Vk.LControl);
            h.Press(Vk.LMenu);
            h.Press(Vk.LWin);
            h.Press(Vk.Right);

            Assert.Contains(h.Ops, o => o is Run { Command: AppCommand.RectNextDisplay });
        }
    }

    [Fact]
    public void RectangleDisabled_LeavesTheChordAlone()
    {
        var h = new Harness(new BindingOptions { EnableRectangle = false });
        h.Press(Vk.LControl);
        h.Press(Vk.LMenu);
        h.Press(Vk.Left);

        Assert.DoesNotContain(h.Ops, o => o is Run);
        Assert.Contains(h.PassedThrough, e => e.Vk == Vk.Left);
    }

    // ------------------------------------------------------------------ recovery

    [Fact]
    public void AltTabSurvivesReconcileWhileCommandIsStillHeld()
    {
        // Seen in the field as:
        //   down 5B eaten
        //   down 09 eaten -> vA4(from 5B) [09]
        //   reconciler released ^A4          <- 165 ms in, ⌘ still held
        //   up   5B eaten                    <- ⌘ only released here
        // The reconciler had asked the OS whether ⌘ was down. Windows does not record a key the
        // hook suppressed, so the answer was no, and the switcher lost its Alt immediately.
        var h = new Harness();
        h.Press(Vk.LWin);
        h.Press(Vk.Tab);
        h.Release(Vk.Tab);

        for (var tick = 0; tick < 10; tick++)
            Assert.Empty(h.ReconcileAsWindowsAnswers());

        Assert.True(h.Remapper.AltTabActive);
        Assert.Equal(Vk.LWin, h.Remapper.Held[Vk.LMenu]);

        h.Release(Vk.LWin);
        Assert.Contains(h.Ops, o => o is Up { Vk: Vk.LMenu });
        Assert.Empty(h.Remapper.Held);
    }

    [Fact]
    public void Reconcile_DoesNotTrustTheOsAboutASuppressedModifier()
    {
        var h = new Harness();
        h.Press(Vk.LWin);
        h.Press(Vk.C);                        // Ctrl is held on ⌘'s behalf

        Assert.Empty(h.ReconcileAsWindowsAnswers());
        Assert.Equal(Vk.LWin, h.Remapper.Held[Vk.LControl]);
    }

    [Fact]
    public void Reconcile_StillReleasesAPassedThroughKeyTheOsSaysIsUp()
    {
        // Passed-through keys are not suppressed, so Windows does record them and its answer is
        // meaningful. This is the one case the reconciler can still settle on its own.
        var h = new Harness();
        h.Press(Vk.LWin);
        h.Press(Vk.C);                        // 'c' itself goes through untouched
        h.LoseKeyUp(Vk.C);

        Assert.Contains(h.Reconcile(), o => o is Up { Vk: Vk.C });
    }

    [Fact]
    public void AModifierWhoseReleaseWasNeverSeen_IsRecoveredByReleaseAll()
    {
        // With no second opinion available, this is the recovery path: the hook watchdog, the
        // panic gesture and the tray all land here.
        var h = new Harness();
        h.Press(Vk.LWin);
        h.Press(Vk.C);
        h.LoseKeyUp(Vk.LWin);

        Assert.Contains(h.ReleaseAll(), o => o is Up { Vk: Vk.LControl });
        Assert.Empty(h.Remapper.Held);
    }

    [Fact]
    public void Reconcile_KeepsTheAltTabSessionAliveOnTheOtherCommandKey()
    {
        var h = new Harness();
        h.Press(Vk.LWin);
        h.Press(Vk.RWin);
        h.Press(Vk.Tab); h.Release(Vk.Tab);   // session owned by whichever ⌘ was found first
        h.Release(Vk.LWin);

        Assert.Empty(h.Reconcile());          // right ⌘ still down — do not tear the session down
        Assert.True(h.Remapper.AltTabActive);

        h.Release(Vk.RWin);
        Assert.Empty(h.Remapper.Held);
    }

    [Fact]
    public void Reconcile_DoesNotDisturbAHeldModifierThatIsStillPhysicallyDown()
    {
        var h = new Harness();
        h.Press(Vk.LWin);
        h.Press(Vk.C);

        Assert.Empty(h.Reconcile());
        Assert.Equal(Vk.LWin, h.Remapper.Held[Vk.LControl]);
    }

    [Fact]
    public void ReleaseAll_DropsEverythingHeld()
    {
        var h = new Harness();
        h.Press(Vk.LWin);
        h.Press(Vk.C);
        h.Press(Vk.LMenu);
        h.Press(Vk.A);

        var ops = h.Remapper.ReleaseAll();

        Assert.Contains(ops, o => o is Up { Vk: Vk.LControl });
        Assert.Contains(ops, o => o is Up { Vk: Vk.LWin });   // ⌥ maps to Win
        Assert.Empty(h.Remapper.Held);
    }

    [Fact]
    public void Suspended_PassesEverythingThrough()
    {
        var h = new Harness();
        h.Remapper.Suspended = true;
        h.Press(Vk.LWin);
        h.Press(Vk.Q);

        Assert.Empty(h.Ops);
        Assert.Equal(new[] { Vk.LWin, Vk.Q }, h.PassedThrough.Select(e => e.Vk));
    }
}
