using CommunityToolkit.Mvvm.Input;
using Shiranuix.ViewModels;

namespace Shiranuix.ViewModels;

/// <summary>
/// The commands the chrome offers: the file flows behind the toolbar and the
/// recent-files list, the status bar's two choices, and the confirm prompt
/// that stands before any of them can lose work.
/// </summary>
/// <remarks>
/// Each command lands a status line — the one sentence the writer reads
/// after the act — and each file flow asks the same question first: is
/// there something here worth keeping? The view binds these by their
/// generated names (<c>NewDocumentCommand</c> and the rest), which is why
/// they stay on the class the view is bound to.
/// </remarks>
public partial class EditorViewModel {
    /// <summary>
    /// Switches the document's line-break convention, by the name the status
    /// bar shows ("LF" or "CRLF").
    /// </summary>
    /// <param name="name">The convention's name; anything else changes nothing.</param>
    /// <remarks>
    /// A choice, not a conversion: the text keeps every break as typed, and
    /// the new convention is what the next save writes them as. The document
    /// counts itself dirty over it — the file on disk would now be written
    /// differently — until the save catches up.
    /// </remarks>
    [RelayCommand]
    private void SetNewline(string? name) {
        var wanted = name switch {
            "LF" => Shiranuix.Models.Newline.Lf,
            "CRLF" => Shiranuix.Models.Newline.CrLf,
            _ => null
        };

        if (wanted is not null) {
            Document.Newline = wanted;
        }
    }

    /// <summary>
    /// Turns word wrap on and off.
    /// </summary>
    [RelayCommand]
    private void ToggleWordWrap() => WordWrap = !WordWrap;

    /// <summary>
    /// The word-wrap state as the format bar's toggle shows it.
    /// </summary>
    public string WordWrapLabel => WordWrap ? "Wrap" : "No wrap";

    /// <summary>
    /// Creates a blank document, after confirming if the current one is dirty.
    /// </summary>
    [RelayCommand]
    private async Task NewDocumentAsync() {
        if (!await ConfirmDiscardAsync()) {
            return;
        }

        Document.New();
        StatusMessage = "New document";
    }

    /// <summary>
    /// Opens a file, after confirming if the current one is dirty.
    /// </summary>
    [RelayCommand]
    private async Task OpenAsync() {
        if (!await ConfirmDiscardAsync()) {
            return;
        }

        var path = await _dialogs.PickOpenFileAsync();
        if (path is null) {
            return;
        }

        OpenPath(path);
    }

    /// <summary>
    /// Opens a folder for the file tree.
    /// </summary>
    /// <remarks>
    /// Asking again when one already stands is how the tree moves, and a
    /// writer who closes the folder question keeps what they had — the
    /// document stays as it is either way, since changing what the tree
    /// stands on touches no open page.
    /// </remarks>
    [RelayCommand]
    private async Task OpenDirectoryAsync() {
        if (Tree is not { } tree) {
            return;
        }

        await tree.ChooseFolderAsync(CancellationToken.None);
    }

    /// <summary>
    /// Opens a file without asking, used by the recent-files list.
    /// </summary>
    /// <param name="path">The file to open.</param>
    [RelayCommand]
    private async Task OpenRecentAsync(string path) {
        if (!await ConfirmDiscardAsync()) {
            return;
        }

        OpenPath(path);
    }

    /// <summary>
    /// Saves, prompting for a path only when the document has never been saved.
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync() {
        if (Document.Path is null) {
            await SaveAsAsync();
            return;
        }

        SaveTo(Document.Path);
    }

    /// <summary>
    /// Always prompts for a path, then saves there.
    /// </summary>
    [RelayCommand]
    private async Task SaveAsAsync() {
        var suggested = Document.Path is null ? "Untitled.txt" : Document.Name;
        var path = await _dialogs.PickSaveFileAsync(suggested);
        if (path is null) {
            return;
        }

        SaveTo(path);
    }

    /// <summary>
    /// Opens a file named on the command line.
    /// </summary>
    /// <param name="path">The file to open.</param>
    /// <remarks>
    /// Prompt-free by construction: <c>Shiranuix file.txt</c> is the same path
    /// Explorer takes when a document is double-clicked, and the blank document
    /// it would discard was created moments ago by this same launch and cannot
    /// have been edited.
    /// </remarks>
    public void OpenFromCommandLine(string path) => OpenPath(path);

    private void OpenPath(string path) {
        var outcome = Document.Open(path);

        StatusMessage = outcome.Succeeded
            ? $"Opened {Document.Name}"
            : $"Could not open: {outcome.Detail}";
    }

    private void SaveTo(string path) {
        var outcome = Document.Save(path);

        StatusMessage = outcome.Succeeded
            ? $"Saved {Document.Name}"
            : $"Could not save: {outcome.Detail}";
    }

    /// <summary>
    /// Asks the user whether to discard unsaved work. Clean documents do not
    /// prompt at all.
    /// </summary>
    /// <returns>True when it is safe to proceed.</returns>
    private async Task<bool> ConfirmDiscardAsync() {
        if (!Document.IsDirty) {
            return true;
        }

        var name = Document.Path is null ? "this document" : Document.Name;
        return await _confirm.ConfirmDiscardAsync(name);
    }

    /// <summary>
    /// Opens a path on the writer's behalf, with the same question about
    /// unsaved work that opening it themselves would ask.
    /// </summary>
    /// <param name="path">The file to open.</param>
    /// <returns>What came of it, including a refusal when they declined.</returns>
    /// <remarks>
    /// Internal rather than public because only the file tree opens files
    /// this way; the writer has the toolbar, the recent list and the file
    /// picker, and none of them go through here.
    /// </remarks>
    internal async Task<DocumentOutcome> OpenPathAsync(string path) {
        if (!await ConfirmDiscardAsync()) {
            return DocumentOutcome.Failed(
                "You kept the unsaved changes, so the file was not opened.");
        }

        var outcome = Document.Open(path);
        StatusMessage = outcome.Succeeded
            ? $"Opened {Document.Name}"
            : $"Could not open: {outcome.Detail}";

        return outcome;
    }

    /// <summary>
    /// Opens and closes the find bar.
    /// </summary>
    [RelayCommand]
    private void ToggleFind() => Find.IsOpen = !Find.IsOpen;
}
