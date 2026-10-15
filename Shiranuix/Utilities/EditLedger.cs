using System.Text;
using Shiranuix.Models;

namespace Shiranuix.Utilities;

/// <summary>
/// What the writer has changed about the file behind the window: each move
/// as the span of the file it replaces and the text that takes its place,
/// with no move left in it that cancels another.
/// </summary>
/// <remarks>
/// <para>
/// The document keeps its text in one string, but that string is only the
/// window onto the file — so the moves cannot be remembered against the
/// text, which shifts with every one of them, and are remembered against
/// the file instead. File coordinates never move: a move made today still
/// means the same bytes tomorrow, whether the window is showing them or has
/// slid a thousand lines away. Saving hands the list to the writer, which
/// streams the file and lays each move over the span it names.
/// </para>
/// <para>
/// Every entry is whole — a move's span starts and ends in untouched file
/// or at an entry's own edge, never in the middle of one. An edit that
/// reaches into an entry absorbs it, edits that meet merge, and an entry
/// whose text is exactly the file's own — a change typed back to nothing —
/// cancels itself out. So an empty ledger means the file and the document
/// agree, however many moves it took to get there, and "changed since the
/// last save" is a question about the list rather than a second copy of the
/// text.
/// </para>
/// <para>
/// The window's own original text is held for the length of the window's
/// stay, because the gaps between entries can only be read from the window
/// that shows them. A window is expected to hold whole entries: moving one
/// snaps its edges over any entry it would otherwise cut in half, which is
/// what keeps "whole" true for every entry it can see.
/// </para>
/// </remarks>
public sealed class EditLedger {
    private readonly List<Entry> _entries = new();

    private int _windowStart;
    private string _windowOriginal = string.Empty;

    /// <summary>
    /// One move against the file: where it falls, what was there, and what
    /// is there now.
    /// </summary>
    /// <param name="Start">The first file character replaced.</param>
    /// <param name="Original">The file's own text over the span.</param>
    /// <param name="Text">The text the span has now.</param>
    private sealed record Entry(int Start, string Original, string Text) {
        public int End => Start + Original.Length;
    }

    /// <summary>
    /// True when no move stands against the file — the document and the
    /// file it came from say the same thing.
    /// </summary>
    public bool IsEmpty => _entries.Count == 0;

    /// <summary>
    /// Where the window's stretch of file begins — the same number the
    /// moves are kept in, so the two can be compared directly.
    /// </summary>
    public int WindowFrom => _windowStart;

    /// <summary>
    /// One past the last file character the window shows.
    /// </summary>
    /// <remarks>
    /// Fixed for the window's stay: moves change what the window says, not
    /// the stretch of file it says it over — which is what lets the history
    /// tell one window from another by two numbers.
    /// </remarks>
    public int WindowTo => _windowStart + _windowOriginal.Length;

    /// <summary>
    /// The file's own text over the window, as it is on disk — the ground
    /// every move standing in the window is laid over.
    /// </summary>
    public string WindowOriginal => _windowOriginal;

    /// <summary>
    /// The window's text as the document should show it: the file's own
    /// text with every move standing in the window laid over the spans they
    /// name.
    /// </summary>
    /// <returns>The window's text, moves and all.</returns>
    /// <remarks>
    /// A window onto a large file is read from the file, but a move made
    /// against it since is not in the file until saving — so a window slid
    /// back over an edited stretch must lay the moves over the freshly read
    /// text, the same way saving will one day lay them over the file.
    /// </remarks>
    public string Materialize() {
        var windowEnd = WindowTo;
        var text = new StringBuilder();
        var cursor = _windowStart;

        foreach (var entry in _entries) {
            if (IsBeforeWindow(entry)) {
                continue;
            }

            if (IsAfterWindow(entry, windowEnd)) {
                break;
            }

            // Untouched file between the last thing and this: its
            // characters come from the window, position for position.
            if (entry.Start > cursor) {
                text.Append(_windowOriginal, cursor - _windowStart, entry.Start - cursor);
            }

            text.Append(entry.Text);
            cursor = entry.End;
        }

        if (cursor < windowEnd) {
            text.Append(_windowOriginal, cursor - _windowStart, windowEnd - cursor);
        }

        return text.ToString();
    }

