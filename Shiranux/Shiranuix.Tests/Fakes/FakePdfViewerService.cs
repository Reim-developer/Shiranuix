using Shiranuix.Services;
using Shiranuix.ViewModels;

namespace Shiranuix.Tests.Fakes;

/// <summary>
/// A PDF viewer that answers without showing anything.
/// </summary>
public sealed class FakePdfViewerService : IPdfViewerService {
    /// <summary>
    /// What the viewer answers with; a failure models a paper it cannot read.
    /// </summary>
    public DocumentOutcome Answer { get; set; } = DocumentOutcome.Done;

    /// <summary>
    /// Every path it was asked to read, in order. An empty list means the
    /// viewer was never reached.
    /// </summary>
    public List<string> Opened { get; } = new();

    /// <inheritdoc />
    public DocumentOutcome Show(string path) {
        ArgumentNullException.ThrowIfNull(path);

        Opened.Add(path);
        return Answer;
    }
}
