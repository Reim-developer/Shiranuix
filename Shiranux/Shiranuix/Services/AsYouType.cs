using Shiranuix.Models;

namespace Shiranuix.Services;

/// <summary>
/// What one stroke of the keyboard becomes: the character that lands, and
/// how much of what already stands it rewrites.
/// </summary>
/// <param name="Text">The characters to put down instead of what was typed.</param>
/// <param name="Replaces">
/// How many characters before the caret the turn stands for - none when the
/// stroke itself is turned, one when a pair of hyphens becomes a dash.
/// </param>
public readonly record struct Turn(string Text, int Replaces);

/// <summary>
/// The writing's habits, as the page keeps them: which of the plain marks
/// the keyboard gives turn as they are typed, and which stay.
/// </summary>
/// <param name="SmartQuotes">Whether the quotes take their curly selves.</param>
/// <param name="Dash">What a pair of hyphens becomes.</param>
public readonly record struct TypingRules(bool SmartQuotes, DashMode Dash);

/// <summary>
/// The writing's small courtesies, judged before the page sees them: the
/// quote that knows which side it stands on, the pair of hyphens that
/// becomes the dash the settings name.
/// </summary>
/// <remarks>
/// Nothing here touches the page: the answer is a turn for the window's
/// ear, or none at all when the stroke stays as it was typed. Which side a
/// quote stands on is read from the one character before the caret - a word
/// or one of the pauses that ends a clause closes it, and everywhere speech
/// or thought has just been shut, it opens again - because a page is
/// written forward, and the rules do not reach further back than the
/// writer's last stroke.
/// </remarks>
public static class AsYouType {
    /// <summary>
    /// Answers what one stroke becomes on a page that stands after this
    /// character, under these rules: a turn to put down in its place, or
    /// nothing when the stroke stays as it was typed.
    /// </summary>
    /// <param name="typed">The character the stroke produced.</param>
    /// <param name="before">
    /// The character the caret stands after, or null when it stands at the
    /// page's beginning.
    /// </param>
    /// <param name="rules">The writing's habits this page keeps.</param>
    public static Turn? Stroke(char typed, char? before, TypingRules rules) {
        if (rules.SmartQuotes && typed is '"' or '\'') {
            return new Turn(Quote(typed, QuoteOpens(before)), 0);
        }

        if (rules.Dash != DashMode.Off && typed == '-' && before == '-') {
            return new Turn(rules.Dash == DashMode.En ? "\u2013" : "\u2014", 1);
        }

        return null;
    }

    /// <summary>
    /// The curly mark of the quote's kind: the doubled one takes the wide
    /// pair, the tick the narrow one, and each takes the side its opening
    /// asks for.
    /// </summary>
    private static string Quote(char typed, bool open) =>
        open
            ? (typed == '"' ? '\u201C' : '\u2018').ToString()
            : (typed == '"' ? '\u201D' : '\u2019').ToString();

    /// <summary>
    /// Whether the quote the caret stands after opens: everywhere speech or
    /// thought is not already shut, a quote opens.
    /// </summary>
    private static bool QuoteOpens(char? before) =>
        before is null || !QuoteCloses(before.Value);

    /// <summary>
    /// Whether a quote standing after a character closes: after a word, a
    /// number, a tick already put down inside one, an opening the same
    /// stroke is paired with, or one of the pauses that ends a clause.
    /// </summary>
    private static bool QuoteCloses(char before) =>
        char.IsLetterOrDigit(before) ||
        before is '\'' or '\u2019' ||
        before is '\u201C' or '\u2018' ||
        before is '.' or ',' or ';' or ':' or '!' or '?' or '\u2026' ||
        before is ')' or ']' or '}';
}
