using Shiranuix.PluginHost;
using Shiranuix.Plugins;
using Shiranuix.Plugins.Diagnostics;
using Shiranuix.Plugins.Services;
using Shiranuix.Tests.Fakes;

namespace Shiranuix.Tests.Plugins;

/// <summary>
/// Tests for <see cref="PluginBase{TPlugin}"/>: the three lines it saves a
/// plugin from writing, and the rule about when they may be reached.
/// </summary>
public sealed class PluginBaseTests {
    /// <summary>A plugin that writes down everything the base class hands it.</summary>
    private sealed class QuietPlugin : PluginBase<QuietPlugin> {
        /// <summary>Whether the plugin has been started.</summary>
        public bool Started { get; private set; }

        /// <summary>Whether the plugin has been wound down.</summary>
        public bool Stopped { get; private set; }

        /// <summary>Reads each of the base class's doors.</summary>
        /// <returns>The number of doors reached, which is four.</returns>
        public int Reach() {
            Started = true;

            _ = Self;
            _ = Log;
            _ = Services;
            _ = Events;

            return 4;
        }

        /// <summary>Says something through the base class's log.</summary>
        public void Say(string message) =>
            Log.Write(PluginLogLevel.Information, message);

        /// <inheritdoc />
        public override Task ShutdownAsync(CancellationToken cancellationToken) {
            Stopped = true;
            return base.ShutdownAsync(cancellationToken);
        }
    }

    /// <summary>A context built over fakes, the way the catalog builds one.</summary>
    private static IPluginContext CreateContext(StubEditorHost editor) => new PluginContext(
        new PluginIdentity("quiet", "Quiet", new Version(1, 0, 0), "C:\\plugins\\quiet"),
        new PluginLog(new RecordingLoggerFactory(), "quiet"),
        new PluginServices(),
        new PluginEvents(new RecordingLoggerFactory()),
        new Dictionary<Type, object>());

    [Fact]
    public async Task The_Context_Is_To_Hand_Between_Starting_And_Stopping() {
        var plugin = new QuietPlugin();
        var context = CreateContext(new StubEditorHost());

        await plugin.InitializeAsync(context, CancellationToken.None);

        Assert.Equal(4, plugin.Reach());

        await plugin.ShutdownAsync(CancellationToken.None);
        Assert.True(plugin.Stopped);
    }

    [Fact]
    public async Task A_Plugin_Reaching_For_Its_Context_Too_Early_Is_Told_Why() {
        var plugin = new QuietPlugin();

        var thrown = Assert.Throws<InvalidOperationException>(() => plugin.Reach());

        Assert.Contains("InitializeAsync", thrown.Message);

        // And again after it has wound down: a plugin shutting down has no
        // business reaching the editor afterwards.
        await plugin.InitializeAsync(CreateContext(new StubEditorHost()), CancellationToken.None);
        await plugin.ShutdownAsync(CancellationToken.None);

        Assert.Throws<InvalidOperationException>(() => plugin.Reach());
    }

    [Fact]
    public async Task Starting_Twice_Is_Not_An_Error() {
        var plugin = new QuietPlugin();

        await plugin.InitializeAsync(CreateContext(new StubEditorHost()), CancellationToken.None);
        await plugin.InitializeAsync(CreateContext(new StubEditorHost()), CancellationToken.None);

        Assert.Equal(4, plugin.Reach());
    }

    [Fact]
    public async Task A_Line_A_Plugin_Writes_Reaches_The_Editors_Log_Under_Its_Own_Id() {
        var log = new RecordingLoggerFactory();
        var context = new PluginContext(
            new PluginIdentity("quiet", "Quiet", new Version(1, 0, 0), "C:\\plugins\\quiet"),
            new PluginLog(log, "quiet"),
            new PluginServices(),
            new PluginEvents(log),
            new Dictionary<Type, object>());

        var plugin = new QuietPlugin();
        await plugin.InitializeAsync(context, CancellationToken.None);

        plugin.Say("Ready to count.");

        var line = Assert.Single(log.Lines);
        Assert.Equal("plugin:quiet", line.Category);
        Assert.Contains("Ready to count.", line.Text);
    }
}
