using Shiranuix.CoreFormat;

namespace Shiranuix.CoreEditor;

/// <summary>
/// The UTF-8 every path through this project reads and writes with: no
/// byte-order mark, and the ways of opening a file the scans, the walks and
/// the saves all share.
/// </summary>
/// <remarks>
/// <para>
/// A BOM is a legacy artefact that confuses `diff` and `git status`, and no
/// editor in 2026 needs one; detecting it on read and omitting it on write
/// keeps files clean whether or not the author's previous tool added one.
/// The one encoding lives in the text package, so the reader a scan opens
/// and the reader a save copies through can never disagree about what a
/// file is written in — everything here asks that package rather than
/// spelling a second opinion.
/// </para>
/// <para>
/// The two file shapes meet here and nowhere else. A <c>.txt</c> is the
/// letters themselves, so its bytes and its text are the same thing. A
/// <c>.shiranuix</c> carries those letters between a header and the
/// alignments that follow, so its bytes and its text are not: a walk that
/// counted the file's own bytes would count the header as letters and stop
/// at the end of the alignments rather than the end of the writing. Every
/// reader below therefore opens the text alone — the one place the file is
/// asked which of the two it is — and every path after that is shared.
/// </para>
/// </remarks>
public static class Utf8File {
    /// <summary>The extension of the app's own binary document.</summary>
    private const string ShiranuixExtension = ".shiranuix";

    /// <summary>
    /// Whether a path names the app's own file: the shape carrying the text
    /// and how each of its paragraphs is set.
    /// </summary>
    /// <param name="path">The path to ask.</param>
    /// <returns>True for <c>.shiranuix</c>, however the extension is
    /// capitalised; false for everything else.</returns>
    public static bool IsShiranuix(string path) =>
        path.EndsWith(ShiranuixExtension, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Opens a file for reading as UTF-8 from its first character.
    /// </summary>
    /// <param name="path">The file to open.</param>
    /// <returns>A reader positioned at the first character.</returns>
    /// <remarks>
    /// For a <c>.shiranuix</c> the first character is the first letter of
    /// the writing, after the header — the reader stops at the end of the
    /// writing, where the alignments begin, and never sees either.
    /// </remarks>
    public static StreamReader OpenReader(string path) =>
        IsShiranuix(path)
            ? new StreamReader(OpenShiranuix(path, 0), TextFile.Encoding, detectEncodingFromByteOrderMarks: false)
            : TextFile.OpenReader(path);

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
        if (IsShiranuix(path)) {
            return new StreamReader(
                OpenShiranuix(path, bytePosition), TextFile.Encoding, detectEncodingFromByteOrderMarks: false);
        }

        // The byte was taken from a known boundary, so decoding from there
        // reads the same characters reading from the start would have.
        return TextFile.OpenReaderAt(path, bytePosition);
    }

    /// <summary>
    /// Opens a file's text as raw bytes, for the one walk that reads the
    /// bytes themselves rather than the characters they spell.
    /// </summary>
    /// <param name="path">The file to open.</param>
    /// <returns>A readable, seekable stream over the text alone.</returns>
    /// <remarks>
    /// A <c>.txt</c> is handed its own file; a <c>.shiranuix</c> is handed
    /// the text section bounded, so a walk that measures bytes measures the
    /// same bytes the anchors count from and never reaches into the
    /// alignments.
    /// </remarks>
    public static Stream OpenBytes(string path) =>
        IsShiranuix(path)
            ? OpenShiranuix(path, 0)
            : TextFile.OpenBytes(path);

    /// <summary>
    /// How many bytes of text a file holds: the file's whole length for a
    /// <c>.txt</c>, the text section's length for a <c>.shiranuix</c>.
    /// </summary>
    /// <param name="path">The file to measure.</param>
    /// <returns>The byte count of the writing.</returns>
    public static long TextLength(string path) =>
        IsShiranuix(path)
            ? Locate(path).Length
            : new FileInfo(path).Length;

    /// <summary>
    /// Opens the text of a <c>.shiranuix</c> as a bounded stream of bytes,
    /// starting at a byte counted from the writing's own first letter.
    /// </summary>
    /// <param name="path">The file to open.</param>
    /// <param name="bytePosition">Where to start, in the text's own bytes.</param>
    /// <returns>The bounded stream, positioned there.</returns>
    private static BoundedStream OpenShiranuix(string path, long bytePosition) {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        try {
            var (offset, length) = ShiranuixFormat.LocateText(stream);
            var bounded = new BoundedStream(stream, offset, length) {
                Position = Math.Clamp(bytePosition, 0, length)
            };

            return bounded;
        } catch {
            // The file was open only to be looked at, and looking at it is
            // what failed: it should not be left open behind the failure.
            stream.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Asks a <c>.shiranuix</c> where its text stands, from a stream opened
    /// for the purpose.
    /// </summary>
    private static (long Offset, long Length) Locate(string path) {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return ShiranuixFormat.LocateText(stream);
    }
}
