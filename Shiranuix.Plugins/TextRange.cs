namespace Shiranuix.Plugins;

/// <summary>
/// A stretch of a document, said the way a string holds one: where it starts
/// and how long it runs.
/// </summary>
/// <remarks>
/// A length rather than an end, so a range can never claim text that is not
/// there by arithmetic — an end past the text would have to be checked against
/// a document that moved, while a length is checked once, at the moment it is
/// used. That is also what an editor's selection already is.
/// </remarks>
public readonly record struct TextRange {
    /// <summary>The empty stretch at the start of a document.</summary>
    public static TextRange Empty { get; } = new(0, 0);

    /// <summary>Creates a stretch of a document.</summary>
    /// <param name="start">Where the stretch begins, counted from zero.</param>
    /// <param name="length">How long it runs.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The start is below zero, or the length is negative.
    /// </exception>
    public TextRange(int start, int length) {
        if (start < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(start), start, "A range starts at zero or later.");
        }

        if (length < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(length), length, "A range does not run backwards.");
        }

        Start = start;
        Length = length;
    }

    /// <summary>Where the stretch begins, counted from zero.</summary>
    public int Start { get; }

    /// <summary>How long the stretch runs.</summary>
    public int Length { get; }

    /// <summary>Where the stretch stops, which is where the next one starts.</summary>
    public int End => Start + Length;

    /// <summary>Whether the stretch holds nothing at all.</summary>
    public bool IsEmpty => Length == 0;

    /// <summary>
    /// Whether the stretch could lie inside a text of the given length.
    /// </summary>
    /// <param name="textLength">How long the text is.</param>
    /// <returns>True when the whole stretch fits.</returns>
    public bool FitsIn(int textLength) => End <= textLength;

    /// <summary>
    /// Pulls the stretch back so it lies inside a text of the given length.
    /// </summary>
    /// <param name="textLength">How long the text is.</param>
    /// <returns>
    /// The same stretch, or one moved to the end of the text when it ran past
    /// it. A range that no longer fits is a range that describes nothing, so
    /// moving it beats refusing to answer.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The length given is negative, which no text has.
    /// </exception>
    public TextRange ClampTo(int textLength) {
        if (textLength < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(textLength), textLength, "A text is never negative.");
        }

        if (FitsIn(textLength)) {
            return this;
        }

        var start = Math.Min(Start, textLength);

        return new TextRange(start, textLength - start);
    }
}
