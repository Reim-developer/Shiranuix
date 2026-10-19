namespace Shiranuix.CoreEditor;

/// <summary>
/// The document as the window navigator reaches it: where it stands in the
/// file, the text it shows, the caret it carries, and the two moves the
/// navigator asks of it — put a planned window on screen, and land the
/// caret without reading the landing as another slide.
/// </summary>
/// <remarks>
/// A seam rather than a service. The navigator plans the window's travel
/// over the file, but only the document can put a window on screen: the
/// text swap reaches the history and the ledger's own window, which are the
/// document's business and no one else's. The document implements this, so
/// the navigator depends on the shape of that conversation instead of on
/// the document itself — and the tests can drive it either way.
/// </remarks>
public interface IWindowStage {
    /// <summary>The document's file, or null for an unsaved one.</summary>
    string? Path { get; }

    /// <summary>The window's text as the document shows it.</summary>
    string Text { get; }

    /// <summary>The caret's index into that text.</summary>
    int CaretIndex { get; }

    /// <summary>What a scan said of the whole file.</summary>
    ScanResult Scan { get; }

    /// <summary>
    /// Puts a freshly planned window on screen: the file's own text with
    /// every move standing in the window laid back over it.
    /// </summary>
    /// <param name="plan">The window the engine worked out.</param>
    void Adopt(WindowPlan plan);

    /// <summary>
    /// Lands the caret where the navigator put it, without the change
    /// reading as the writer asking for another slide.
    /// </summary>
    /// <param name="index">The caret's index in the window's text.</param>
    void SetCaretQuietly(int index);
}
