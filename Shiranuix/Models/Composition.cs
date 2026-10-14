namespace Shiranuix.Models;

/// <summary>
/// A character the input method is composing: drawn in the editor, not yet in
/// the text.
/// </summary>
/// <remarks>
/// A Vietnamese writer's first "â" exists twice for a moment — underlined at
/// the caret while the input method makes up its mind, and in the document only
/// once it commits. The caret on screen stands inside that composition, so line
/// and column read against the page as drawn rather than against the buffer as
/// committed; the writer's column is the column they can see.
///
/// The shape mirrors what the editor's presenter does with it: the composition
/// sits at the caret, and the cursor stands where the input method says it
/// does — or after the whole composition when it says nothing, which is where a
/// writer reads their caret while typing a diacritic.
/// </remarks>
public readonly record struct Composition(string Text, int Caret) {
    /// <summary>Nothing is being composed: no input method is at work.</summary>
    public static Composition None => new(string.Empty, 0);

    /// <summary>Whether nothing is being composed.</summary>
    public bool IsEmpty => string.IsNullOrEmpty(Text);

    /// <summary>
    /// Reads what an input method reports into a composition.
    /// </summary>
    /// <param name="text">The composing text, or null when nothing composes.</param>
    /// <param name="caret">
    /// The cursor's position inside the composition, as the input method
    /// reports it. Null, negative, or past the end means the end of the
    /// composition — the same fallback the editor draws its caret with.
    /// </param>
    /// <returns>The composition, or <see cref="None"/>.</returns>
    public static Composition Of(string? text, int? caret) {
        if (string.IsNullOrEmpty(text)) {
            return None;
        }

        return new Composition(text, caret is >= 0 && caret <= text.Length ? caret.Value : text.Length);
    }
}
