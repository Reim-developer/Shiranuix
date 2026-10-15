namespace Shiranuix.Services;

/// <summary>
/// The document's own undo and redo: a stack of what each move to the text
/// did, and the reverses of them.
/// </summary>
/// <remarks>
/// <para>
/// The text box keeps its own history, but it throws that history away the
/// moment the whole text is replaced from outside — which is what opening a
/// file does, what the window does when it swaps a different span of a large
/// file into the box, and what every later window move will do. A history
/// that dies on the first navigation is not a history, so this one belongs to
/// the document instead: it reads the text it is given, works out what moved,
/// and outlives whatever replaced it.
/// </para>
/// <para>
/// Entries are deltas, not snapshots — one move stores the span it replaced
/// and the text that took its place, so a run of typing costs its characters
/// rather than a copy of the document per keystroke. Consecutive keystrokes
/// merge into one entry, the way an editor's own undo counts a word rather
/// than a letter.
/// </para>
/// <para>
/// The class is plain state over strings, so tests drive it with literals and
/// the view can hold a second implementation (or a fake) behind the same door.
/// </remarks>
public interface IDocumentHistory {
    /// <summary>
    /// Whether there is a move to take back.
    /// </summary>
    bool CanUndo { get; }

    /// <summary>
    /// Whether a taken-back move is waiting to be reapplied.
    /// </summary>
    bool CanRedo { get; }

    /// <summary>
    /// Records what a move did to the text, merging it into the last entry
    /// when it continues the same run of typing, and dropping the redo
    /// branch the way starting to type again does.
    /// </summary>
    /// <param name="before">The text as it stood.</param>
    /// <param name="after">The text as it stands.</param>
    void Note(string before, string after);

    /// <summary>
    /// Takes the last move back, leaving the redo stack holding it.
    /// </summary>
    /// <param name="current">The text as it stands.</param>
    /// <param name="text">The text to restore.</param>
    /// <returns>True when a move was taken back.</returns>
    bool TryUndo(string current, out string text);

    /// <summary>
    /// Reapplies the last taken-back move, leaving the undo stack holding it
    /// again.
    /// </summary>
    /// <param name="current">The text as it stands.</param>
    /// <param name="text">The text to restore.</param>
    /// <returns>True when a move was reapplied.</returns>
    bool TryRedo(string current, out string text);

    /// <summary>
    /// Forgets every move on both stacks.
    /// </summary>
    /// <remarks>
    /// A document replaced is a document rebegun: opening a file or starting
    /// a new one clears the moves, because taking one back would restore a
    /// text the document no longer has any claim to. Saving does not clear
    /// them — a save is a point the text passed through, not a new document.
    /// </remarks>
    void Clear();
}
