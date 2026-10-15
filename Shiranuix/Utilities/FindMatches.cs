using System.Text.RegularExpressions;

namespace Shiranuix.Utilities;

/// <summary>
/// One occurrence found in the text: where it begins and how long it runs.
/// </summary>
/// <param name="Start">The occurrence's position in the text, in characters.</param>
/// <param name="Length">The occurrence's length, in characters — zero for a query that is not there.</param>
public readonly record struct FindMatch(int Start, int Length) {
    /// <summary>The empty occurrence, used when there is no match at all.</summary>
    public static FindMatch None => new(-1, 0);
}

/// <summary>
/// What a find run could say about a query that cannot be searched as it
/// stands.
/// </summary>
public enum FindQueryError {
    /// <summary>The query is well-formed and was searched.</summary>
    None,
    /// <summary>An empty query names nothing.</summary>
    Empty,
    /// <summary>The query is a regular expression that does not compile.</summary>
    BadRegex
}

/// <summary>
/// Finds every occurrence of a query in a piece of text, without looking
/// at it from a document — a pure function, so it can be tested on its own
/// and run off the user-interface thread.
/// </summary>
/// <remarks>
/// The three options that can be switched on correspond to the three
/// toggles in the find bar: case sensitivity, whole word, and treating the
/// query as a regular expression. With a regular expression the case and
/// whole-word switches still shape it; with plain text the query is treated
/// as literal characters, every one of them.
/// </remarks>
public static class FindMatches {
    /// <summary>
    /// Finds every occurrence of <paramref name="query"/> in
    /// <paramref name="text"/>.
    /// </summary>
    /// <param name="text">The text to search.</param>
    /// <param name="query">The query, plain or regex.</param>
    /// <param name="matchCase">Whether uppercase and lowercase differ.</param>
    /// <param name="wholeWord">Whether the match must be a complete word.</param>
    /// <param name="useRegex">Whether the query is a regular expression.</param>
    /// <param name="error">What was wrong with the query, if anything.</param>
    /// <returns>The occurrences, in their order in the text.</returns>
    public static IReadOnlyList<FindMatch> Find(
        string text,
        string query,
        bool matchCase,
        bool wholeWord,
        bool useRegex,
        out FindQueryError error) {

        error = FindQueryError.None;
        if (string.IsNullOrEmpty(query)) {
            return Array.Empty<FindMatch>();
        }

        string pattern;
        try {
            if (useRegex) {
                pattern = wholeWord ? $@"\b(?:{query})\b" : query;
            } else {
                pattern = wholeWord ? $@"\b{Regex.Escape(query)}\b" : Regex.Escape(query);
            }

            var options = matchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
            options |= RegexOptions.CultureInvariant;

            var regex = new Regex(pattern, options);
            var matches = regex.Matches(text);
            var result = new List<FindMatch>(matches.Count);
            for (int i = 0; i < matches.Count; i++) {
                result.Add(new FindMatch(matches[i].Index, matches[i].Length));
            }
            return result;
        } catch (ArgumentException) {
            error = FindQueryError.BadRegex;
            return Array.Empty<FindMatch>();
        }
    }
}
