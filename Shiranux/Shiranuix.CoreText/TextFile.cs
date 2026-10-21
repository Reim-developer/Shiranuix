using System.Text;

namespace Shiranuix.CoreText;

/// <summary>
/// The encoding every plain file in this repository is read and written
/// with — UTF-8, without a byte-order mark — and the ways of opening one
/// that every reader shares.
/// </summary>
/// <remarks>
/// A BOM is a legacy artefact that confuses `diff` and `git status`, and no
/// editor in 2026 needs one. The encoding lives here, stated once, so a
/// scan, a walk and a save can never disagree about what a file is written
/// in, and none of them can spell the answer for itself. The openers below
/// are the ordinary file's own; a shape that carries more than its letters
/// — the app's own document, with its alignments behind the text — answers
/// for where those letters stand in its own project, and still comes here
/// for the encoding they are read with.
/// </remarks>
public static class TextFile {
    /// <summary>The encoding every reader and writer here uses: UTF-8, no BOM.</summary>
    public static readonly UTF8Encoding Encoding = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Opens a plain file for reading as UTF-8 from its first character.
    /// </summary>
    /// <param name="path">The file to open.</param>
    /// <returns>A reader positioned at the first character.</returns>
    public static StreamReader OpenReader(string path) =>
        new(path, Encoding, detectEncodingFromByteOrderMarks: false);

    /// <summary>
    /// Opens a plain file at an exact character, by seeking to the byte the
    /// caller carries — the one thing a windowed document cannot do by
    /// counting from the start.
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
        return new StreamReader(stream, Encoding, detectEncodingFromByteOrderMarks: false);
    }

    /// <summary>
    /// Opens a plain file as raw bytes, for the one walk that reads the
    /// bytes themselves rather than the characters they spell.
    /// </summary>
    /// <param name="path">The file to open.</param>
    /// <returns>A readable, seekable stream over the file's own bytes.</returns>
    public static Stream OpenBytes(string path) =>
        new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
}
