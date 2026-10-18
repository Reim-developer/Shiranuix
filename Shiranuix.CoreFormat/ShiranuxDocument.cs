namespace Shiranuix.CoreFormat;

/// <summary>
/// A document in the .shiranux format: the text, and how each of its
/// paragraphs is set.
/// </summary>
/// <remarks>
/// <para>
/// A paragraph is a run of lines between line breaks - the same shape a
/// .txt file has, so a document that has never been given an alignment
/// reads back exactly as it was written. The alignments are kept as a map
/// from paragraph number to the way it is set; a paragraph that is not in
/// the map is flush left, which is what a page of prose starts at.
/// </para>
/// <para>
/// The text is brought to the one shape the format carries - lines broken
/// by <c>\n</c> - before the paragraphs are counted, so the numbers handed
/// in and the numbers the file writes are the same numbers however the
/// text arrived.
/// </para>
/// </remarks>
public sealed class ShiranuxDocument {
    private readonly int _paragraphCount;

    /// <summary>
    /// Creates a document from its text and the alignments of its
    /// paragraphs.
    /// </summary>
    /// <param name="text">The document's text, with lines broken by <c>\n</c>.</param>
    /// <param name="alignments">The paragraphs that are not flush left, by number.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An alignment names a paragraph the text does not have.
    /// </exception>
    public ShiranuxDocument(
        string text,
        IReadOnlyDictionary<int, ParagraphAlignment>? alignments = null) {
        ArgumentNullException.ThrowIfNull(text);

        Text = text.Replace("\r\n", "\n").Replace("\r", "\n");
        Alignments = alignments is null
            ? new Dictionary<int, ParagraphAlignment>()
            : new Dictionary<int, ParagraphAlignment>(alignments);

        _paragraphCount = Text.Split('\n').Length;

        foreach (var index in Alignments.Keys) {
            if (index < 0 || index >= _paragraphCount) {
                throw new ArgumentOutOfRangeException(
                    nameof(alignments),
                    $"Paragraph {index} does not exist in a document of {_paragraphCount} paragraphs.");
            }
        }
    }

    /// <summary>The document's text, with lines broken by <c>\n</c>.</summary>
    public string Text { get; }

    /// <summary>The paragraphs that are not flush left, by paragraph number.</summary>
    public IReadOnlyDictionary<int, ParagraphAlignment> Alignments { get; }

    /// <summary>How many paragraphs the document has: one per run of lines between breaks.</summary>
    public int ParagraphCount => _paragraphCount;

    /// <summary>
    /// How a paragraph is set: its own choice if it has one, flush left if
    /// it does not.
    /// </summary>
    /// <param name="paragraph">The paragraph's number, counting from zero.</param>
    /// <returns>The way the paragraph's lines are set.</returns>
    public ParagraphAlignment AlignmentOf(int paragraph) =>
        Alignments.TryGetValue(paragraph, out var alignment)
            ? alignment
            : ParagraphAlignment.Left;
}
