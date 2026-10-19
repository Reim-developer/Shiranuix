namespace Shiranuix.CoreEditor;

/// <summary>
/// One move against the file: where it falls, what was there, and what
/// the span has now — and, once anything asks, what each of those texts
/// holds.
/// </summary>
/// <remarks>
/// <para>
/// Entries fall in order and never overlap, and the two predicates carry
/// the seam rule every edge in the ledger agrees on: a move ending
/// exactly at a position still lies before it, while an insertion
/// standing exactly there does not — an insertion at a seam is the text
/// the stretch begins with, not something before it.
/// </para>
/// <para>
/// The counts of each text are worked out the first time anything asks
/// and held after: a move standing outside the window is counted on every
/// keystroke, and re-reading its text each time would make every
/// keystroke pay for text that never changed. The cache is only ever read
/// through the getters, and a merged move becomes a new entry rather than
/// an edited one — so a count cannot go stale behind an edited text.
/// </para>
/// </remarks>
public sealed class EditLedgerEntry {
    private TextStatisticsResult? _textStats;
    private TextStatisticsResult? _originalStats;

    /// <summary>
    /// Creates a move at a position of the file.
    /// </summary>
    /// <param name="start">The first file character replaced.</param>
    /// <param name="original">The file's own text over the span.</param>
    /// <param name="text">The text the span has now.</param>
    public EditLedgerEntry(int start, string original, string text) {
        Start = start;
        Original = original ?? throw new ArgumentNullException(nameof(original));
        Text = text ?? throw new ArgumentNullException(nameof(text));
    }

    /// <summary>The first file character replaced.</summary>
    public int Start { get; }

    /// <summary>The file's own text over the span.</summary>
    public string Original { get; }

    /// <summary>The text the span has now.</summary>
    public string Text { get; }

    /// <summary>One past the last file character the move replaced.</summary>
    public int End => Start + Original.Length;

    /// <summary>
    /// What the span's text holds, worked out on first read.
    /// </summary>
    public TextStatisticsResult TextStats =>
        _textStats ??= TextStatistics.Measure(Text);

    /// <summary>
    /// What the file's own text over the span held, worked out on first
    /// read.
    /// </summary>
    public TextStatisticsResult OriginalStats =>
        _originalStats ??= TextStatistics.Measure(Original);

    /// <summary>
    /// Whether the move lies entirely before a position, with the seam
    /// rule the window's own edge uses.
    /// </summary>
    /// <param name="position">The position.</param>
    /// <returns>True when the move ends before it.</returns>
    public bool LiesBefore(int position) =>
        End < position || (End == position && Start < End);

    /// <summary>
    /// Whether the move lies entirely after a stretch of file ends.
    /// </summary>
    /// <param name="windowEnd">Where the stretch ends.</param>
    /// <returns>True when the move cannot be seen from inside the stretch.</returns>
    public bool LiesAfter(int windowEnd) =>
        Start > windowEnd || (Start == windowEnd && Start < End);
}
