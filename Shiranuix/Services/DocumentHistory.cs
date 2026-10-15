using Shiranuix.Utilities;

namespace Shiranuix.Services;

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
/// Stateless with respect to the window and the file: strings in, strings
/// out, no I/O, which is why the tests can drive it with literals.
/// </remarks>
public sealed class DocumentHistory : IDocumentHistory {
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
    /// One move: the span it replaced, and the text that replaced it.
    /// </summary>
    private readonly record struct TextChange(int Start, string Removed, string Inserted);

    /// <inheritdoc />
    public bool CanUndo => _undo.Count > 0;

    /// <inheritdoc />
    public bool CanRedo => _redo.Count > 0;

    /// <inheritdoc />
    public void Note(string before, string after) {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        if (!TryDiff(before, after, out var change)) {
            return;
        }

        // Typing anew forks the timeline: the moves waiting on the redo
        // branch describe a text that is about to stop being reachable.
        _redo.Clear();
        Push(change);
    }

    /// <inheritdoc />
    public bool TryUndo(string current, out string text) {
        ArgumentNullException.ThrowIfNull(current);

        if (_undo.Count == 0) {
            text = current;
            return false;
        }

        var entry = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);

        // The inverse of the move: the text it left behind takes the place
        // of the text it wrote. A span past the end of this text would mean
        // the stacks and the document had parted company — drop the entry
        // rather than write outside the text.
        if (entry.Start > current.Length
                || entry.Start + entry.Inserted.Length > current.Length) {
            text = current;
            return false;
        }

        text = string.Concat(
            current.AsSpan(0, entry.Start),
            entry.Removed,
            current.AsSpan(entry.Start + entry.Inserted.Length));
        _redo.Add(entry);

        return true;
    }

    /// <inheritdoc />
    public bool TryRedo(string current, out string text) {
        ArgumentNullException.ThrowIfNull(current);

        if (_redo.Count == 0) {
            text = current;
            return false;
        }

        var entry = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);

        if (entry.Start > current.Length
                || entry.Start + entry.Removed.Length > current.Length) {
            text = current;
            return false;
        }

        text = string.Concat(
            current.AsSpan(0, entry.Start),
            entry.Inserted,
            current.AsSpan(entry.Start + entry.Removed.Length));
        _undo.Add(entry);

        return true;
    }

    /// <inheritdoc />
    public void Clear() {
        _undo.Clear();
        _redo.Clear();
    }

    /// <summary>
    /// Works out what one move changed, as this history's own entry.
    /// </summary>
    /// <param name="before">The text as it stood.</param>
    /// <param name="after">The text as it stands.</param>
    /// <param name="change">The move, when there was one.</param>
    /// <returns>True when the text moved at all.</returns>
    /// <remarks>
    /// The shared <see cref="TextDelta"/> answers the same question for the
    /// edit ledger; this wrapper only puts the answer in the history's shape.
    /// </remarks>
    private static bool TryDiff(string before, string after, out TextChange change) {
        if (!TextDelta.TryDiff(before, after, out var start, out var removed, out var inserted)) {
            change = default;
            return false;
        }

        change = new TextChange(start, removed, inserted);
        return true;
    }

    /// <summary>
    /// Adds a move to the undo stack, merging it into the last one when it
    /// continues the same run of typing or deleting, and dropping the oldest
    /// moves past <see cref="Limit"/>.
    /// </summary>
    /// <param name="change">The move to record.</param>
    private void Push(TextChange change) {
        if (_undo.Count > 0 && TryMerge(_undo[^1], change, out var merged)) {
            _undo[^1] = merged;
            return;
        }

        _undo.Add(change);

        if (_undo.Count > Limit) {
            _undo.RemoveAt(0);
        }
    }

    /// <summary>
    /// Decides whether a move continues the entry before it, and computes the
    /// entry the two together make.
    /// </summary>
    /// <param name="previous">The last move recorded.</param>
    /// <param name="next">The move that followed it.</param>
    /// <param name="merged">The combined move.</param>
    /// <returns>True when the two are one run.</returns>
    /// <remarks>
    /// Three runs merge, and only when they stay inside
    /// <see cref="MergeLimit"/> together: text typed forward onto the end of
    /// what was typed before; characters deleted forward from the same place
    /// (the Delete key); and characters deleted backward from a caret walking
    /// back (the Backspace key), where the span grows leftward. A replaced
    /// selection, a paste, or a mixed edit stands alone as its own entry.
    /// </remarks>
    private static bool TryMerge(TextChange previous, TextChange next, out TextChange merged) {
        // Typed onto the end of the last typing: the run's start stays put
        // and the inserted text grows.
        if (previous.Removed.Length == 0
                && previous.Inserted.Length > 0
                && next.Removed.Length == 0
                && next.Inserted.Length > 0
                && next.Start == previous.Start + previous.Inserted.Length
                && previous.Inserted.Length + next.Inserted.Length <= MergeLimit) {
            merged = previous with {
                Inserted = string.Concat(previous.Inserted, next.Inserted)
            };
            return true;
        }

        // Deleted forward from where the last forward delete ended: the
        // start stays put and the removed span grows rightward.
        if (previous.Inserted.Length == 0
                && previous.Removed.Length > 0
                && next.Inserted.Length == 0
                && next.Removed.Length > 0
                && next.Start == previous.Start
                && previous.Removed.Length + next.Removed.Length <= MergeLimit) {
            merged = previous with {
                Removed = string.Concat(previous.Removed, next.Removed)
            };
            return true;
        }

        // Deleted backward from a caret walking left: the start moves left
        // with the caret and the removed span grows leftward.
        if (previous.Inserted.Length == 0
                && previous.Removed.Length > 0
                && next.Inserted.Length == 0
                && next.Removed.Length > 0
                && next.Start + next.Removed.Length == previous.Start
                && previous.Removed.Length + next.Removed.Length <= MergeLimit) {
            merged = next with {
                Removed = string.Concat(next.Removed, previous.Removed)
            };
            return true;
        }

        merged = default;
        return false;
    }
}
