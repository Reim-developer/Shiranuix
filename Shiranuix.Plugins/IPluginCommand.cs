using System.Windows.Input;

namespace Shiranuix.Plugins;

/// <summary>
/// One thing a plugin can be asked to do: a word count, a sort, a jump to the
/// top of the document.
/// </summary>
/// <remarks>
/// <para>
/// Two ways in, on purpose. <see cref="Binding"/> is what the editor's menus
/// and keyboard settings bind to, because that is the shape they expect;
/// <see cref="ExecuteAsync"/> is what the host calls when it wants an outcome
/// and a cancellation token, which <see cref="ICommand"/> cannot carry.
/// A plugin writes the work once and lets the binding call into it.
/// </para>
/// <para>
/// Whatever the command does to the document, it does through
/// <see cref="IEditorHost"/>, so it lands in the undo history the same way the
/// writer's own typing does.
/// </para>
/// </remarks>
public interface IPluginCommand {
    /// <summary>What this command is called, and what key it starts on.</summary>
    PluginCommandInfo Info { get; }

    /// <summary>
    /// The command as the editor's menus and keyboard settings bind to.
    /// </summary>
    ICommand Binding { get; }

    /// <summary>
    /// Does the work, and says how it went.
    /// </summary>
    /// <param name="cancellationToken">Cancelled when the editor is closing.</param>
    /// <returns>A task carrying the outcome.</returns>
    Task<PluginOutcome> ExecuteAsync(CancellationToken cancellationToken);
}
