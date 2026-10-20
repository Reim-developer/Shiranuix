namespace Shiranuix.CoreEditor;

/// <summary>
/// A place in a text: which line and which column, counted from one.
/// </summary>
/// <remarks>
/// A value, because "line 3, column 12" is a fact about a place and not an
/// object with a history. The arithmetic for it lives here too: counting
/// newlines up to an offset is the same question however the caret moved there
/// - typing, clicking, or a stale binding offering an offset past the end.
/// </remarks>
public readonly record struct TextPosition(int Line, int Column) {
    /// <summary>Where a new document's caret starts.</summary>
    public static TextPosition Start => new(1, 1);

    /// <summary>
    /// Reads the place an offset into a text points at.
    /// </summary>
    /// <param name="text">The text the offset is into.</param>
    /// <param name="offset">The offset of the caret.</param>
    /// <returns>The position, with an offset past the end clamped to it.</returns>
    /// <remarks>
    /// A carriage return is only ever half a break, so it does not count as one
    /// - which is the same reason <see cref="Newline"/> normalises through LF.
    /// </remarks>
    public static TextPosition Of(string text, int offset) {
        ArgumentNullException.ThrowIfNull(text);

        var caret = Math.Clamp(offset, 0, text.Length);
        var line = 1;
        var column = 1;

        for (var index = 0; index < caret; index++) {
            if (text[index] == '\n') {
                line++;
                column = 1;
            } else {
                column++;
            }
        }

        return new TextPosition(line, column);
    }
}
