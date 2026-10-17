using System.Windows.Input;
using Shiranuix.Plugins;

namespace Shiranuix.FileTree.Commands;

/// <summary>
/// What one of the tree's commands is called, and what key it starts on.
/// </summary>
/// <param name="Id">The command's id, in the naming shape the chords use.</param>
/// <param name="Label">What the writer reads in the palette.</param>
/// <param name="Description">What it does, in a line.</param>
/// <param name="DefaultGesture">
/// The chord the command starts on, or null when it starts on nothing.
/// </param>
public readonly record struct TreeCommandInfo(
    string Id,
    string Label,
    string? Description = null,
    string? DefaultGesture = null);

/// <summary>
/// One thing the tree can be told to do.
/// </summary>
/// <remarks>
/// The editor's palette lists it, its keyboard settings bind <see cref="Binding"/>,
/// and the palette draws it dimmed when <see cref="Binding"/> says it cannot
/// run. The work is written once, in <see cref="Run"/>.
/// </remarks>
public sealed class TreeCommand {
    private readonly Func<CancellationToken, Task<PluginOutcome>> _run;

    /// <summary>
    /// Creates a command.
    /// </summary>
    /// <param name="id">The command's id.</param>
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
        Info = new TreeCommandInfo(id, label, description, gesture);
        _run = run ?? throw new ArgumentNullException(nameof(run));

        // Built here rather than in a field initializer because it is the
        // command itself, and a field initializer cannot say so.
        Binding = new CommandBinding(this, canRun);
    }

    /// <summary>What this command is called.</summary>
    public TreeCommandInfo Info { get; }

    /// <summary>The command as the editor's menus, settings and palette bind it.</summary>
    public ICommand Binding { get; }

    /// <summary>The work itself.</summary>
    public Func<CancellationToken, Task<PluginOutcome>> Run => _run;

    /// <summary>
    /// Says to the editor that what this command needs has moved - a row
    /// selected, marks changed - so a menu or a palette redraws itself.
    /// </summary>
    public void CanRunMoved() => (Binding as CommandBinding)?.Moved();

    /// <summary>
    /// Does the work, and says how it went.
    /// </summary>
    /// <param name="cancellationToken">Cancelled when the editor is closing.</param>
    /// <returns>A task carrying the outcome.</returns>
    public Task<PluginOutcome> ExecuteAsync(CancellationToken cancellationToken) =>
        _run(cancellationToken);
}
