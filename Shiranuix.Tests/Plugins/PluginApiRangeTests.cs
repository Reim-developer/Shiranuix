using Shiranuix.Plugins;

namespace Shiranuix.Tests.Plugins;

/// <summary>
/// Tests for <see cref="PluginApiRange"/>: what a plugin may claim to work
/// with, and what the host will let in.
/// </summary>
public sealed class PluginApiRangeTests {
    [Fact]
    public void A_Minimum_Takes_Its_Own_Major_And_Every_Later_Minor() {
        var range = new PluginApiRange(new Version(0, 1));

        Assert.True(range.Contains(new Version(0, 1)));
        Assert.True(range.Contains(new Version(0, 2, 7)));
    }

    [Fact]
    public void A_Minimum_Refuses_Earlier_Minors() {
        var range = new PluginApiRange(new Version(0, 2));

        Assert.False(range.Contains(new Version(0, 1, 9)));
    }

    [Fact]
    public void The_Major_Is_The_Promise_And_It_Is_Kept() {
        var range = new PluginApiRange(new Version(0, 9));

        // Newer minor within the line: still the same contract.
        Assert.True(range.Contains(new Version(0, 30)));

        // A major the plugin never claimed: refused rather than half-worked.
        Assert.False(range.Contains(new Version(1, 0)));
    }

    [Fact]
    public void A_Maximum_Ends_The_Window() {
        var range = new PluginApiRange(new Version(0, 1), new Version(0, 3));

        Assert.True(range.Contains(new Version(0, 3, 4)));
        Assert.False(range.Contains(new Version(0, 4)));
    }

    [Fact]
    public void A_Window_Whose_Maximum_Is_Below_Its_Minimum_Is_Not_A_Window() {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PluginApiRange(new Version(0, 3), new Version(0, 1)));
    }

    [Fact]
    public void Reading_The_Text_Form_Takes_A_Bare_Minimum() {
        var range = PluginApiRange.Parse("0.1");

        Assert.Equal(new Version(0, 1), range.Minimum);
        Assert.Null(range.Maximum);
    }

    [Fact]
    public void Reading_The_Text_Form_Takes_A_Window() {
        var range = PluginApiRange.Parse("0.1 - 0.3");

        Assert.Equal(new Version(0, 1), range.Minimum);
        Assert.Equal(new Version(0, 3), range.Maximum);
    }

    [Fact]
    public void Reading_The_Text_Form_Takes_A_Star_As_Any_Version() {
        var range = PluginApiRange.Parse("*");

        Assert.Equal(new Version(0, 0), range.Minimum);
        Assert.Null(range.Maximum);
    }

    [Theory]
    [InlineData("")]
    [InlineData("soon")]
    [InlineData("0.1 - 0.3 - 0.4")]
    [InlineData("0.1 - later")]
    public void Text_That_Is_Not_A_Range_Is_Refused(string text) {
        Assert.Throws<ArgumentException>(() => PluginApiRange.Parse(text));
    }
}
