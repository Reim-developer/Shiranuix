namespace Shiranuix.Models;

/// <summary>
/// The document's typography and the colour theme it should be drawn in,
/// exactly as the user left them.
/// </summary>
/// <remarks>
/// Doubles as the single row of the settings table: the store writes these
/// three fields and nothing else, so the persisted shape and the in-memory
/// one are the same type rather than two that must be kept in step.
/// </remarks>
public sealed class UserSettings {
    /// <summary>
    /// The row's primary key. Fixed at 1 because there is only ever one row:
    /// the store reads it, updates it, or inserts it.
    /// </summary>
    public int Id { get; set; } = 1;

    /// <summary>The typeface the document is set in.</summary>
    public string FontFamily { get; set; } = string.Empty;

    /// <summary>The point size the document is set in.</summary>
    public double FontSize { get; set; }

    /// <summary>The requested colour theme.</summary>
    public ThemeMode ThemeMode { get; set; }

    /// <summary>
    /// Whether the document's lines wrap within the sheet's width, or run on
    /// past it. Off by default: a writing tool's default is a page, and not a
    /// ribbon.
    /// </summary>
    public bool WordWrap { get; set; }

    /// <summary>
    /// Which way the page's lines are set: flush left, centred, or flush
    /// right. Left by default: a page of prose starts where it is written.
    /// </summary>
    public TextAlign TextAlign { get; set; }

    /// <summary>
    /// Whether the page's lines are set in a bold face. Off by default: a
    /// page is written down before it is dressed up.
    /// </summary>
    public bool Bold { get; set; }

    /// <summary>
    /// Whether the page's lines are set in an italic face.
    /// </summary>
    public bool Italic { get; set; }
}
