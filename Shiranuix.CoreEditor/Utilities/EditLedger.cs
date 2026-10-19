namespace Shiranuix.CoreEditor;

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
/// <remarks>
/// The class spans three files by concern: this one holds the list and the
/// window it is anchored to, <c>EditLedger.Moves.cs</c> holds how a move is
/// folded in, and <c>EditLedger.Reads.cs</c> holds what is asked of the
/// list. The invariant belongs to all three — every entry whole, every
/// entry in order — and one class is what keeps it true across them.
/// </remarks>
public sealed partial class EditLedger {
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
}
