namespace ReadingTime;

/// <summary>
/// How long a document takes to read, said the way a person would say it.
/// </summary>
/// <remarks>
/// A plugin brings its own arithmetic: nothing here reaches into the editor's
/// counting, because a plugin cannot see the editor's counting. That is the
/// price of a contract with no shared internals, and for a sample it is also
/// the point — the numbers below are the plugin's own opinion, and a plugin
/// author is free to disagree with the editor.
/// </remarks>
public static class ReadingTime {
    /// <summary>
    /// How many words a person reads in a minute, quietly, the way this one
    /// does.
    /// </summary>
    public const int WordsPerMinute = 220;

    /// <summary>
    /// Counts the words in a text.
    /// </summary>
    /// <param name="text">The text to count.</param>
    /// <returns>How many words it holds; zero for empty text.</returns>
    public static int Words(string text) {
        ArgumentNullException.ThrowIfNull(text);

        var words = 0;
        var inWord = false;

        foreach (var character in text) {
            if (char.IsWhiteSpace(character)) {
                inWord = false;
            } else if (!inWord) {
                // Whitespace runs separate words; punctuation does not, so
                // "one, two" is two words and not three.
                inWord = true;
                words++;
            }
        }

        return words;
    }

    /// <summary>
    /// How long a document takes to read.
    /// </summary>
    /// <param name="words">How many words it holds.</param>
    /// <returns>
    /// The minutes it takes, rounded up: half a minute is still time spent,
    /// and a document that takes no time at all is not a document.
    /// </returns>
    public static int Minutes(int words) => words <= 0
        ? 0
        : (words + WordsPerMinute - 1) / WordsPerMinute;

    /// <summary>
    /// The sentence to put in the status line.
    /// </summary>
    /// <param name="words">How many words the document holds.</param>
    /// <returns>A sentence worth showing a writer.</returns>
    public static string Sentence(int words) {
        if (words <= 0) {
            return "Nothing to read yet.";
        }

        var minutes = Minutes(words);

        return minutes <= 1
            ? $"{words} words — about a minute."
            : $"{words} words — about {minutes} minutes.";
    }
}
