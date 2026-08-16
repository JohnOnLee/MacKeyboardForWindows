using MacKeyboard.Core;
using Xunit;

namespace MacKeyboard.Tests;

/// <summary>
/// Config parsing, tested against the file the program actually writes. An unbound Rectangle chord
/// falls through to Windows and does something unrelated, so a preset that silently fails to load
/// looks exactly like a broken binding — worth pinning down here rather than at a work machine.
/// </summary>
public class AppConfigTests
{
    static string[] Lines(string text) => text.Split('\n');

    [Fact]
    public void TheShippedTemplateParsesToTheDocumentedDefaults()
    {
        var config = AppConfig.FromLines(Lines(AppConfig.Template));

        Assert.Equal(RectanglePreset.Rectangle, config.Preset);
        Assert.True(config.EnableRectangle);
        Assert.True(config.EnableMacShortcuts);
        Assert.True(config.EnableAltTab);
        Assert.True(config.EnableWindowSwitcher);
        Assert.False(config.Log);
    }

    [Fact]
    public void EditingThePresetInTheShippedTemplateTakesEffect()
    {
        // Exactly what a user does: open the generated file, change the one word, save.
        var edited = AppConfig.Template.Replace("Preset=Rectangle", "Preset=Spectacle");
        var config = AppConfig.FromLines(Lines(edited));

        Assert.Equal(RectanglePreset.Spectacle, config.Preset);
    }

    [Theory]
    [InlineData("Preset=Spectacle")]
    [InlineData("Preset=spectacle")]
    [InlineData("Preset = Spectacle")]
    [InlineData("Preset=Spectacle   ")]
    [InlineData("  Preset=Spectacle")]
    [InlineData("Preset=Spectacle ; switched")]
    public void PresetIsReadRegardlessOfSpacingCaseOrTrailingComment(string line)
    {
        var config = AppConfig.FromLines(["[Rectangle]", line]);
        Assert.Equal(RectanglePreset.Spectacle, config.Preset);
    }

    [Fact]
    public void CommentedOutLinesDoNotCount()
    {
        var config = AppConfig.FromLines([
            "[Rectangle]",
            "; Preset=Rectangle",
            "Preset=Spectacle",
        ]);

        Assert.Equal(RectanglePreset.Spectacle, config.Preset);
    }

    [Fact]
    public void AKeyRepeatedInTheFileTakesItsLastValue()
    {
        // Adding a line instead of editing the existing one is an easy mistake, and the loser is
        // whichever comes first. Pinned so the behaviour is at least known.
        var config = AppConfig.FromLines([
            "[Rectangle]",
            "Preset=Spectacle",
            "Preset=Rectangle",
        ]);

        Assert.Equal(RectanglePreset.Rectangle, config.Preset);
    }

    [Fact]
    public void APresetUnderTheWrongSectionIsIgnored()
    {
        var config = AppConfig.FromLines(["[General]", "Preset=Spectacle"]);
        Assert.Equal(RectanglePreset.Rectangle, config.Preset);
    }

    [Fact]
    public void CarriageReturnsFromAWindowsEditorDoNotBreakParsing()
    {
        // File.ReadAllLines strips these, but a hand-built line array or a stray \r in the middle
        // of a file would not — and "Spectacle\r" does not parse as an enum.
        var config = AppConfig.FromLines(["[Rectangle]\r", "Preset=Spectacle\r"]);
        Assert.Equal(RectanglePreset.Spectacle, config.Preset);
    }

    [Fact]
    public void AnUnknownPresetFallsBackRatherThanThrowing()
    {
        var config = AppConfig.FromLines(["[Rectangle]", "Preset=Nonsense"]);
        Assert.Equal(RectanglePreset.Rectangle, config.Preset);
    }

    [Fact]
    public void BlacklistEntriesLoseTheirExeSuffix()
    {
        var config = AppConfig.FromLines([
            "[WindowSwitcher]",
            "Blacklist=game.exe, vmware , Notepad",
        ]);

        Assert.Contains("game", config.WindowSwitcherBlacklist);
        Assert.Contains("vmware", config.WindowSwitcherBlacklist);
        Assert.Contains("notepad", config.WindowSwitcherBlacklist);
    }
}
