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
}
