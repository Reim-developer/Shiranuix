using System.Windows.Input;
using Shiranuix.Plugins;
using Shiranuix.Plugins.Commands;

namespace FileTree.Commands;

/// <summary>
/// One thing the tree can be told to do, in the two shapes the editor asks
/// for.
/// </summary>
/// <remarks>
/// The editor's menus and keyboard settings bind <see cref="Binding"/>,
/// because that is the shape they expect; the editor itself calls
/// <see cref="ExecuteAsync"/>, which carries an outcome and a token. The work
/// is written once, in <see cref="Run"/>.
/// </remarks>
internal sealed class TreeCommand : IPluginCommand {
    private readonly Func<CancellationToken, Task<PluginOutcome>> _run;

    /// <summary>
    /// Creates a command.
    /// </summary>
    /// <param name="id">The command's id, namespaced by the plugin.</param>
    /// <param name="label">What the writer reads.</param>
    /// <param name="gesture">The chord it starts on, or null.</param>
    /// <param name="description">What it does, in a line, or null.</param>
    /// <param name="run">The work, which says how it went.</param>
    /// <param name="canRun">
    /// What has to be true before the work would do anything, or null when it
    /// always would. A command that needs a row selected says so here, so the
    /// palette draws it dimmed instead of letting a writer press it and be met
    /// with nothing.
    /// </param>
    public TreeCommand(
        string id,
        string label,
        string? gesture,
        string? description,
        Func<CancellationToken, Task<PluginOutcome>> run,
        Func<bool>? canRun = null) {
        Info = new PluginCommandInfo(id, label, gesture, description);
        _run = run ?? throw new ArgumentNullException(nameof(run));

        // Built here rather than in a field initializer because it is the
        // command itself, and a field initializer cannot say so.
        Binding = new CommandBinding(this, canRun);
    }

    /// <inheritdoc />
    public PluginCommandInfo Info { get; }

    /// <inheritdoc />
    public ICommand Binding { get; }

    /// <summary>
    /// Says to the editor that what this command needs has moved - a row
    /// selected, marks changed - so a menu or a palette redraws itself.
    /// </summary>
    public void CanRunMoved() => (Binding as CommandBinding)?.Moved();

    /// <summary>The work itself, for the editor to call.</summary>
    public Func<CancellationToken, Task<PluginOutcome>> Run => _run;

    /// <inheritdoc />
    public Task<PluginOutcome> ExecuteAsync(CancellationToken cancellationToken) =>
        _run(cancellationToken);
}
