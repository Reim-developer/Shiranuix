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
    /// Writes a UTF-8 text file from a source file with the document's moves
    /// laid over it, converting every line break to
    /// <paramref name="newline"/> on the way.
    /// </summary>
    /// <param name="targetPath">The file to write.</param>
    /// <param name="sourcePath">The file the unmoved text comes from, or
    /// null when there is no file behind the document — a blank page, or a
    /// first save whose every character is a move.</param>
    /// <param name="replacements">The moves, in file coordinates, in the
    /// order they fall.</param>
    /// <param name="newline">The convention to write every break in.</param>
    /// <remarks>
    /// The source is streamed, not held: bytes before the first move are
    /// copied as they are read, each move's text is written in its place,
    /// and the rest follows — so the whole file passes through memory in
    /// pieces. The write lands in a temporary file and replaces the target
    /// in one move, so a failure part-way through leaves the previous file
    /// standing instead of half a document.
    /// </remarks>
    void WriteCombined(
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