    /// <summary>
    /// Makes the saved file the new zero: every move collapses into it, and
    /// the window keeps showing its own text — now at the place that same
    /// text stands in the file that was written.
    /// </summary>
    /// <param name="windowText">The window's text as the document shows it.</param>
    /// <returns>Where the window now begins in the saved file.</returns>
    /// <remarks>
    /// The moves before the window are the ones that move it: whatever they
    /// added or took out of the file ahead of the window slides every later
    /// position by the same amount. Moves inside the window are already
    /// behind its text — the file now says what the window says over that
    /// stretch — and moves after it change nothing before them.
    /// </remarks>
    public int RebaseToSaved(string windowText) {
        ArgumentNullException.ThrowIfNull(windowText);

        var start = 0;

        foreach (var entry in _entries) {
            if (IsBeforeWindow(entry)) {
                start += entry.Text.Length - entry.Original.Length;
            }
        }

        _entries.Clear();
        _windowStart += start;
        _windowOriginal = windowText;

        return _windowStart;
    }

    /// <summary>
    /// Points the ledger at a new stretch of file: the window now starts at
    /// <paramref name="start"/> and shows <paramref name="originalText"/>
    /// there. Moves outside the new window stand; the window's own original
    /// is replaced.
    /// </summary>
    /// <param name="start">Where the window's text begins in the file.</param>
    /// <param name="originalText">The file's text over the window, as it is
    /// on disk — before this window's moves, not before all of them.</param>
    public void ResetWindow(int start, string originalText) {
        ArgumentNullException.ThrowIfNull(originalText);

        if (start < 0) {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        _windowStart = start;
        _windowOriginal = originalText;
    }

    /// <summary>
    /// Forgets every move: the file and the document have been made to
    /// agree, so there is nothing between them to remember.
    /// </summary>
    public void Clear() {
        _entries.Clear();
    }

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

        if (!TextDelta.TryDiff(before, after, out var start, out var removed, out var inserted)) {
            return;
        }

        var end = start + removed.Length;
        var live = Realize();

        // Where the move falls in the file: an entry in the way is absorbed
        // whole — half an entry cannot stand — and a stretch of untouched
        // file translates position for position. The maps also say how many
        // of the window's entries the span swallowed, from first to last.
        var (rangeStart, currentStart, first) = MapStart(start, live);
        var (rangeEnd, currentEnd, last) = MapEnd(end, live);

        // What the file held over the span, read from the window and from
        // the entries the span swallowed; and what the window's text makes
        // of it, which is the text around the move.
        var original = OriginalSlice(rangeStart, rangeEnd, live);

        var text = new StringBuilder();
        text.Append(before, currentStart, start - currentStart);
        text.Append(inserted);
        text.Append(before, end, currentEnd - end);

        var candidate = new Entry(rangeStart, original, text.ToString());

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
    /// The moves against the file, in the order they fall, ready for the
    /// writer.
    /// </summary>
    /// <returns>One replacement per surviving move.</returns>
    public IReadOnlyList<TextReplacement> Build() {
        var replacements = new TextReplacement[_entries.Count];

        for (var index = 0; index < _entries.Count; index++) {
            var entry = _entries[index];
            replacements[index] = new TextReplacement(entry.Start, entry.Original.Length, entry.Text);
        }

        return replacements;
    }

    /// <summary>
    /// The moves against the file, in the order they fall, in the shape the
    /// document reads them.
    /// </summary>
    /// <returns>One span per surviving move.</returns>
    public IReadOnlyList<EditSpan> Spans() {
        var spans = new EditSpan[_entries.Count];

        for (var index = 0; index < _entries.Count; index++) {
            var entry = _entries[index];
            spans[index] = new EditSpan(entry.Start, entry.Original, entry.Text);
        }

        return spans;
    }

    /// <summary>
    /// The moves the window cannot see: their text is not in it, so their
    /// effect on the whole file's counts has to be added over the top of
    /// the window's own.
    /// </summary>
    /// <returns>Every move standing before or after the window.</returns>
    public IReadOnlyList<EditSpan> SpansOutside() {
        var windowEnd = WindowTo;
        var spans = new List<EditSpan>();

        foreach (var entry in _entries) {
            if (IsBeforeWindow(entry) || IsAfterWindow(entry, windowEnd)) {
                spans.Add(new EditSpan(entry.Start, entry.Original, entry.Text));
            }
        }

        return spans;
    }

    /// <summary>
    /// The moves that lie entirely before a position — the ones that shift
    /// what stands there by whatever they changed.
    /// </summary>
    /// <param name="position">A file position.</param>
    /// <returns>Every move ending before it, in order.</returns>
    public IReadOnlyList<EditSpan> SpansBefore(int position) {
        var spans = new List<EditSpan>();

        foreach (var entry in _entries) {
            if (!LiesBefore(entry, position)) {
                // Entries fall in order: the first that does not lie before
                // the position is the first that cannot.
                break;
            }

            spans.Add(new EditSpan(entry.Start, entry.Original, entry.Text));
        }

        return spans;
    }

    /// <summary>
    /// Whether any move's span would be cut in half by a window edge
    /// standing at a position.
    /// </summary>
    /// <param name="position">The candidate edge.</param>
    /// <returns>True when the edge must move to keep every entry whole.</returns>
    public bool Cuts(int position) => CutAt(position) is not null;

    /// <summary>
    /// The move a window edge standing at a position would cut in half, if
    /// any.
    /// </summary>
    /// <param name="position">The candidate edge.</param>
    /// <returns>The move that would be cut, or nothing when the edge is
    /// clean.</returns>
    /// <remarks>
    /// The whole move comes back rather than just the yes, because the edge
    /// that has to move needs both ends: a front edge steps out to where the
    /// move begins, a trailing edge to where it ends.
    /// </remarks>
    public EditSpan? CutAt(int position) {
        foreach (var entry in _entries) {
            if (entry.Start < position && position < entry.End) {
                return new EditSpan(entry.Start, entry.Original, entry.Text);
            }
        }

        return null;
    }

    /// <summary>
    /// Translates the front of a move in the window's current text into
    /// where it falls in the file, and how far back the span must reach.
    /// </summary>
    /// <param name="start">The move's front in the window's text.</param>
    /// <param name="live">The window's entries, in order.</param>
    /// <returns>The file position the span starts at, the position in the
    /// window's text the span's own current text starts at, and the index
    /// of the first entry the span swallows — one past the last entry it
    /// does not, when it swallows none.</returns>
    private (int File, int Current, int First) MapStart(int start, List<Fragment> live) {
        var cursorLocal = 0;
        var cursorFile = _windowStart;

        for (var index = 0; index < live.Count; index++) {
            var fragment = live[index];

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

        return (cursorFile + (start - cursorLocal), start, live.Count);
    }

    /// <summary>
    /// Translates the back of a move in the window's current text into
    /// where it falls in the file, and how far forward the span must reach.
    /// </summary>
    /// <param name="end">The move's back in the window's text — one past
    /// its last character.</param>
    /// <param name="live">The window's entries, in order.</param>
    /// <returns>The file position the span ends after, the position in the
    /// window's text the span's own current text ends at, and the index of
    /// the last entry the span swallows — one before the first entry it
    /// does not, when it swallows none.</returns>
    private (int File, int Current, int Last) MapEnd(int end, List<Fragment> live) {
        var cursorLocal = 0;
        var cursorFile = _windowStart;

        for (var index = 0; index < live.Count; index++) {
            var fragment = live[index];

            if (end <= fragment.LocalStart) {
                return (cursorFile + (end - cursorLocal), end, index - 1);
            }

            if (end <= fragment.LocalEnd) {
                return (fragment.Entry.End, fragment.LocalEnd, index);
            }

            cursorLocal = fragment.LocalEnd;
            cursorFile = fragment.Entry.End;
        }

        return (cursorFile + (end - cursorLocal), end, live.Count - 1);
    }

    /// <summary>
    /// The entries the window can see, each with where its text lands in
    /// the window's current text.
    /// </summary>
    /// <returns>Every entry that stands in the window, in order.</returns>
    private List<Fragment> Realize() {
        var windowEnd = _windowStart + _windowOriginal.Length;
        var fragments = new List<Fragment>();
        var local = 0;
        var cursor = _windowStart;

        foreach (var entry in _entries) {
            if (IsBeforeWindow(entry)) {
                // Entirely before the window: no part of it is in the text.
                continue;
            }

            if (IsAfterWindow(entry, windowEnd)) {
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

        return fragments;
    }

    /// <summary>
    /// Whether an entry lies entirely before the window's stretch of file.
    /// </summary>
    /// <param name="entry">The entry to test.</param>
    /// <returns>True when the window cannot see it.</returns>
    /// <remarks>
    /// An entry ending exactly at the window's start still lies before it,
    /// but a point entry standing exactly there does not: an insertion at
    /// the seam is the text the window begins with.
    /// </remarks>
    private bool IsBeforeWindow(Entry entry) => LiesBefore(entry, _windowStart);

    /// <summary>
    /// Whether an entry lies entirely before a position, with the seam rule
    /// the window's own edge uses: a move ending exactly there still lies
    /// before it, an insertion standing exactly there does not.
    /// </summary>
    /// <param name="entry">The entry to test.</param>
    /// <param name="position">The position.</param>
    /// <returns>True when the entry ends before it.</returns>
    private static bool LiesBefore(Entry entry, int position) =>
        entry.End < position
        || (entry.End == position && entry.Start < entry.End);

    /// <summary>
    /// Whether an entry lies entirely after the window's stretch of file.
    /// </summary>
    /// <param name="entry">The entry to test.</param>
    /// <param name="windowEnd">Where the window's stretch ends.</param>
    /// <returns>True when the window cannot see it.</returns>
    private bool IsAfterWindow(Entry entry, int windowEnd) =>
        entry.Start > windowEnd
        || (entry.Start == windowEnd && entry.Start < entry.End);

    /// <summary>
    /// Reads the file's own text over a span, from the window where the
    /// span crosses untouched file and from the entries where it does not.
    /// </summary>
    /// <param name="start">Where the span begins in the file.</param>
    /// <param name="end">Where it ends.</param>
    /// <param name="live">The window's entries, in order.</param>
    /// <returns>What the file says over the span.</returns>
    private string OriginalSlice(int start, int end, List<Fragment> live) {
        var slice = new StringBuilder();
        var cursor = start;

        foreach (var fragment in live) {
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
    /// Takes the entries a move swallowed out of the list: their spans and
    /// their text live inside the move's own entry now.
    /// </summary>
    /// <param name="live">The window's entries, in order.</param>
    /// <param name="from">The index of the first swallowed entry.</param>
    /// <param name="to">The index of the last swallowed entry.</param>
    private void Swallow(List<Fragment> live, int from, int to) {
        for (var index = from; index <= to && index < live.Count; index++) {
            _entries.Remove(live[index].Entry);
        }
    }

    /// <summary>
    /// Puts an entry into the list where it falls, folded into every entry
    /// it now stands beside, so adjacent stretches of file are one move
    /// rather than a chain of them.
    /// </summary>
    /// <param name="entry">The entry to store.</param>
    private void Store(Entry entry) {
        var index = NextIndex(entry.Start);
        _entries.Insert(index, entry);
        var current = entry;

        while (index > 0 && _entries[index - 1].End == current.Start) {
            var neighbor = _entries[index - 1];
            current = neighbor with {
                Original = neighbor.Original + current.Original,
                Text = neighbor.Text + current.Text
            };
            _entries[index - 1] = current;
            _entries.RemoveAt(index);
            index--;
        }

        while (index + 1 < _entries.Count && _entries[index + 1].Start == current.End) {
            var neighbor = _entries[index + 1];
            current = current with {
                Original = current.Original + neighbor.Original,
                Text = current.Text + neighbor.Text
            };
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

    /// <summary>
    /// One entry as it sits in the window's current text.
    /// </summary>
    /// <param name="Entry">The entry itself.</param>
    /// <param name="LocalStart">Where its text starts in the window.</param>
    /// <param name="LocalEnd">Where its text ends in the window.</param>
    private readonly record struct Fragment(Entry Entry, int LocalStart, int LocalEnd);
}
