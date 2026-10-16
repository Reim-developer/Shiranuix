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
/// </remarks>
/// <param name="command">The command this stands in for.</param>
internal sealed class CommandBinding(IPluginCommand command) : ICommand {
    /// <summary>
    /// Never raised: a plugin command is always runnable, because whether it
    /// can do anything is answered in the outcome.
    /// </summary>
    public event EventHandler? CanExecuteChanged {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => true;

    /// <inheritdoc />
    public void Execute(object? parameter) =>
        _ = command.ExecuteAsync(CancellationToken.None);
}
