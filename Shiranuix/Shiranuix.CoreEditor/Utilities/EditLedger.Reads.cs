using System.Text;
namespace Shiranuix.CoreEditor;

/// <summary>
/// What is asked of the ledger's list: the window's text with the moves
/// laid over it, the moves in every shape the document reads them, and the
/// shifts a save sends through every position.
/// </summary>
/// <remarks>
/// Reads over the list held beside them in <c>EditLedger.cs</c> — but the
/// same class, because what a read means is the whole-entry invariant the
/// folding keeps: outside means before or after the window's stretch, cut
/// means halfway into an entry, and the window's text is only the file's
/// own with the entries it can see laid over.
/// </remarks>
public sealed partial class EditLedger {
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
            bytes += TextFile.Encoding.GetByteCount(entry.Text)
                - TextFile.Encoding.GetByteCount(entry.Original);
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
}
