namespace Shiranuix.CoreEditor;

/// <summary>
/// How a move is recorded: the difference worked out, the run it continues
/// folded into, and the entry both together leave on the undo stack.
/// </summary>
/// <remarks>
/// The computing half of the class, against the taking-back half in
/// <c>DocumentHistory.Playback.cs</c> — but the same class, because what a
/// recording may merge into is exactly what playback can later take back as
/// one step: entries whole, entries in order, and no entry past
/// <see cref="Limit"/>.
/// </remarks>
public sealed partial class DocumentHistory {
    /// <inheritdoc />
    public void Note(string before, string after, UndoTarget at) {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        if (TryDiff(before, after, out var change)) {
            Note(change.Start, change.Removed, change.Inserted, at);
        }
    }

    /// <inheritdoc />
    public void Note(int start, string removed, string inserted, UndoTarget at) {
        ArgumentNullException.ThrowIfNull(removed);
        ArgumentNullException.ThrowIfNull(inserted);

        // Typing anew forks the timeline: the moves waiting on the redo
        // branch describe a text that is about to stop being reachable.
        _redo.Clear();

        Push(new TextChange(start, removed, inserted, at.WindowFrom, at.WindowTo, at.Caret));
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
    /// The window starts out covering the whole text, because the diff knows
    /// nothing of windows — <see cref="Note(int, string, string, UndoTarget)"/>
    /// puts the real one in.
    /// </remarks>
    private static bool TryDiff(string before, string after, out TextChange change) {
        if (!TextDelta.TryDiff(before, after, out var start, out var removed, out var inserted)) {
            change = default;
            return false;
        }

        change = new TextChange(start, removed, inserted, 0, before.Length, 0);
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
    /// <see cref="MergeLimit"/> together and inside the window they started
    /// in: text typed forward onto the end of what was typed before;
    /// characters deleted forward from the same place (the Delete key); and
    /// characters deleted backward from a caret walking back (the Backspace
    /// key), where the span grows leftward. A replaced selection, a paste, a
    /// move made after the window slid, or a mixed edit stands alone as its
    /// own entry.
    /// </remarks>
    private static bool TryMerge(TextChange previous, TextChange next, out TextChange merged) {
        // A run continues only inside the window it started in: after the
        // window slides, the same keystrokes move a different stretch of
        // file, and one entry spanning both windows would restore neither.
        if (previous.WindowFrom != next.WindowFrom || previous.WindowTo != next.WindowTo) {
            merged = default;
            return false;
        }

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
