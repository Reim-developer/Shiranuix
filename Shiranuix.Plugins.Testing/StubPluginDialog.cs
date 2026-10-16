using Shiranuix.Plugins;
using Shiranuix.Plugins.Dialogs;

namespace Shiranuix.Plugins.Testing;

/// <summary>
/// A dialog door that answers from a script instead of asking a writer.
/// </summary>
/// <remarks>
/// Every question is written down, so a test can assert both what was asked -
/// the wording, the labels, the default - and what the plugin did with the
/// answer. An unanswered question returns a refusal, which is what the editor
/// does when a writer closes a dialog without answering it.
/// </remarks>
public sealed class StubPluginDialog : IPluginDialog {
    /// <summary>The answer <see cref="ConfirmAsync"/> gives.</summary>
    public bool? ConfirmAnswer { get; set; } = true;

    /// <summary>The answer <see cref="PromptAsync"/> gives.</summary>
    public string? PromptAnswer { get; set; }

    /// <summary>The folder <see cref="PickFolderAsync"/> gives.</summary>
    public string? FolderAnswer { get; set; }

    /// <summary>Every question asked, in order.</summary>
    public List<AskedQuestion> Questions { get; } = [];

    /// <inheritdoc />
    public Task<PluginOutcome<string>> PromptAsync(
        PluginPrompt prompt,
        CancellationToken cancellationToken) {
        Questions.Add(AskedQuestion.For(prompt.Message, prompt.Title));

        return Task.FromResult(PromptAnswer is { } typed
            ? PluginOutcome<string>.From(typed)
            : PluginOutcome<string>.Failure("Nobody answered the question."));
    }

    /// <inheritdoc />
    public Task<PluginOutcome<bool>> ConfirmAsync(
        PluginConfirm confirm,
        CancellationToken cancellationToken) {
        Questions.Add(AskedQuestion.For(confirm.Message, confirm.Title));

        return Task.FromResult(ConfirmAnswer is { } agreed
            ? PluginOutcome<bool>.From(agreed)
            : PluginOutcome<bool>.Failure("Nobody answered the question."));
    }

    /// <inheritdoc />
    public Task<PluginOutcome<string>> PickFolderAsync(
        PluginFolderPrompt prompt,
        CancellationToken cancellationToken) {
        Questions.Add(AskedQuestion.For(prompt.Message, prompt.Title));

        return Task.FromResult(FolderAnswer is { } folder
            ? PluginOutcome<string>.From(folder)
            : PluginOutcome<string>.Failure("Nobody chose a folder."));
    }

    /// <summary>
    /// One question, in the words it was asked.
    /// </summary>
    /// <param name="Message">What it had to say.</param>
    /// <param name="Title">What it was called.</param>
    public sealed record AskedQuestion(string Message, string? Title) {
        /// <summary>Records a question's two fields.</summary>
        /// <param name="message">What it had to say.</param>
        /// <param name="title">What it was called.</param>
        /// <returns>The recorded question.</returns>
        public static AskedQuestion For(string message, string? title) => new(message, title);
    }
}
