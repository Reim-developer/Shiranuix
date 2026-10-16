using Shiranuix.Plugins;

namespace ReadingTime;

/// <summary>
/// The plugin the editor loads: one command that counts the document and says
/// what it would take to read it.
/// </summary>
/// <remarks>
/// <para>
/// The whole of a plugin's life with the editor is in
/// <see cref="InitializeAsync"/>: ask for the one thing this plugin needs, and
/// carry on without it if the host will not give it. The editor is not obliged
/// to offer the document, and a host that did not would be one where this
/// command has nothing to say — so the plugin says so and still loads.
/// </para>
/// <para>
/// Everything else it needs, it keeps to itself. The contract hands over
/// interfaces and nothing else, which is what makes a plugin loadable at all:
/// there is nothing in the editor it can be holding on to that the editor
/// might one day change the shape of.
/// </para>
/// </remarks>
public sealed class ReadingTimePlugin : IPlugin, IPluginCommandProvider {
    private readonly EstimateCommand _estimate;
    private IEditorHost? _editor;
    private IPluginLog? _log;

    /// <summary>
    /// Builds the plugin and the command it offers.
    /// </summary>
    public ReadingTimePlugin() {
        _estimate = new EstimateCommand(this);
        Commands = [_estimate];
    }

    /// <inheritdoc />
    public IReadOnlyList<IPluginCommand> Commands { get; }

    /// <inheritdoc />
    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(context);

        _log = context.Log;
        _editor = context.Capability<IEditorHost>();

        _log.Write(
            PluginLogLevel.Information,
            _editor is null
                ? "No document here, so the command will have nothing to count."
                : "Ready to count.");

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ShutdownAsync(CancellationToken cancellationToken) {
        _editor = null;
        _log?.Write(PluginLogLevel.Information, "Going quiet.");
        _log = null;

        return Task.CompletedTask;
    }

    /// <summary>
    /// The document, when the host offered one.
    /// </summary>
    /// <remarks>
    /// Internal to the plugin, not to the editor: a capability handed to a
    /// plugin stays inside that plugin's assembly, and the editor keeps no
    /// reference back to it.
    /// </remarks>
    internal IEditorHost? Editor => _editor;
}
