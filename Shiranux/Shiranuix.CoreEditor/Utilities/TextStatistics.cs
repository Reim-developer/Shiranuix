namespace Shiranuix.CoreEditor;

/// <summary>
/// Pure text measurements for the editor's status bar.
/// </summary>
/// <remarks>
/// Everything here is a total function over a string, so it can be checked
/// without a window, a file, or a cursor. Vietnamese is handled correctly
/// because words are split on whitespace rather than on ASCII letters: a
/// syllable like "không" carries diacritics but is still one word, and a
/// multi-syllable word separated by spaces ("người đó") counts as the two
/// words a writer would expect.
/// </remarks>
public static class TextStatistics {
    /// <summary>
    /// Counts words, characters, and lines.
    /// </summary>
    /// <param name="text">The document text; null counts as empty.</param>
    /// <returns>A snapshot of the counts.</returns>
    /// <remarks>
    /// One pass over the text: the three questions are asked of each
    /// character together, because this is the whole window's text re-read
    /// on every keystroke — walking it twice would make every keystroke pay
    /// for the text twice.
    /// </remarks>
    public static TextStatisticsResult Measure(string? text) {
        if (string.IsNullOrEmpty(text)) {
            return new TextStatisticsResult(0, 0, 0);
        }

        var words = 0;
        var lines = 1;
        var inWord = false;

        foreach (var c in text) {
            if (Newline.IsLineFeed(c)) {
                lines++;
            }

            if (char.IsWhiteSpace(c)) {
                inWord = false;
            } else if (!inWord) {
                inWord = true;
                words++;
            }
        }

        return new TextStatisticsResult(words, text.Length, lines);
    }

    /// <summary>
    /// Counts words: runs of non-whitespace characters.
    /// </summary>
    /// <param name="text">The document text.</param>
    /// <returns>The number of words.</returns>
    public static int Words(string text) => Measure(text).Words;

    /// <summary>
    /// Counts lines the way a writer sees them: every newline starts a new
    /// line, and trailing text with no final newline still counts as one.
    /// </summary>
    /// <param name="text">The document text.</param>
    /// <returns>The number of lines.</returns>
    public static int Lines(string text) => Measure(text).Lines;
}

/// <summary>
/// An immutable snapshot of a document's counts.
/// </summary>
/// <param name="Words">Number of words.</param>
/// <param name="Characters">Number of characters, including whitespace.</param>
/// <param name="Lines">Number of lines.</param>
public readonly record struct TextStatisticsResult(int Words, int Characters, int Lines);
