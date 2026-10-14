using Shiranuix.Services.Dialogs;

namespace Shiranuix.Tests.Fakes;

/// <summary>
/// A confirmation prompt that answers from a preset, recording what it was
/// asked so a test can assert the prompt did or did not appear.
/// </summary>
public sealed class FakeConfirmDialogService : IConfirmDialogService {
    /// <summary>
    /// What the user replies. Defaults to discarding, because that is the
    /// branch under test whenever a prompt is expected.
    /// </summary>
    public bool Reply { get; set; } = true;

    /// <summary>
    /// Every file name the prompt was shown for, in order. An empty list means
    /// the prompt never appeared -- which is the assertion for clean documents.
    /// </summary>
    public List<string> AskedAbout { get; } = new();

    /// <inheritdoc />
    public Task<bool> ConfirmDiscardAsync(string fileName) {
        AskedAbout.Add(fileName);
        return Task.FromResult(Reply);
    }
}
