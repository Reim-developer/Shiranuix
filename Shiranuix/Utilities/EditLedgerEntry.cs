namespace Shiranuix.Utilities;

/// <summary>
/// One move against the file: where it falls, what was there, and what
/// the span has now.
/// </summary>
/// <param name="Start">The first file character replaced.</param>
/// <param name="Original">The file's own text over the span.</param>
/// <param name="Text">The text the span has now.</param>
/// <remarks>
/// Entries fall in order and never overlap, and the two predicates below
/// carry the seam rule every edge in the ledger agrees on: a move ending
/// exactly at a position still lies before it, while an insertion
/// standing exactly there does not — an insertion at a seam is the text
/// the stretch begins with, not something before it.
/// </remarks>
public sealed record EditLedgerEntry(int Start, string Original, string Text) {
    /// <summary>One past the last file character the move replaced.</summary>
    public int End => Start + Original.Length;

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
