namespace Shiranuix.Models;

/// <summary>
/// A document's newline convention: what a line break is written as, and what
/// it is called.
/// </summary>
/// <remarks>
/// A file opened on Windows is usually CRLF and one written here is usually LF,
/// and the difference is invisible on screen but permanent on disk. A document
/// keeps the convention it arrived in, so saving does not quietly rewrite every
/// line break in the file.
/// </remarks>
public sealed class Newline {
    /// <summary>The Unix convention: a line feed on its own.</summary>
    public static readonly Newline Lf = new("LF", "\n");

    /// <summary>The Windows convention: a carriage return, then a line feed.</summary>
    public static readonly Newline CrLf = new("CRLF", "\r\n");

    /// <summary>
    /// The convention this operating system writes its own files in: CRLF on
    /// Windows, a line feed on its own everywhere else. A document nobody has
    /// opened yet starts here, so a file written on this machine looks the way
    /// the machine's other tools expect.
    /// </summary>
    public static readonly Newline Default =
        Environment.NewLine == "\r\n" ? CrLf : Lf;

    private Newline(string name, string text) {
        Name = name;
        Text = text;
    }

    /// <summary>What the status bar calls it.</summary>
    public string Name { get; }

    /// <summary>What it is written as.</summary>
    public string Text { get; }

    /// <summary>
    /// Reads the convention a piece of text is written in. A text whose first
    /// break is CRLF is CRLF; anything else is LF, including a text with no
    /// breaks at all.
    /// </summary>
    /// <param name="text">The text to inspect.</param>
    /// <returns>The convention found.</returns>
    public static Newline Detect(string? text) =>
        text?.Contains("\r\n", StringComparison.Ordinal) == true ? CrLf : Lf;

    /// <summary>
    /// Rewrites every line break in a piece of text to this convention.
    /// </summary>
    /// <param name="text">The text to normalise.</param>
    /// <returns>The text, with every break the same.</returns>
    public string Apply(string text) {
        // Through LF first: a document edited on both conventions would
        // otherwise keep a stray carriage return at the end of half its lines.
        var uniform = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        return Text == "\n"
            ? uniform
            : uniform.Replace("\n", "\r\n", StringComparison.Ordinal);
    }

    /// <summary>The convention, by the name the status bar shows.</summary>
    /// <param name="name">"LF" or "CRLF".</param>
    /// <returns>The convention.</returns>
    public static Newline FromName(string name) => name == CrLf.Name ? CrLf : Lf;
}
