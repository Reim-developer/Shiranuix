using System.Text;

namespace Shiranuix.Services;

/// <summary>
/// The UTF-8 every path through this project reads and writes with: no
/// byte-order mark, and the two ways of opening a file the scans, the
/// walks and the saves all share.
/// </summary>
/// <remarks>
/// A BOM is a legacy artefact that confuses `diff` and `git status`, and no
/// editor in 2026 needs one; detecting it on read and omitting it on write
/// keeps files clean whether or not the author's previous tool added one.
/// The one encoding lives here so the reader a scan opens and the reader a
/// save copies through can never disagree about what a file is written in.
/// </remarks>
public static class Utf8File {
    /// <summary>The encoding every reader and writer here uses.</summary>
    public static readonly UTF8Encoding Text = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Opens a file for reading as UTF-8 from its first character.
    /// </summary>
    /// <param name="path">The file to open.</param>
    /// <returns>A reader positioned at the first character.</returns>
    public static StreamReader OpenReader(string path) =>
        new(path, Text, detectEncodingFromByteOrderMarks: false);

    /// <summary>
    /// Opens a file at an exact character, by seeking to the byte the anchor
    /// carries — the one thing a windowed document cannot do by counting
    /// from the start.
    /// </summary>
    /// <param name="path">The file to open.</param>
    /// <param name="bytePosition">The byte to seek to; a character boundary
    /// the anchor guarantees.</param>
    /// <returns>A reader positioned at that character.</returns>
    public static StreamReader OpenReaderAt(string path, long bytePosition) {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        stream.Seek(bytePosition, SeekOrigin.Begin);

        // The byte was taken from a known boundary, so decoding from there
        // reads the same characters reading from the start would have.
        return new StreamReader(stream, Text, detectEncodingFromByteOrderMarks: false);
    }
}
