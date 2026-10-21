namespace Shiranuix.CoreEditor;

/// <summary>
/// The standard implementation of <see cref="IDocumentHistory"/>: two stacks
/// of deltas — what each move replaced and what took its place.
/// </summary>
/// <remarks>
/// <para>
/// A move is remembered as a span of the text it left behind and the text
/// that replaced it, not as a copy of the document: typing a word costs the
/// word, and undoing it costs the span back. The spans are positions in the
/// text as it stands when the move is taken or reapplied, so taking back the
/// last move always inverts exactly the last thing recorded — the stacks are
/// walked in step with the text by whoever owns it, and this class only
/// computes and applies.
/// </para>
/// <para>
/// Consecutive keystrokes merge into one entry the way a writer thinks of a
/// word rather than a letter: inserts that continue forward, deletes that
/// continue in one direction, each capped so a long run still undoes in
/// readable steps. Anything else — a selection replaced, a paste, a jump —
/// is its own entry.
/// </para>
/// <para>
/// Plain state over strings and positions: no I/O, and no idea what a window
/// is beyond the two numbers an entry names it by — which is why the tests
/// can still drive it with literals.
/// </para>
/// <para>
/// The class spans three files by concern: this one holds the two stacks and
/// what moves them wholesale, <c>DocumentHistory.Recording.cs</c> holds how a
/// move is recorded, and <c>DocumentHistory.Playback.cs</c> holds what comes
/// back out. One class is what keeps the stacks in step with each other —
/// every entry popped from one lands in the other, always.
/// </para>
/// </remarks>
public sealed partial class DocumentHistory : IDocumentHistory {
    /// <summary>
    /// How many moves are kept before the oldest are dropped.
    /// </summary>
    /// <remarks>
    /// A limit rather than an unbounded stack because every entry holds the
    /// text it replaced, and a long session of a large document would
    /// otherwise grow without end. A thousand moves back is far past what
    /// anyone reaches for, and dropping the oldest keeps the recent work —
    /// the part still worth taking back — intact.
    /// </remarks>
    public const int Limit = 1000;

    /// <summary>
    /// How many characters one merged run may cover before it starts a new
    /// entry.
    /// </summary>
    /// <remarks>
    /// The granularity of one undo: small enough that taking back a word is
    /// precise, large enough that undoing a paragraph is not a hundred
    /// keystrokes. Typing, forward deletes and backspaces each merge only
    /// while they stay under this together.
    /// </remarks>
    public const int MergeLimit = 32;

    private readonly List<TextChange> _undo = new();
    private readonly List<TextChange> _redo = new();

    /// <summary>
    /// One move: the span it replaced, the text that replaced it, and the
    /// window it was made in.
    /// </summary>
    private readonly record struct TextChange(
        int Start,
        string Removed,
        string Inserted,
        int WindowFrom,
        int WindowTo,
        int Caret);

    /// <inheritdoc />
    public bool CanUndo => _undo.Count > 0;

    /// <inheritdoc />
    public bool CanRedo => _redo.Count > 0;

    /// <inheritdoc />
    public void Rebase(Func<int, int> shift) {
        ArgumentNullException.ThrowIfNull(shift);

        for (var index = 0; index < _undo.Count; index++) {
            _undo[index] = Rebase(_undo[index], shift);
        }

        for (var index = 0; index < _redo.Count; index++) {
            _redo[index] = Rebase(_redo[index], shift);
        }
    }

    /// <inheritdoc />
    public void Clear() {
        _undo.Clear();
        _redo.Clear();
    }

    /// <summary>
    /// Moves one entry's window from where it stood in the old file to
    /// where the same stretch stands in the new one.
    /// </summary>
    /// <param name="change">The entry to move.</param>
    /// <param name="shift">The position mapping.</param>
    /// <returns>The entry, pointing at its new stretch of file.</returns>
    private static TextChange Rebase(TextChange change, Func<int, int> shift) =>
        change with {
            WindowFrom = shift(change.WindowFrom),
            WindowTo = shift(change.WindowTo)
        };
}
