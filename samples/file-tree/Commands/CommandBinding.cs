using System.Windows.Input;
using Shiranuix.Plugins;
using Shiranuix.Plugins.Commands;

namespace FileTree.Commands;

/// <summary>
/// The <see cref="ICommand"/> the editor's menus and keyboard settings bind
/// to, wrapping a plugin command so the plugin writes its work once.
/// </summary>
/// <remarks>
/// <see cref="ICommand"/> has nowhere to put an outcome and no way to be
/// cancelled, so this is where those two things get dropped: the command
/// itself answers with both, and anything that wants to know what happened
/// asks it directly.
///
/// What it does carry is whether the command can be run at all, which is the
/// one question <see cref="ICommand"/> has an answer for and which the
/// editor's palette and menus both ask. A tree command that needs a row
/// selected says so, and a row in the palette that cannot be run is drawn
/// dimmed rather than waiting to be pressed and doing nothing.
/// </remarks>
/// <param name="command">The command this stands in for.</param>
/// <param name="canRun">
/// What has to be true before the work would do anything, or null when it
/// always would.
/// </param>
internal sealed class CommandBinding(IPluginCommand command, Func<bool>? canRun = null) : ICommand {
    private readonly Func<bool>? _canRun = canRun;

    /// <summary>
    /// Raised when what the command needs has changed - a row selected, marks
    /// put down or cleared - so a menu redraws itself. A handler that asks
    /// and finds nothing is not a broken menu; a handler that is never called
    /// is.
    /// </summary>
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => _canRun?.Invoke() ?? true;

    /// <summary>
    /// Says the answer may have moved. Called by the tree whenever the state a
    /// command depends on changes.
    /// </summary>
    public void Moved() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc />
    public void Execute(object? parameter) {
        if (CanExecute(parameter)) {
            _ = command.ExecuteAsync(CancellationToken.None);
        }
    }
}
