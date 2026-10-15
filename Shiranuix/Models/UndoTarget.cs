namespace Shiranuix.Models;

/// <summary>
/// Where a history entry was made: the stretch of file the window covered
/// when the move happened, and where the caret stood in it.
/// </summary>
/// <remarks>
/// <para>
/// A windowed document shows one span of a large file at a time, so a move
/// remembered only as its text would land wrongly the moment the window slid
/// somewhere else — undo has to put the window back where the move was made
/// before it can take the move back. The stretch is in file coordinates,
/// which never move, so the entry still names the same span of file however
/// many edits and saves have passed since.
/// </para>
/// <para>
/// The caret rides along for the same reason: an undo that restored the text
/// a thousand lines away and left the caret where the user now stands would
/// look like nothing happened. On a redo the caret is reported one step
/// forward — at the end of what the move put back — the way a writer expects
/// the cursor to land after retyping a word.
/// </para>
/// </remarks>
/// <param name="WindowFrom">The first file character the window showed.</param>
/// <param name="WindowTo">One past the last file character the window
/// showed.</param>
/// <param name="Caret">Where the caret stood, in the window's own text.</param>
public readonly record struct UndoTarget(int WindowFrom, int WindowTo, int Caret);
