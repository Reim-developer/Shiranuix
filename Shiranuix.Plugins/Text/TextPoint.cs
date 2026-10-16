namespace Shiranuix.Plugins.Text;

/// <summary>
/// A place in a document, said three ways at once: as an offset a replacement
/// can be made at, and as the line and column a person reads.
/// </summary>
/// <remarks>
/// Offsets count from zero because that is what a string and a selection in
/// every editor library agree on. Lines and columns count from one because
/// that is what a person reading a status bar expects, and it is the
/// convention this editor already keeps in its own caret arithmetic. Both are
/// kept together so a plugin never has to hold a document and count
/// newlines to answer "where am I".
/// </remarks>
public readonly record struct TextPoint {
    /// <summary>Creates a place in a document.</summary>
    /// <param name="offset">Where in the text, counted from zero.</param>
    /// <param name="line">Which line, counted from one.</param>
    /// <param name="column">Which column on that line, counted from one.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The offset is below zero, or the line or column is below one — the
    /// place a reader sees and the place a string holds do not both start at
    /// zero, and this is where that difference is settled.
    /// </exception>
    public TextPoint(int offset, int line, int column) {
        if (offset < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(offset), offset, "An offset counts from zero and goes no lower.");
        }

        if (line < 1 || column < 1) {
            throw new ArgumentOutOfRangeException(
                nameof(line), $"Lines and columns count from one: {line}, {column}.");
        }

        Offset = offset;
        Line = line;
        Column = column;
    }

    /// <summary>Where in the text, counted from zero.</summary>
    public int Offset { get; }

    /// <summary>Which line, counted from one.</summary>
    public int Line { get; }

    /// <summary>Which column on that line, counted from one.</summary>
    public int Column { get; }
}
