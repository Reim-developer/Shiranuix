using System.Windows.Input;
using Shiranuix.Plugins;

namespace ReadingTime;

/// <summary>
/// The one command: count what is on the page and say what it would take to
/// read.
/// </summary>
/// <remarks>
/// Two ways in, as the contract asks for. The editor's menus and keyboard
/// settings bind <see cref="Binding"/>, because that is the shape they expect;
/// the editor itself calls <see cref="ExecuteAsync"/>, which carries an outcome
/// and a cancellation token. The work is written once, here.
/// </remarks>
internal sealed class EstimateCommand : IPluginCommand {
    private readonly ReadingTimePlugin _plugin;

    /// <summary>
    /// Creates the command.
    /// </summary>
    /// <param name="plugin">The plugin whose document this command reads.</param>
    public EstimateCommand(ReadingTimePlugin plugin) {
        _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));

        // Built here rather than in a field initializer because it is the
        // command itself, and a field initializer cannot say so.
        Binding = new CommandBinding(this);
    }

    /// <inheritdoc />
    public PluginCommandInfo Info { get; } = new(
        Id: "reading-time.estimate",
        Label: "Estimate reading time",
        DefaultGesture: "Ctrl+Shift+R",
        Description: "Counts the document and says how long it takes to read.");

    /// <inheritdoc />
    public ICommand Binding { get; }

    /// <inheritdoc />
    public Task<PluginOutcome> ExecuteAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        var editor = _plugin.Editor;
        if (editor is null) {
            return Task.FromResult(
                PluginOutcome.Failure("This editor offers no document to read."));
        }

        var words = ReadingTime.Words(editor.Snapshot.Text);

        return Task.FromResult(editor.SetStatus(ReadingTime.Sentence(words)));
    }
}
