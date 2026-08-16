using MacKeyboard.Core;
using Xunit;

namespace MacKeyboard.Tests;

public class PresetTableTests
{
    [Fact]
    public void Spectacle_OptionCommandArrows_AreTheHalves_NotVirtualDesktopSwitching()
    {
        var b = Bindings.Build(new BindingOptions { Preset = RectanglePreset.Spectacle });
        const MacMods oc = MacMods.Option | MacMods.Command;

        Assert.True(b.TryGet(oc, Vk.Left, out var left));
        Assert.Equal(new Run(AppCommand.RectLeftHalf), left[0]);
        Assert.True(b.TryGet(oc, Vk.Right, out _));
        Assert.True(b.TryGet(oc, Vk.Up, out _));
        Assert.True(b.TryGet(oc, Vk.Down, out _));
    }

    [Fact]
    public void Rectangle_ControlOptionArrows_AreTheHalves()
    {
        var b = Bindings.Build(new BindingOptions { Preset = RectanglePreset.Rectangle });
        const MacMods ko = MacMods.Control | MacMods.Option;

        Assert.True(b.TryGet(ko, Vk.Left, out var left));
        Assert.Equal(new Run(AppCommand.RectLeftHalf), left[0]);
    }

    [Fact]
    public void ThePresetsDifferOnTheChordsThatMatter()
    {
        var rect = Bindings.Build(new BindingOptions { Preset = RectanglePreset.Rectangle });
        var spec = Bindings.Build(new BindingOptions { Preset = RectanglePreset.Spectacle });

        // Option|Command+Left is a Spectacle-only chord: under Rectangle it must fall through,
        // which on Windows means Win+Ctrl+Left — switch virtual desktop.
        Assert.False(rect.TryGet(MacMods.Option | MacMods.Command, Vk.Left, out _));
        Assert.True(spec.TryGet(MacMods.Option | MacMods.Command, Vk.Left, out _));
    }
}
