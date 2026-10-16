using System.Windows.Input;
using Shiranuix.Plugins;

namespace ReadingTime;

/// <summary>
/// The <see cref="ICommand"/> the editor's menus and keyboard settings bind
/// to, wrapping a plugin command so the plugin writes its work once.
/// </summary>
/// <remarks>
/// <see cref="ICommand"/> has nowhere to put an outcome and no way to be
/// cancelled, so this is where those two things get dropped: the command
/// itself answers with both, and anything that wants to know what happened
/// asks it directly. A plugin that needs the answer to reach a person asks the
/// editor to say it — which is what the editor door is for.
/// </remarks>
/// <param name="command">The command this stands in for.</param>
internal sealed class CommandBinding(IPluginCommand command) : ICommand {
    /// <summary>
    /// Never raised: a plugin command is always runnable, because whether it
    /// can do anything is its own business and is answered in the outcome.
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
