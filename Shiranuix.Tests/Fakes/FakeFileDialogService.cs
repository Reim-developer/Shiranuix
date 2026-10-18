using Shiranuix.Services.Dialogs;

namespace Shiranuix.Tests.Fakes;

/// <summary>
/// A file dialog that answers without showing anything.
/// </summary>
public sealed class FakeFileDialogService : IFileDialogService {
    /// <summary>
    /// Path the open dialog hands back; null models the user cancelling.
    /// </summary>
    public string? OpenResult { get; set; }

    /// <summary>
    /// Path the save dialog hands back; null models the user cancelling.
    /// </summary>
    public string? SaveResult { get; set; }

    /// <summary>
    /// Names suggested to the save dialog, in order, so a test can assert the
    /// dialog was primed with the current file rather than a blank name.
    /// </summary>
    public List<string> SuggestedNames { get; } = new();

    /// <summary>
    /// Folder the folder picker hands back; null models the user cancelling.
    /// </summary>
    public string? FolderResult { get; set; }

    /// <summary>
    /// How many times the open dialog was asked, so a test can assert the
    /// question about a file came before the question about the writing.
    /// </summary>
    public int OpenAsks { get; private set; }

    /// <inheritdoc />
    public Task<string?> PickOpenFileAsync() {
        OpenAsks++;
        return Task.FromResult(OpenResult);
    }

    /// <inheritdoc />
    public Task<string?> PickSaveFileAsync(string suggestedName) {
        SuggestedNames.Add(suggestedName);
        return Task.FromResult(SaveResult);
    }

    /// <inheritdoc />
    public Task<string?> PickFolderAsync() => Task.FromResult(FolderResult);
}
