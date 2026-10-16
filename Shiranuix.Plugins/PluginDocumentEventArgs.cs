namespace Shiranuix.Plugins;

/// <summary>
/// What happened to the document.
/// </summary>
public enum PluginDocumentEvent {
    /// <summary>A document was opened, or a new one was started.</summary>
    Opened,

    /// <summary>The text, the caret or the selection moved.</summary>
    Changed,

    /// <summary>The document was written to its file.</summary>
    Saved,

    /// <summary>The document was closed and nothing of it is left.</summary>
    Closed
}

/// <summary>
/// One thing that happened to the document, and the document as it stood
/// afterwards.
/// </summary>
/// <param name="Kind">What happened.</param>
/// <param name="Snapshot">The document after it happened.</param>
public readonly record struct PluginDocumentEventArgs(
    PluginDocumentEvent Kind,
    EditorSnapshot Snapshot);
