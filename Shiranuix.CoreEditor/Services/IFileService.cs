using Shiranuix.CoreFormat;
namespace Shiranuix.CoreEditor;

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
    /// <param name="typography">How the document is set — the typeface it
    /// names and the point size it stands at. Spent only by a target that
    /// can hold it; a <c>.txt</c> writes its letters and nothing else. Left
    /// out, a <c>.shiranux</c> keeps whatever the file it was written
    /// through was standing on, so a save can never be the moment a paper
    /// forgets the face it was drawn in.</param>
    /// <param name="alignments">How each line of the document is set, by
    /// its number in the source — the same numbering the moves carry.
    /// Spent only where a file can hold it; a <c>.txt</c> target writes its
    /// letters and nothing else. Left out, a <c>.shiranux</c> keeps whatever
    /// alignments the file it was written through already carried, carried
    /// to where their letters now stand.</param>
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
        Newline newline,
        DocumentTypography? typography = null,
        IReadOnlyDictionary<int, ParagraphAlignment>? alignments = null);

    /// <summary>
    /// Reads only what a file says about how it is set — the typeface it
    /// names and the point size it stands at — without reading its text.
    /// </summary>
    /// <param name="path">The file to ask.</param>
    /// <returns>
    /// How the file says it is set, or null when it says nothing: a plain
    /// file cannot, and an older one of the app's own may not.
    /// </returns>
    /// <remarks>
    /// Asked before a file is opened rather than as part of opening it,
    /// because a typeface the file names may be one this machine does not
    /// carry, and that has to be settled with whoever is opening the file
    /// before any of its words land.
    /// </remarks>
    DocumentTypography? ReadTypography(string path);

    /// <summary>
    /// Reads only how a file sets its lines — which stand centred or flush
    /// right, and where — without reading its text.
    /// </summary>
    /// <param name="path">The file to ask.</param>
    /// <returns>
    /// Each line that is not flush left, by its number in the file's text,
    /// or null when the file carries none: a plain file cannot hold any,
    /// and a file of the app's own that has never been aligned keeps no
    /// section at all.
    /// </returns>
    /// <remarks>
    /// The numbers say nothing beside the text they were written against,
    /// so these are read as part of opening a file rather than before it,
    /// against the same reading — an arrival, not a question asked ahead of
    /// one. The section is stepped over to rather than read through, so
    /// this costs a few bytes at the file's head however large it is.
    /// </remarks>
    IReadOnlyDictionary<int, ParagraphAlignment>? ReadAlignments(string path);

    /// <summary>
    /// Answers a document's whole writing as one string: the same
    /// composition a save lays over a file, without writing anything.
    /// </summary>
    /// <param name="sourcePath">The file to stream through, or null when
    /// there is no file behind the document.</param>
    /// <param name="replacements">The moves, in file coordinates, in the
    /// order they fall.</param>
    /// <returns>
    /// The whole writing, character for character: what the file holds and
    /// the moves lay over it, including the letters the window never shows.
    /// </returns>
    string ReadCombined(string? sourcePath, IReadOnlyList<TextReplacement> replacements);

    /// <summary>
    /// Reports whether a file exists.
    /// </summary>
    /// <param name="path">The path to test.</param>
    /// <returns>True when the file is present.</returns>
    bool Exists(string path);
}
