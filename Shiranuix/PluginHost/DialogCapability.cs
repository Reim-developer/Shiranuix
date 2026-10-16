using Shiranuix.Plugins;
using Shiranuix.Plugins.Dialogs;
using Shiranuix.Services.Dialogs;

namespace Shiranuix.PluginHost;

/// <summary>
/// The editor's own questions, in the shape a plugin asks them.
/// </summary>
/// <remarks>
/// Two of the three work here. Confirming and choosing a folder are things the
/// editor can already do; asking for a line of text is not, because the
/// dialog window it owns has a message and buttons and no box to type in. That
/// one says so in a sentence rather than pretending, and a plugin that wants a
/// name asked for should ask inside its own panel instead — which is where a
/// file tree asks anyway, the way every editor with a tree does it.
/// </remarks>
internal sealed class DialogCapability : IPluginDialog {
    private readonly IDialogService _dialogs;
    private readonly IFileDialogService _files;

    /// <summary>
    /// Creates the dialog door over the editor's own dialogs.
    /// </summary>
    /// <param name="dialogs">Shows a question and reports the answer.</param>
    /// <param name="files">Asks for files and folders.</param>
    public DialogCapability(IDialogService dialogs, IFileDialogService files) {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _files = files ?? throw new ArgumentNullException(nameof(files));
    }

    /// <inheritdoc />
    public async Task<PluginOutcome<string>> PromptAsync(
        PluginPrompt prompt,
        CancellationToken cancellationToken) {
        await Task.CompletedTask;

        return PluginOutcome<string>.Failure(
            "This editor cannot ask for a line of text yet. Ask inside your own panel instead.");
    }

    /// <inheritdoc />
    public async Task<PluginOutcome<bool>> ConfirmAsync(
        PluginConfirm confirm,
        CancellationToken cancellationToken) {

        // Asked over a nullable bool on purpose: a plugin's yes-or-no question
        // has to be able to say "they closed it", and only a nullable answer
        // can carry that. A plain false would read as a "no" they never gave.
        var answer = await _dialogs.ShowAsync(new DialogRequest<bool?>(
            confirm.Title ?? EditorTitle,
            confirm.Message,
            [
                new DialogButton<bool?>(
                    confirm.ConfirmLabel ?? "Yes", true, IsDefault: false, IsCancel: false),
                new DialogButton<bool?>(
                    confirm.CancelLabel ?? "No", false, IsDefault: true, IsCancel: true)
            ]));

        return answer switch {
            { } agreed => PluginOutcome<bool>.From(agreed),
            null => PluginOutcome<bool>.Failure(
                "The writer closed the question without answering it.")
        };
    }

    /// <inheritdoc />
    public async Task<PluginOutcome<string>> PickFolderAsync(
        PluginFolderPrompt prompt,
        CancellationToken cancellationToken) {
        var chosen = await _files.PickFolderAsync();

        return chosen is { } folder
            ? PluginOutcome<string>.From(folder)
            : PluginOutcome<string>.Failure(
                "The writer closed the picker without choosing a folder.");
    }

    /// <summary>
    /// What the editor calls its own questions when a plugin does not say.
    /// </summary>
    private const string EditorTitle = "Shiranuix";
}
