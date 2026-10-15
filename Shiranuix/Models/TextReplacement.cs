namespace Shiranuix.Models;

/// <summary>
/// One move against a file: the span it replaces, and the text that takes
/// the span's place.
/// </summary>
/// <remarks>
/// Coordinates are positions in the file the replacements are written
/// against — not in the text as it currently stands — because that is the
/// only numbering that survives every move before it. The replaced length
/// travels with the start, so the writer can copy past the span without
/// asking anyone how long it was.
/// </remarks>
public readonly record struct TextReplacement(int Start, int Length, string Text);
