namespace Shiranuix.CoreEditor;

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
    /// <param name="at">The window the move was made in, and where the
    /// caret stood before it.</param>
    /// <remarks>
    /// Two moves merge only when they were made in the same window: a run
    /// continued after the window slid is a run against a different stretch
    /// of file, and taking the two back together would restore neither.
    /// </remarks>
    void Note(string before, string after, UndoTarget at);

    /// <summary>
    /// Records a move whose difference is already known: the window and
    /// caret it was made at, ready to be put back the way they were.
    /// </summary>
    /// <param name="start">Where in the old text the move began.</param>
    /// <param name="removed">What stood there.</param>
    /// <param name="inserted">What stands there now.</param>
    /// <param name="at">The window the move was made in, and where the
    /// caret stood before it.</param>
    /// <remarks>
    /// <para>
    /// One difference answers this and the edit ledger alike — both owners
    /// ask the same question of the same two texts, and at the size a
    /// window can reach, asking twice is not free. Only call it when there
    /// really was a move; <see cref="Note(string, string, UndoTarget)"/>
    /// is the way in from the two texts alone.
    /// </para>
    /// <para>
    /// Two moves merge only when they were made in the same window, as
    /// that overload says.
    /// </para>
    /// </remarks>
    void Note(int start, string removed, string inserted, UndoTarget at);

    /// <summary>
    /// Reports the window the next undo would return to, without taking
    /// anything back.
    /// </summary>
    /// <param name="target">The entry's window and caret, when there is
    /// one to take back.</param>
    /// <returns>True when there is a move to take back.</returns>
    /// <remarks>
    /// The undo itself cannot run until the window it was made in is back on
    /// screen — the entry's span means nothing against a text the window no
    /// longer shows — so the owner asks first, moves the window, and only
    /// then takes the move back.
    /// </remarks>
    bool PeekUndo(out UndoTarget target);

    /// <summary>
    /// Reports the window the next redo would return to, without reapplying
    /// anything.
    /// </summary>
    /// <param name="target">The entry's window, with the caret at the end
    /// of what the redo would put back.</param>
    /// <returns>True when there is a move to reapply.</returns>
    bool PeekRedo(out UndoTarget target);

    /// <summary>
    /// Takes every entry's window from where it stood in the old file to
    /// where the same stretch stands in the new one.
    /// </summary>
    /// <param name="shift">Maps a file position before the change to the
    /// position holding the same text after it.</param>
    /// <remarks>
    /// A save rewrites the file: what the ledger laid over it collapses in,
    /// and every position after a move slides by what the move changed.
    /// The entries' text is untouched — the window it was made in still shows
    /// the same characters — but the stretch of file it names has moved, and
    /// an undo after a save has to put the window back at the new place.
    /// </remarks>
    void Rebase(Func<int, int> shift);

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
