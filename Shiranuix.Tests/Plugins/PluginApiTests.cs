using Shiranuix.Plugins;

namespace Shiranuix.Tests.Plugins;

/// <summary>
/// Tests for <see cref="PluginApi"/>: the number a plugin's manifest is
/// checked against, read from the contract's own build rather than repeated
/// beside it.
/// </summary>
public sealed class PluginApiTests {
    [Fact]
    public void The_Contract_Says_Which_Version_It_Is() {
        // Zero is what the read falls back to when it cannot find a version at
        // all, so anything else means the assembly really did answer.
        Assert.NotEqual(new Version(0, 0, 0), PluginApi.Current);
    }

    [Fact]
    public void The_Contract_Takes_Only_Its_Own_Major() {
        var itsOwn = new PluginApiRange(PluginApi.Current);

        Assert.True(itsOwn.Contains(PluginApi.Current));
        Assert.False(itsOwn.Contains(new Version(PluginApi.Current.Major + 1, 0)));
    }

    [Fact]
    public void The_Contract_Version_Has_No_Build_Metadata_Hanging_Off_It() {
        // An informational version may carry build metadata ("0.1.0+abc"), and
        // Version has nowhere to put it — a build number here would mean the
        // metadata had been read as a fourth part.
        Assert.Equal(0, PluginApi.Current.Build);
        Assert.True(PluginApi.Current >= new Version(0, 1));
    }
}
