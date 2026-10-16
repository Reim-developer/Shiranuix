using System.Collections.Concurrent;
using Shiranuix.Plugins.Diagnostics;
using Shiranuix.Plugins.Dialogs;
using Shiranuix.Plugins.Editor;
using Shiranuix.Plugins.Messaging;
using Shiranuix.Plugins.Services;
using Shiranuix.Plugins.UI;

namespace Shiranuix.Plugins.Testing;

/// <summary>
/// Everything a plugin is handed at startup, standing in for the editor: the
/// document, the questions, the register, the channel and the panel column.
/// </summary>
/// <remarks>
/// <para>
/// A plugin under test needs a context and nothing else. This is that context,
/// with a door for each of the five things the contract hands out - and each
/// door is a stub a test can look at afterwards: what the plugin wrote to the
/// document, what it asked the writer, what it offered, what it announced.
/// </para>
/// <para>
/// Doors can be taken away, because a plugin has to survive a host that has
/// none: <c>harness.WithoutEditor()</c> is how a test proves a plugin still
/// loads, and still says something sensible, where there is no document.
/// </para>
/// </remarks>
public sealed class PluginHarness : IPluginContext {
    private readonly ConcurrentDictionary<Type, object> _doors = new();

    /// <summary>
    /// Creates a harness over a plugin's own identity, with every door open.
    /// </summary>
    /// <param name="id">The plugin's id, used in its identity and its log.</param>
    public PluginHarness(string id = "test-plugin") {
        Self = new PluginIdentity(id, id, new Version(1, 0, 0), AppContext.BaseDirectory);

        _doors[typeof(IEditorHost)] = Document = new StubEditorHost();
        _doors[typeof(IPluginDialog)] = Questions = new StubPluginDialog();
        _doors[typeof(IPluginPanelHost)] = Panels = new StubPanelHost();
    }

    /// <summary>The document door, and everything written to it.</summary>
    public StubEditorHost Document { get; }

    /// <summary>The question door, and everything asked over it.</summary>
    public StubPluginDialog Questions { get; }

    /// <summary>The panel column, and everything put in it.</summary>
    public StubPanelHost Panels { get; }

    /// <summary>The register, and everything offered on it.</summary>
    public StubPluginServices Offered { get; } = new();

    /// <summary>The channel, and everything announced over it.</summary>
    public StubPluginEvents Channel { get; } = new();

    /// <summary>The log the plugin writes to.</summary>
    public StubPluginLog Written { get; } = new();

    /// <inheritdoc />
    public PluginIdentity Self { get; }

    /// <inheritdoc />
    public IPluginLog Log => Written;

    /// <inheritdoc />
    public IPluginServices Services => Offered;

    /// <inheritdoc />
    public IPluginEvents Events => Channel;

    /// <summary>
    /// Takes a door away, the way a host without one would.
    /// </summary>
    /// <typeparam name="TDoor">The door to take away.</typeparam>
    /// <returns>The same harness, so calls can be chained.</returns>
    public PluginHarness Without<TDoor>()
        where TDoor : class {
        _doors.TryRemove(typeof(TDoor), out _);

        return this;
    }

    /// <inheritdoc />
    public TCapability? Capability<TCapability>()
        where TCapability : class =>
        _doors.TryGetValue(typeof(TCapability), out var door) ? (TCapability)door : null;

    /// <summary>
    /// Starts a plugin with this context and waits until it says it is ready.
    /// </summary>
    /// <typeparam name="TPlugin">The plugin to start.</typeparam>
    /// <param name="plugin">The plugin to start.</param>
    /// <returns>The plugin, started.</returns>
    public async Task<TPlugin> StartAsync<TPlugin>(TPlugin plugin)
        where TPlugin : IPlugin {
        await plugin.InitializeAsync(this, CancellationToken.None);

        return plugin;
    }

    /// <summary>
    /// Winds a plugin down, the way the editor does on the way out.
    /// </summary>
    /// <param name="plugin">The plugin to stop.</param>
    /// <returns>A task that finishes when it is done.</returns>
    public Task StopAsync(IPlugin plugin) =>
        plugin.ShutdownAsync(CancellationToken.None);
}
