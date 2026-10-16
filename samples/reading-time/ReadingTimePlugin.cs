using Shiranuix.Plugins;
using Shiranuix.Plugins.Commands;
using Shiranuix.Plugins.Diagnostics;
using Shiranuix.Plugins.Editor;
using Shiranuix.Plugins.UI;

namespace ReadingTime;

/// <summary>
/// The plugin the editor loads: one command that counts the document and says
/// what it would take to read it.
/// </summary>
/// <remarks>
/// <para>
/// Built on <see cref="PluginBase{TPlugin}"/>, which is optional and saves the
/// three lines every plugin would otherwise open with. What a plugin does with
/// what it is handed is still its own business: this one asks for the document
/// and carries on without it when a host will not give it, because a host with
/// no document is one where the command has nothing to say.
/// </para>
/// <para>
/// Everything else it needs, it keeps to itself. The contract hands over
/// interfaces and nothing else, which is what makes a plugin loadable at all:
/// there is nothing in the editor it can hold on to that the editor might one
/// day change the shape of.
/// </para>
/// </remarks>
public sealed class ReadingTimePlugin
    : PluginBase<ReadingTimePlugin>, IPluginCommandProvider, IPluginPanelProvider {
    private readonly EstimateCommand _estimate;

    private IEditorHost? _editor;
    private ReadingTimePanel? _panel;

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
    public IReadOnlyList<IPluginPanel> Panels => _panel is null ? [] : [_panel];

    /// <inheritdoc />
    public override async Task InitializeAsync(
        IPluginContext context,
        CancellationToken cancellationToken) {
        await base.InitializeAsync(context, cancellationToken);

        _editor = context.Capability<IEditorHost>();

        // Built here, while the plugin starts, because a control can only be
        // built on the thread that owns the window - and because the editor
        // reads Panels right after this returns.
        _panel = new ReadingTimePanel();

        Log.Write(
            PluginLogLevel.Information,
            _editor is null
                ? "No document here, so the command will have nothing to count."
                : "Ready to count.");
    }

    /// <inheritdoc />
    public override async Task ShutdownAsync(CancellationToken cancellationToken) {
        Log.Write(PluginLogLevel.Information, "Going quiet.");

        // The panel is the editor's now: it was handed over at startup, and
        // taking it back is the plugin's own business.
        _panel = null;
        _editor = null;
        await base.ShutdownAsync(cancellationToken);
    }

    /// <summary>
    /// Counts what is on the page, says it in the status line, tells any
    /// plugin listening that the count happened, and shows it in the panel.
    /// </summary>
    /// <returns>The outcome of saying it.</returns>
    internal PluginOutcome Estimate() {
        if (_editor is null) {
            return PluginOutcome.Failure("This editor offers no document to read.");
        }

        var words = ReadingTime.Words(_editor.Snapshot.Text);
        var minutes = ReadingTime.Minutes(words);

        // A kind of news the editor has never heard of, announced to whoever
        // happens to be listening for exactly this type. That is the whole of
        // it: no door in the contract, no line of core code.
        Events.Publish(new ReadingTimeMeasured(words, minutes));

        _panel?.Show(words, minutes);

        return _editor.SetStatus(ReadingTime.Sentence(words));
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
