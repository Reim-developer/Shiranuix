using Shiranuix.Models;

namespace Shiranuix.Services;

/// <summary>
/// What comes back out of the stacks: the move on top inverted over the text
/// as it stands, the stacks kept in step through the swap, and the target
/// that says which window to make the trip back to.
/// </summary>
/// <remarks>
/// The applying half of the class, against the recording half in
/// <c>DocumentHistory.Recording.cs</c> — but the same class, because an entry
/// playback inverts is the entry recording shaped: the same span, the same
/// window, and a stack pair where every pop from one lands in the other.
/// </remarks>
public sealed partial class DocumentHistory {
    /// <inheritdoc />
    public bool PeekUndo(out UndoTarget target) {
        if (_undo.Count == 0) {
            target = default;
            return false;
        }

        var entry = _undo[^1];
        target = new UndoTarget(entry.WindowFrom, entry.WindowTo, entry.Caret);

        return true;
    }

    /// <inheritdoc />
    public bool PeekRedo(out UndoTarget target) {
        if (_redo.Count == 0) {
            target = default;
            return false;
        }

        var entry = _redo[^1];

        // The redo lands the caret after what it puts back — the end of the
        // text the move wrote, where a writer expects to continue.
        target = new UndoTarget(
            entry.WindowFrom,
            entry.WindowTo,
            entry.Start + entry.Inserted.Length);

        return true;
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
}
