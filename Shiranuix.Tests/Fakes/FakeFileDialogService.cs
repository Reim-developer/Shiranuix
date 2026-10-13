using Shiranuix.Services;

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

    /// <inheritdoc />
    public Task<string?> PickOpenFileAsync() => Task.FromResult(OpenResult);

    /// <inheritdoc />
    public Task<string?> PickSaveFileAsync(string suggestedName) {
        SuggestedNames.Add(suggestedName);
        return Task.FromResult(SaveResult);
    }
}
