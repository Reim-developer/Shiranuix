namespace Shiranuix.Plugins;

/// <summary>
/// The question a plugin asks the writer, and the answer it gets.
/// </summary>
/// <remarks>
/// Dialogs are a capability rather than a part of the plugin contract for the
/// same reason the editor is: a host that runs plugins in a background agent,
/// or in a window that has not opened yet, can simply not offer this one, and
/// the plugin that needs it finds out with a null rather than a crash.
/// </remarks>
public interface IPluginDialog {
    /// <summary>
    /// Asks the writer for a line of text.
    /// </summary>
    /// <param name="prompt">What to ask, and where to start typing.</param>
    /// <param name="cancellationToken">Cancelled when the editor is closing.</param>
    /// <returns>
    /// A task carrying the text that was typed, or a refusal when the writer
    /// closed the question without answering it.
    /// </returns>
    Task<PluginOutcome<string>> PromptAsync(
        PluginPrompt prompt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Asks the writer a yes-or-no question.
    /// </summary>
    /// <param name="confirm">What to ask, and what the two buttons say.</param>
    /// <param name="cancellationToken">Cancelled when the editor is closing.</param>
    /// <returns>
    /// A task carrying the answer: true for yes, false for no, and a refusal
    /// when the writer closed the question without answering it.
    /// </returns>
    Task<PluginOutcome<bool>> ConfirmAsync(
        PluginConfirm confirm,
        CancellationToken cancellationToken);
}
