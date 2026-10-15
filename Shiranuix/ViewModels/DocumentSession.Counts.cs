namespace Shiranuix.ViewModels;

/// <summary>
/// The document's counts and the gutter's run of numbers as the session
/// holds them: the collaborator's answers, read back through the doors the
/// view is bound to.
/// </summary>
/// <remarks>
/// The arithmetic belongs to <see cref="DocumentCounts"/>; what lives here
/// is the session refreshing it when the text moves and forwarding its
/// answers, so the view hears one set of property names wherever the
/// numbers come from.
/// </remarks>
public sealed partial class DocumentSession {
    /// <summary>
    /// The document's counts and the gutter's run of numbers, worked out
    /// of the text beside the scan and the ledger they are read from.
    /// </summary>
    /// <remarks>
    /// A collaborator rather than this class's own arithmetic: the counts
    /// follow from the text and nothing else, and its changes are forwarded
    /// here so the view hears them through the doors it is bound to.
    /// </remarks>
    private readonly DocumentCounts _counts = new();

    /// <summary>Word count, refreshed on every edit.</summary>
    public int WordCount => _counts.WordCount;

    /// <summary>Character count, refreshed on every edit.</summary>
    public int CharacterCount => _counts.CharacterCount;

    /// <summary>Line count, refreshed on every edit.</summary>
    public int LineCount => _counts.LineCount;

    /// <summary>
    /// The line numbers for the gutter: one per line on screen, newline
    /// separated, each the line's number in the file rather than in the
    /// window — worked out in <see cref="DocumentCounts"/> beside the
    /// counts it shares its line arithmetic with.
    /// </summary>
    public string LineNumbersText => _counts.LineNumbersText;

    private void RefreshCounts() {
        // The whole file's counts from the scan that read it, with the
        // window swapped from the file's own text to the document's — the
        // arithmetic itself is the counts class's business.
        _counts.Refresh(_scan, _ledger, _window.FileLine, Text);
    }
}
