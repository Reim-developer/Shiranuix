using Shiranuix.Models;
using Shiranuix.Utilities;

namespace Shiranuix.Services;

/// <summary>
/// Reads and writes the document's bytes to and from disk, in the pieces a
/// windowed document needs.
/// </summary>
/// <remarks>
/// Kept behind an interface so the view model can be exercised headlessly --
/// the tests substitute an in-memory implementation and never touch the file
/// system, which is the same split Shiranuix uses for its services.
///
/// Nothing here takes the whole file as a string: a count is a pass over the
/// stream, a window is a range of it, and a save is the file it was opened
/// from with the moves since laid over the top. A file larger than memory is
/// therefore countable, readable and writable without ever being held whole.
/// </remarks>
public interface IFileService {
    /// <summary>
    /// Counts a file and reads its line-break convention in one pass,
    /// keeping none of its text.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The counts the status bar would show for the whole file, and
    /// the convention its lines break on.</returns>
    ScanResult Scan(string path);

    /// <summary>
    /// Reads a range of a UTF-8 text file: the characters from
    /// <paramref name="from"/> up to but not including <paramref name="to"/>.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <param name="from">The first character to read.</param>
    /// <param name="to">The character to stop before.</param>
    /// <returns>The range asked for, shortened by whatever runs out first.</returns>
    string ReadRange(string path, int from, int to);

    /// <summary>
    /// Reads <paramref name="chars"/> characters from an exact position,
    /// seeking straight there rather than counting from the file's start.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <param name="start">Where to read from — a byte the stream seeks to.</param>
    /// <param name="chars">How many characters to take.</param>
    /// <returns>The characters asked for, shortened by whatever runs out first.</returns>
    string ReadFrom(string path, LineAnchor start, int chars);

    /// <summary>
    /// Reads a fixed number of whole lines from an exact position: the
    /// window a large document shows, in one pass that both takes the text
    /// and works out where the next window would begin.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <param name="start">The first line's start — a line boundary in both units.</param>
    /// <param name="lines">How many lines to read.</param>
    /// <returns>The lines' text without the newline ending the last of them,
    /// the line after them, and whether the file continues past them.</returns>
    LineRead ReadLines(string path, LineAnchor start, int lines);

    /// <summary>
    /// Steps a number of lines from an exact position without reading the
    /// text between — forward over the stream, backward over the bytes —
    /// to land on another line's start.
    /// </summary>
    /// <param name="path">The file to walk.</param>
    /// <param name="from">The line to step from.</param>
    /// <param name="lines">How many lines: negative steps backwards.</param>
    /// <returns>The line reached and how many lines were actually moved,
    /// which falls short when the file runs out first.</returns>
    LineMove MoveLines(string path, LineAnchor from, int lines);

    /// <summary>
    /// Finds the start of the line holding a character, walking backwards
    /// from an anchor no higher than it — used when a window's edge must
    /// snap out over an entry it would otherwise cut in half.
    /// </summary>
    /// <param name="path">The file to walk.</param>
    /// <param name="near">A line at or below <paramref name="offset"/> to walk from.</param>
    /// <param name="offset">The character whose line is wanted.</param>
    /// <returns>That line's start, and how many lines below
    /// <paramref name="near"/> it stands.</returns>
    LineMove LineStartOf(string path, LineAnchor near, int offset);

    /// <summary>
    /// Writes a UTF-8 text file from a source file with the document's moves
    /// laid over it, converting every line break to
    /// <paramref name="newline"/> on the way, and counts what it wrote.
    /// </summary>
    /// <param name="targetPath">The file to write.</param>
    /// <param name="sourcePath">The file the unmoved text comes from, or
    /// null when there is no file behind the document — a blank page, or a
    /// first save whose every character is a move.</param>
    /// <param name="replacements">The moves, in file coordinates, in the
    /// order they fall.</param>
    /// <param name="newline">The convention to write every break in.</param>
    /// <returns>The counts and convention of the file just written — what
    /// <see cref="Scan"/> would say of it — taken from the same pass, so the
    /// document's baseline stays exact without reading the file back.</returns>
    /// <remarks>
    /// The source is streamed, not held: bytes before the first move are
    /// copied as they are read, each move's text is written in its place,
    /// and the rest follows — so the whole file passes through memory in
    /// pieces. The write lands in a temporary file and replaces the target
    /// in one move, so a failure part-way through leaves the previous file
    /// standing instead of half a document.
    /// </remarks>
    ScanResult WriteCombined(
        string targetPath,
        string? sourcePath,
        IReadOnlyList<TextReplacement> replacements,
        Newline newline);

    /// <summary>
    /// Reports whether a file exists.
    /// </summary>
    /// <param name="path">The path to test.</param>
    /// <returns>True when the file is present.</returns>
    bool Exists(string path);
}
