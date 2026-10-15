using System.Text;

namespace Shiranuix.Utilities;

/// <summary>
/// How a move is folded into the ledger: where it falls in the file, which
/// entries it swallows, and the one entry it ends up as.
/// </summary>
/// <remarks>
/// The record side of the class, against the reads in
/// <c>EditLedger.Reads.cs</c> — but the same class, because the folding
/// rules and the whole-entry invariant they keep are two halves of one
/// promise: entries whole, entries in order, no entry left standing that
/// cancels another.
/// </remarks>
public sealed partial class EditLedger {
    /// <summary>
    /// Records what one move did to the window's text, folding it into the
    /// entries it touches.
    /// </summary>
    /// <param name="before">The window's text before the move.</param>
    /// <param name="after">The window's text after it.</param>
    /// <remarks>
    /// The move arrives as the window's own before and after, because that
    /// is what the text box hands over; the ledger works out where in the
    /// file it falls, which spans it swallows, and what the text over those
    /// spans now is — then folds it in with whatever it meets, so a run of
    /// typing against one line is one move against one line.
    /// </remarks>
    public void Note(string before, string after) {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        if (TextDelta.TryDiff(before, after, out var start, out var removed, out var inserted)) {
            Note(before, start, removed, inserted);
        }
    }

    /// <summary>
    /// Folds a move whose difference is already known into the ledger: it
    /// works out where in the file the span falls, which spans it swallows,
    /// and what the text over those spans now is — then folds it in with
    /// whatever it meets, so a run of typing against one line is one move
    /// against one line.
    /// </summary>
    /// <param name="before">The window's text as it stood — the stretch on
    /// either side of the move still stands in it, which is what the span's
    /// text is built from; only the difference, not the question, is saved
    /// by arriving with one already worked out.</param>
    /// <param name="start">Where in that text the move began.</param>
    /// <param name="removed">What stood there.</param>
    /// <param name="inserted">What stands there now.</param>
    /// <remarks>
    /// The text setter works the difference out once for the history and
    /// the ledger together — the same question asked of the same two texts,
    /// and at the size a window can reach, asking twice is not free. Only
    /// call it when there really was a move;
    /// <see cref="Note(string, string)"/> is the way in from the two texts
    /// alone.
    /// </remarks>
    public void Note(string before, int start, string removed, string inserted) {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(removed);
        ArgumentNullException.ThrowIfNull(inserted);

        var end = start + removed.Length;
        var live = WindowFragments.Over(_entries, _windowStart, _windowOriginal);

        // Where the move falls in the file: an entry in the way is absorbed
        // whole — half an entry cannot stand — and a stretch of untouched
        // file translates position for position. The view also says how many
        // of the window's entries the span swallowed, from first to last.
        var (rangeStart, currentStart, first) = live.MapStart(start);
        var (rangeEnd, currentEnd, last) = live.MapEnd(end);

        // What the file held over the span, read from the window and from
        // the entries the span swallowed; and what the window's text makes
        // of it, which is the text around the move.
        var original = live.OriginalSlice(rangeStart, rangeEnd);

        var text = new StringBuilder();
        text.Append(before, currentStart, start - currentStart);
        text.Append(inserted);
        text.Append(before, end, currentEnd - end);

        var candidate = new EditLedgerEntry(rangeStart, original, text.ToString());

        // The swallowed spans live inside the candidate now — their own
        // entries must go whether the candidate stands or cancels.
        Swallow(live, first, last);

        if (candidate.Original == candidate.Text) {
            // Typed back to what the file already says: the move undoes
            // itself, and the ledger forgets it rather than carrying an
            // edit that is no one's edit.
            return;
        }

        Store(candidate);
    }

    /// <summary>
    /// Takes the entries a move swallowed out of the list: their spans and
    /// their text live inside the move's own entry now.
    /// </summary>
    /// <param name="live">The window's view of its entries.</param>
    /// <param name="from">The index of the first swallowed entry.</param>
    /// <param name="to">The index of the last swallowed entry.</param>
    private void Swallow(WindowFragments live, int from, int to) {
        if (from > to || from >= live.Count) {
            return;
        }

        // The view holds one stretch of the ledger's list, in the list's own
        // order, so the swallowed run is one stretch of that list too — cut
        // it whole rather than searching and shifting once per entry.
        var count = Math.Min(to, live.Count - 1) - from + 1;
        var first = _entries.IndexOf(live.EntryAt(from));

        if (first >= 0) {
            _entries.RemoveRange(first, count);
        }
    }

    /// <summary>
    /// Puts an entry into the list where it falls, folded into every entry
    /// it now stands beside, so adjacent stretches of file are one move
    /// rather than a chain of them.
    /// </summary>
    /// <param name="entry">The entry to store.</param>
    private void Store(EditLedgerEntry entry) {
        var index = NextIndex(entry.Start);
        _entries.Insert(index, entry);
        var current = entry;

        while (index > 0 && _entries[index - 1].End == current.Start) {
            var neighbor = _entries[index - 1];

            // A merged move is a new entry — never an edited one — so its
            // counts, worked out whenever anything asks, start fresh with
            // the text they describe.
            current = new EditLedgerEntry(
                neighbor.Start,
                neighbor.Original + current.Original,
                neighbor.Text + current.Text);
            _entries[index - 1] = current;
            _entries.RemoveAt(index);
            index--;
        }

        while (index + 1 < _entries.Count && _entries[index + 1].Start == current.End) {
            var neighbor = _entries[index + 1];
            current = new EditLedgerEntry(
                current.Start,
                current.Original + neighbor.Original,
                current.Text + neighbor.Text);
            _entries[index] = current;
            _entries.RemoveAt(index + 1);
        }
    }

    /// <summary>
    /// The index of the first entry that does not fall at or before
    /// <paramref name="start"/>.
    /// </summary>
    /// <param name="start">A file position.</param>
    /// <returns>Where an entry at that position belongs.</returns>
    /// <remarks>
    /// Entries already standing at the position come first, so a new one
    /// joins them behind: at one point in the file, what was there already
    /// is what the window's text shows first, and folding them together
    /// has to keep that order.
    /// </remarks>
    private int NextIndex(int start) {
        var index = 0;

        while (index < _entries.Count && _entries[index].Start <= start) {
            index++;
        }

        return index;
    }
}
