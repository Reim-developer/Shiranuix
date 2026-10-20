using System.Text;

namespace Shiranuix.CoreEditor;

/// <summary>
/// The ledger's entries as they sit in the window's current text: where
/// each one's text lands, and the questions a new move asks of that view.
/// </summary>
/// <remarks>
/// The window shows the file's own text with the entries standing in it
/// laid over the spans they name, so a position in the window's text and
/// a position in the file are one question asked from either side. This
/// view is worked out once per move — the mapping, the slices and the
/// swallowing all read the same fragments, so they cannot disagree about
/// where anything stands.
/// </remarks>
public sealed class WindowFragments {
    private readonly int _windowStart;
    private readonly string _windowOriginal;
    private readonly List<Fragment> _fragments;

    private WindowFragments(int windowStart, string windowOriginal, List<Fragment> fragments) {
        _windowStart = windowStart;
        _windowOriginal = windowOriginal;
        _fragments = fragments;
    }

    /// <summary>
    /// Works the view out: every entry standing in the window's stretch of
    /// file, with where its text lands in the window's current text.
    /// </summary>
    /// <param name="entries">The ledger's entries, in order.</param>
    /// <param name="windowStart">Where the window's stretch of file begins.</param>
    /// <param name="windowOriginal">The file's own text over the window.</param>
    /// <returns>The window's entries, in order.</returns>
    /// <remarks>
    /// An entry ending exactly at the window's start still lies before it
    /// and is not part of the view, but a point entry standing exactly
    /// there is the text the window begins with and is.
    /// </remarks>
    public static WindowFragments Over(
        IReadOnlyList<EditLedgerEntry> entries,
        int windowStart,
        string windowOriginal) {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(windowOriginal);

        var windowEnd = windowStart + windowOriginal.Length;
        var fragments = new List<Fragment>();
        var local = 0;
        var cursor = windowStart;

        foreach (var entry in entries) {
            if (entry.LiesBefore(windowStart)) {
                // Entirely before the window: no part of it is in the text.
                continue;
            }

            if (entry.LiesAfter(windowEnd)) {
                break;
            }

            // Untouched file between the last thing and this: its
            // characters sit in the text exactly as they sit in the file,
            // so the two positions move together from there.
            var gap = entry.Start - cursor;

            if (gap > 0) {
                local += gap;
                cursor = entry.Start;
            }

            fragments.Add(new Fragment(entry, local, local + entry.Text.Length));
            local += entry.Text.Length;
            cursor = entry.End;
        }

        return new WindowFragments(windowStart, windowOriginal, fragments);
    }

    /// <summary>How many entries stand in the window.</summary>
    public int Count => _fragments.Count;

    /// <summary>
    /// The entry a fragment index names — the one a swallowed span's
    /// removal walks back to the ledger's list for.
    /// </summary>
    /// <param name="index">The fragment's index.</param>
    /// <returns>The entry itself.</returns>
    public EditLedgerEntry EntryAt(int index) => _fragments[index].Entry;

    /// <summary>
    /// Translates the front of a move in the window's current text into
    /// where it falls in the file, and how far back the span must reach.
    /// </summary>
    /// <param name="start">The move's front in the window's text.</param>
    /// <returns>The file position the span starts at, the position in the
    /// window's text the span's own current text starts at, and the index
    /// of the first entry the span swallows — one past the last entry it
    /// does not, when it swallows none.</returns>
    public (int File, int Current, int First) MapStart(int start) {
        var cursorLocal = 0;
        var cursorFile = _windowStart;

        for (var index = 0; index < _fragments.Count; index++) {
            var fragment = _fragments[index];

            if (start < fragment.LocalStart) {
                // Untouched file: the file and the text count it the same.
                return (cursorFile + (start - cursorLocal), start, index);
            }

            if (start < fragment.LocalEnd) {
                // Inside an entry: the whole entry goes with the move.
                return (fragment.Entry.Start, fragment.LocalStart, index);
            }

            cursorLocal = fragment.LocalEnd;
            cursorFile = fragment.Entry.End;
        }

        return (cursorFile + (start - cursorLocal), start, _fragments.Count);
    }

    /// <summary>
    /// Translates the back of a move in the window's current text into
    /// where it falls in the file, and how far forward the span must reach.
    /// </summary>
    /// <param name="end">The move's back in the window's text — one past
    /// its last character.</param>
    /// <returns>The file position the span ends after, the position in the
    /// window's text the span's own current text ends at, and the index of
    /// the last entry the span swallows — one before the first entry it
    /// does not, when it swallows none.</returns>
    public (int File, int Current, int Last) MapEnd(int end) {
        var cursorLocal = 0;
        var cursorFile = _windowStart;

        for (var index = 0; index < _fragments.Count; index++) {
            var fragment = _fragments[index];

            if (end <= fragment.LocalStart) {
                return (cursorFile + (end - cursorLocal), end, index - 1);
            }

            if (end <= fragment.LocalEnd) {
                return (fragment.Entry.End, fragment.LocalEnd, index);
            }

            cursorLocal = fragment.LocalEnd;
            cursorFile = fragment.Entry.End;
        }

        return (cursorFile + (end - cursorLocal), end, _fragments.Count - 1);
    }

    /// <summary>
    /// Reads the file's own text over a span, from the window where the
    /// span crosses untouched file and from the entries where it does not.
    /// </summary>
    /// <param name="start">Where the span begins in the file.</param>
    /// <param name="end">Where it ends.</param>
    /// <returns>What the file says over the span.</returns>
    public string OriginalSlice(int start, int end) {
        var slice = new StringBuilder();
        var cursor = start;

        foreach (var fragment in _fragments) {
            var entry = fragment.Entry;

            if (entry.End <= cursor) {
                continue;
            }

            if (entry.Start >= end) {
                break;
            }

            if (entry.Start > cursor) {
                slice.Append(_windowOriginal, cursor - _windowStart, entry.Start - cursor);
                cursor = entry.Start;
            }

            slice.Append(entry.Original);
            cursor = entry.End;
        }

        if (cursor < end) {
            slice.Append(_windowOriginal, cursor - _windowStart, end - cursor);
        }

        return slice.ToString();
    }

    /// <summary>
    /// One entry as it sits in the window's current text.
    /// </summary>
    /// <param name="Entry">The entry itself.</param>
    /// <param name="LocalStart">Where its text starts in the window.</param>
    /// <param name="LocalEnd">Where its text ends in the window.</param>
    private readonly record struct Fragment(EditLedgerEntry Entry, int LocalStart, int LocalEnd);
}
