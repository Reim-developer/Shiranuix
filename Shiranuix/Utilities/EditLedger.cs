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
    private readonly List<EditLedgerEntry> _entries = new();

    private int _windowStart;
    private string _windowOriginal = string.Empty;

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
    /// The counts of that text, measured when it arrived rather than every
    /// time a count asks: the whole file's counts work from these, and a
    /// keystroke inside the window changes the document's text without
    /// changing what the file said over the stretch the window was read
    /// from. Recomputed exactly where <see cref="WindowOriginal"/> is —
    /// the two cannot drift.
    /// </summary>
    public TextStatisticsResult WindowStats { get; private set; } =
        TextStatistics.Measure(string.Empty);

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
            if (entry.LiesBefore(_windowStart)) {
                continue;
            }

            if (entry.LiesAfter(windowEnd)) {
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
            if (entry.LiesBefore(_windowStart)) {
                start += entry.Text.Length - entry.Original.Length;
            }
        }

        _entries.Clear();
        _windowStart += start;
        _windowOriginal = windowText;
        WindowStats = TextStatistics.Measure(windowText);

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
        WindowStats = TextStatistics.Measure(originalText);
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
    public IReadOnlyList<EditSpan> Spans() => EditSpans.Of(_entries);

    /// <summary>
    /// The moves the window cannot see: their text is not in it, so their
    /// effect on the whole file's counts has to be added over the top of
    /// the window's own.
    /// </summary>
    /// <returns>Every move standing before or after the window.</returns>
    public IReadOnlyList<EditLedgerEntry> EntriesOutside() {
        var windowEnd = WindowTo;
        var entries = new List<EditLedgerEntry>();

        foreach (var entry in _entries) {
            if (entry.LiesBefore(_windowStart) || entry.LiesAfter(windowEnd)) {
                entries.Add(entry);
            }
        }

        return entries;
    }

    /// <summary>
    /// The moves the window cannot see, in the shape the document reads
    /// them.
    /// </summary>
    /// <returns>Every move standing before or after the window.</returns>
    public IReadOnlyList<EditSpan> SpansOutside() => EditSpans.Of(EntriesOutside());

    /// <summary>
    /// The moves that lie entirely before a position — the ones that shift
    /// what stands there by whatever they changed, and whose counts the
    /// gutter's first number stands on.
    /// </summary>
    /// <param name="position">A file position.</param>
    /// <returns>Every move ending before it, in order.</returns>
    public IReadOnlyList<EditLedgerEntry> EntriesBefore(int position) {
        var entries = new List<EditLedgerEntry>();

        foreach (var entry in _entries) {
            if (!entry.LiesBefore(position)) {
                // Entries fall in order: the first that does not lie before
                // the position is the first that cannot.
                break;
            }

            entries.Add(entry);
        }

        return entries;
    }

    /// <summary>
    /// The moves that lie entirely before a position, in the shape the
    /// document reads them.
    /// </summary>
    /// <param name="position">A file position.</param>
    /// <returns>Every move ending before it, in order.</returns>
    public IReadOnlyList<EditSpan> SpansBefore(int position) => EditSpans.Of(EntriesBefore(position));

    /// <summary>
    /// How far a file position moved when every move before it collapsed
    /// into the saved file: in characters, in bytes for the anchors, and in
    /// lines for the gutter.
    /// </summary>
    /// <param name="position">The position, in the file as it was read.</param>
    /// <returns>The shift.</returns>
    /// <remarks>
    /// Asked while the entries still stand against the old file — before
    /// <see cref="RebaseToSaved"/> takes what was written as the new zero —
    /// because the question is what the write changed for anything standing
    /// after the moves.
    /// </remarks>
    public (int Characters, int Bytes, int Lines) ShiftAt(int position) {
        var characters = 0;
        var bytes = 0;
        var lines = 0;

        foreach (var entry in EntriesBefore(position)) {
            characters += entry.Text.Length - entry.Original.Length;
            bytes += Encoding.UTF8.GetByteCount(entry.Text)
                - Encoding.UTF8.GetByteCount(entry.Original);
            lines += Newlines(entry.TextStats) - Newlines(entry.OriginalStats);
        }

        return (characters, bytes, lines);
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
    /// The newlines a set of counts stands for: a text with content has one
    /// more line than it has newlines, and an empty one has neither.
    /// </summary>
    /// <param name="stats">The counts to read.</param>
    /// <returns>How many newlines the text holds.</returns>
    private static int Newlines(TextStatisticsResult stats) =>
        stats.Lines > 0 ? stats.Lines - 1 : 0;

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
