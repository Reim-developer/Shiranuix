using System.Globalization;

namespace Shiranuix.Graphs;

/// <summary>
/// The letter a character wears in their circle: the first of their name,
/// as an avatar wears it.
/// </summary>
/// <remarks>
/// The first letter and not the first character: a name that begins with a
/// letter outside the Latin alphabet — or with something that is more than
/// one character at all — is taken whole, so that the letter on the paper
/// is the letter the writer wrote and not half of it.
/// </remarks>
public static class GraphInitial {
    /// <summary>
    /// The letter a name is worn by: its first letter, standing up.
    /// </summary>
    /// <param name="name">What the character is called.</param>
    /// <returns>The letter, or nothing for a name that has none.</returns>
    public static string Of(string? name) {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0) {
            return string.Empty;
        }

        // A grapheme, not a code unit: the whole of the first letter, even
        // where the letter takes two units to write itself.
        var letter = StringInfo.GetNextTextElement(trimmed);
        var upper = letter.ToUpper(CultureInfo.CurrentCulture);

        // "ß" stands up as "SS"; a letter worn on a circle is one letter.
        // And the letter taken whole is taken whole: a letter that needs two
        // units to write itself is not cut in half by being stood up.
        var worn = StringInfo.GetNextTextElement(upper);

        return worn.Length > 0 ? worn : string.Empty;
    }
}
