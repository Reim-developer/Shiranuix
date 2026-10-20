using System.Text;
using Shiranuix.CoreFormat;
using Shiranuix.Models;
using Shiranuix.Utilities;

namespace Shiranuix.Services;

/// <summary>
/// The real file service, backed by <see cref="File"/>: the facade the
/// document talks to, over the four pieces that do the work in pieces.
/// </summary>
/// <remarks>
/// Reading and writing here never make the file's size into the size of a
/// string — <see cref="FileScanner"/> counts as it reads,
/// <see cref="FileLineWalker"/> walks the breaks without the text, and
/// <see cref="FileCombinedWriter"/> streams a save through with the moves
/// laid over it as they fall. Only the two reads the window opens with
/// stay below: a stretch of characters from an anchor, and the lines that
/// follow it.
/// </remarks>
public sealed class FileService : IFileService {
    /// <inheritdoc />
    public ScanResult Scan(string path) => FileScanner.Scan(path);

    /// <inheritdoc />
    public string ReadFrom(string path, LineAnchor start, int chars) {
        if (start.Byte < 0) {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        if (chars <= 0) {
            return string.Empty;
        }

        using var source = Utf8File.OpenReaderAt(path, start.Byte);

        var text = new StringBuilder(chars);
        Span<char> buffer = stackalloc char[4096];
        var remaining = chars;

        while (remaining > 0) {
            var slice = buffer[..(int)Math.Min(buffer.Length, remaining)];
            var read = source.Read(slice);

            if (read == 0) {
                break;
            }

            text.Append(slice[..read]);
            remaining -= read;
        }

        return text.ToString();
    }

    /// <inheritdoc />
    public LineRead ReadLines(string path, LineAnchor start, int lines) {
        if (start.Byte < 0) {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        using var source = Utf8File.OpenReaderAt(path, start.Byte);

        var offset = start.Offset;
        var bytes = start.Byte;
        var found = 0;
        var text = new StringBuilder();
        Span<char> buffer = stackalloc char[4096];
        var read = 0;
        var index = 0;

        while (found < lines && (read = source.Read(buffer)) > 0) {
            index = 0;

            for (; index < read; index++) {
                var character = buffer[index];
                text.Append(character);
                offset++;

                if (character == '\n') {
                    found++;

                    if (found == lines) {
                        break;
                    }
                }
            }

            bytes += Utf8File.Text.GetByteCount(buffer[..(index < read ? index + 1 : index)]);

            if (index < read) {
                break;
            }
        }

        LineAnchor end;
        bool more;

        if (found >= lines) {
            // Stopped on the lines-th newline: the line after them begins
            // exactly there, and the file may well carry on.
            end = new LineAnchor(bytes, offset);

            // The reader ran ahead of the walk — it fills its own buffer —
            // so what stands after the newline is either still in this
            // chunk or still ahead of the stream.
            more = index + 1 < read || source.Peek() != -1;
        } else {
            // The file ran out first: the last window ends at the writing's
            // own end — the byte after the last letter, not the byte after
            // whatever else the file carries around them.
            end = new LineAnchor(Utf8File.TextLength(path), offset);
            more = false;
        }

        // The window holds no newline that ends its last line — that line
        // would draw one the box cannot show.
        if (text.Length > 0 && text[^1] == '\n') {
            text.Length--;
        }

        return new LineRead(text.ToString(), new LineMove(end, found), more);
    }

    /// <inheritdoc />
    public LineMove MoveLines(string path, LineAnchor from, int lines) =>
        FileLineWalker.MoveLines(path, from, lines);

    /// <inheritdoc />
    public LineMove LineStartOf(string path, LineAnchor near, int offset) =>
        FileLineWalker.LineStartOf(path, near, offset);

    /// <inheritdoc />
    public ScanResult WriteCombined(
        string targetPath,
        string? sourcePath,
        IReadOnlyList<TextReplacement> replacements,
        Newline newline,
        DocumentTypography? typography = null,
        IReadOnlyDictionary<int, ParagraphAlignment>? alignments = null) =>
        FileCombinedWriter.WriteCombined(
            targetPath, sourcePath, replacements, newline, typography, alignments);

    /// <inheritdoc />
    public DocumentTypography? ReadTypography(string path) {
        // A plain file is letters and nothing else, so there is nothing to
        // ask it: only the app's own file carries a shape to speak of.
        if (!Utf8File.IsShiranux(path)) {
            return null;
        }

        using var container = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return ShiranuxFormat.ReadTypography(container);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<int, ParagraphAlignment>? ReadAlignments(string path) {
        // The same answer as the typeface's own question for the same
        // reason: a plain file holds its letters and nothing else, so
        // there is no way it could be setting any of its lines.
        if (!Utf8File.IsShiranux(path)) {
            return null;
        }

        using var container = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return ShiranuxFormat.ReadAlignments(container);
    }

    /// <inheritdoc />
    public string ReadCombined(string? sourcePath, IReadOnlyList<TextReplacement> replacements) =>
        FileCombinedWriter.ReadCombined(sourcePath, replacements);

    /// <inheritdoc />
    public bool Exists(string path) => File.Exists(path);
}
