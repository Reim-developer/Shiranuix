namespace Shiranuix.Plugins;

/// <summary>
/// The open document, as a plugin is allowed to touch it.
/// </summary>
/// <remarks>
/// <para>
/// Every change a plugin makes goes through here rather than into the text
/// directly, which is what buys it two things it could not have on its own: the
/// change lands in the editor's own undo history as one move, so the writer
/// can take it back with Ctrl+Z without also taking back their typing; and the
/// editor can refuse a change that does not make sense — a range past the end
/// of the text — instead of taking it and showing something broken.
/// </para>
/// <para>
/// Nothing here blocks and nothing here reaches the network or the disk beyond
/// <see cref="SaveAsync"/>. A plugin that needs more asks the host for another
/// capability, and the host decides whether to offer one.
/// </para>
/// </remarks>
public interface IEditorHost {
    /// <summary>
    /// The document as it stands. Read it; change it through the methods
    /// below.
    /// </summary>
    EditorSnapshot Snapshot { get; }

    /// <summary>
    /// Raised after the document changes, on the editor's own thread. A
    /// plugin that does slow work in response should hand it to a background
    /// task rather than doing it here, because everything else in the editor
    /// is waiting on this thread.
    /// </summary>
    event Action<PluginDocumentEventArgs>? DocumentChanged;

    /// <summary>
    /// Puts text where a range was, replacing whatever the range held.
    /// </summary>
    /// <param name="range">
    /// The stretch to replace, in offsets of the current snapshot.
    /// </param>
    /// <param name="text">The text to put there.</param>
    /// <returns>
    /// The outcome: a refusal carrying a sentence when the range is not in
    /// this document.
    /// </returns>
    PluginOutcome Replace(TextRange range, string text);

    /// <summary>
    /// Puts text at the caret, the way typing would.
    /// </summary>
    /// <param name="text">The text to put there.</param>
    /// <returns>The outcome.</returns>
    PluginOutcome Insert(string text);

    /// <summary>
    /// Selects a stretch of the document, which also moves the caret to the
    /// end of it.
    /// </summary>
    /// <param name="range">The stretch to select.</param>
    /// <returns>
    /// The outcome: a refusal when the range is not in this document.
    /// </returns>
    PluginOutcome Select(TextRange range);

    /// <summary>
    /// Says something in the window's status line, where the writer will read
    /// it and where their own messages go.
    /// </summary>
    /// <param name="message">The sentence to show.</param>
    /// <returns>The outcome.</returns>
    PluginOutcome SetStatus(string message);

    /// <summary>
    /// Takes the last move back, including the moves a plugin made.
    /// </summary>
    /// <returns>The outcome: a refusal when there is nothing to take back.</returns>
    PluginOutcome Undo();

    /// <summary>
    /// Puts the last taken-back move back.
    /// </summary>
    /// <returns>The outcome: a refusal when there is nothing to put back.</returns>
    PluginOutcome Redo();

    /// <summary>
    /// Writes the document to the file it came from, asking where to put it
    /// when it has never been saved.
    /// </summary>
    /// <param name="cancellationToken">Cancelled when the editor is closing.</param>
    /// <returns>
    /// A task carrying the outcome; a refusal when the writer cancelled or
    /// the write failed.
    /// </returns>
    Task<PluginOutcome> SaveAsync(CancellationToken cancellationToken);
}
