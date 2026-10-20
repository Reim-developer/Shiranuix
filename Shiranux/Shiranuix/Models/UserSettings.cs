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
    /// Whether the quotes the writer types take their curly selves: the
    /// opening one after a pause, the closing one after a word. True by
    /// default: the page is written in prose, and prose wears the curly
    /// ones.
    /// </summary>
    public bool SmartQuotes { get; set; } = true;

    /// <summary>
    /// The dash a pair of hyphens becomes as the writer types. Em by
    /// default: in prose the pair stands for the dash that breaks a
    /// sentence, and the sentence takes the long one.
    /// </summary>
    public DashMode DashMode { get; set; } = DashMode.Em;

    /// <summary>
    /// Whether the empty sheet is greeted by the start screen — the heading,
    /// the words under it, New and Open, and the files the writer came back
    /// to. True by default: a page that greets is how a writing tool opens,
    /// until the writer says otherwise.
    /// </summary>
    public bool ShowWelcomeMessage { get; set; } = true;

    /// <summary>
    /// The typeface the last export to PDF was drawn in. Empty when the
    /// export dialog has never been answered: the next export prefills with
    /// what the page itself is set in.
    /// </summary>
    public string PdfFontFamily { get; set; } = string.Empty;

    /// <summary>
    /// The point size the last export to PDF was drawn at. Zero when the
    /// export dialog has never been answered: the next export prefills with
    /// the size the page itself is set at.
    /// </summary>
    public double PdfFontSize { get; set; }
}
