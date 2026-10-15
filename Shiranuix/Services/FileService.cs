using System.Text;
using Shiranuix.Models;
using Shiranuix.Utilities;

namespace Shiranuix.Services;

/// <summary>
/// The real file service, backed by <see cref="File"/>.
/// </summary>
/// <remarks>
/// Reads and writes UTF-8 without a byte-order mark. A BOM is a legacy
/// artefact that confuses `diff` and `git status`, and no editor in 2026
/// needs one; detecting it on read and omitting it on write keeps files
/// clean whether or not the author's previous tool added one.
///
/// Every path here works in pieces: a scan counts as it reads, a range is
/// copied out of the stream, and a save streams the source file through,
/// laying the document's moves over it as they fall — so the file's size
/// never becomes the size of a string. Break conversion runs over that same
/// stream, character by character, so a file written here reads back with
/// every line on the convention the status bar named, exactly as if the
/// whole text had been normalised in one string first.
/// </remarks>
public sealed class FileService : IFileService {
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <inheritdoc />
    public ScanResult Scan(string path) {
        using var source = OpenReader(path);

        var words = 0;
        var characters = 0;
        var newlines = 0;
        var inWord = false;
        var sawCrLf = false;
        var sawContent = false;

        Span<char> buffer = stackalloc char[4096];
        int read;

        while ((read = source.Read(buffer)) > 0) {
            for (var index = 0; index < read; index++) {
                var character = buffer[index];
                characters++;
                sawContent = true;

                if (char.IsWhiteSpace(character)) {
                    inWord = false;
                } else if (!inWord) {
                    inWord = true;
                    words++;
                }

                if (character == '\n') {
                    newlines++;
                } else if (character == '\r' && IsLfNext(buffer, index, read, source)) {
                    // Only a pair is the Windows convention: a carriage
                    // return at the end of a line on its own is not one, and
                    // this is the question Newline.Detect asks of a string.
                    sawCrLf = true;
                }
            }
        }

        return new ScanResult(
            words,
            characters,
            sawContent ? newlines + 1 : 0,
            sawCrLf ? Models.Newline.CrLf : Models.Newline.Lf);
    }

    /// <inheritdoc />
    public string ReadRange(string path, int from, int to) {
        if (from < 0) {
            throw new ArgumentOutOfRangeException(nameof(from));
        }

        if (to < from) {
            throw new ArgumentOutOfRangeException(nameof(to));
        }

        using var source = OpenReader(path);

        for (var index = 0; index < from; index++) {
            if (source.Read() == -1) {
                // Past the end of the file: the range there is empty.
                return string.Empty;
            }
        }

        var remaining = to - from;
        var text = new StringBuilder(remaining);
        Span<char> buffer = stackalloc char[4096];

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
    public string ReadFrom(string path, LineAnchor start, int chars) {
        if (start.Byte < 0) {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        if (chars <= 0) {
            return string.Empty;
        }

        using var source = OpenReaderAt(path, start.Byte);

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

        using var source = OpenReaderAt(path, start.Byte);

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

            bytes += Utf8NoBom.GetByteCount(buffer[..(index < read ? index + 1 : index)]);

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
            // The file ran out first: the last window ends at its very end.
            end = new LineAnchor(new FileInfo(path).Length, offset);
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
    public LineMove MoveLines(string path, LineAnchor from, int lines) {
        if (lines == 0) {
            return new LineMove(from, 0);
        }

        if (lines < 0) {
            return MoveBack(path, from, -lines);
        }

        using var source = OpenReaderAt(path, from.Byte);

        var offset = from.Offset;
        var bytes = from.Byte;
        var found = 0;
        Span<char> buffer = stackalloc char[4096];
        int read;

        while (found < lines && (read = source.Read(buffer)) > 0) {
            var index = 0;

            for (; index < read; index++) {
                offset++;

                if (buffer[index] == '\n') {
                    found++;

                    if (found == lines) {
                        break;
                    }
                }
            }

            bytes += Utf8NoBom.GetByteCount(buffer[..(index < read ? index + 1 : index)]);

            if (index < read) {
                break;
            }
        }

        return new LineMove(
            found >= lines
                ? new LineAnchor(bytes, offset)
                : new LineAnchor(new FileInfo(path).Length, offset),
            found);
    }

    /// <inheritdoc />
    public LineMove LineStartOf(string path, LineAnchor near, int offset) =>
        offset < near.Offset
            ? LineStartBackward(path, near, offset)
            : LineStartForward(path, near, offset);

    /// <inheritdoc />
    public ScanResult WriteCombined(
        string targetPath,
        string? sourcePath,
        IReadOnlyList<TextReplacement> replacements,
        Newline newline) {
        ArgumentNullException.ThrowIfNull(replacements);
        ArgumentNullException.ThrowIfNull(newline);

        // The temporary lands beside the target so the final move stays on
        // one volume — and the target itself is written last, in one step,
        // so a failure part-way leaves the file that was there standing.
        var fullPath = Path.GetFullPath(targetPath);
        var tempPath = Path.Combine(
            Path.GetDirectoryName(fullPath)!,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try {
            ScanResult result;

            using (var writer = new StreamWriter(tempPath, append: false, Utf8NoBom)) {
                var breaks = new BreakWriter(writer, newline);

                if (sourcePath is null) {
                    foreach (var replacement in replacements) {
                        breaks.Write(replacement.Text);
                    }
                } else {
                    using var source = OpenReader(sourcePath);
                    var written = 0L;

                    foreach (var replacement in replacements) {
                        if (replacement.Start < written) {
                            throw new InvalidDataException(
                                "Replacements must come in the order they fall in the file.");
                        }

                        breaks.Copy(source, replacement.Start - written);
                        breaks.Write(replacement.Text);
                        breaks.Skip(source, replacement.Length);
                        written = replacement.Start + replacement.Length;
                    }

                    breaks.CopyRest(source);
                }

                breaks.Flush();

                // Counted from the same pass that wrote the bytes, so the
                // document's baseline is exactly what a scan would now say —
                // reading the file back would cost the whole save again.
                result = breaks.Result();
            }

            File.Move(tempPath, fullPath, overwrite: true);

            return result;
        } catch {
            try {
                File.Delete(tempPath);
            } catch {
                // The failure being reported is the one that stopped the
                // write; a temp file nothing created needs no reporting.
            }

            throw;
        }
    }

    /// <inheritdoc />
    public bool Exists(string path) => File.Exists(path);

    /// <summary>
    /// Opens a file for reading as UTF-8, the way <see cref="ReadRange"/>
    /// and <see cref="Scan"/> both mean to.
    /// </summary>
    /// <param name="path">The file to open.</param>
    /// <returns>A reader positioned at the first character.</returns>
    private static StreamReader OpenReader(string path) =>
        new(path, Utf8NoBom, detectEncodingFromByteOrderMarks: false);

    /// <summary>
    /// Reports whether the character after the one at the buffer's
    /// <paramref name="index"/> is a line feed — from the buffer when it is
    /// still holding it, from the stream ahead when the pair straddles the
    /// chunk's end.
    /// </summary>
    private static bool IsLfNext(Span<char> buffer, int index, int read, StreamReader source) =>
        index + 1 < read ? buffer[index + 1] == '\n' : source.Peek() == '\n';

    /// <summary>
    /// Opens a file at an exact character, by seeking to the byte the anchor
    /// carries — the one thing a windowed document cannot do by counting
    /// from the start.
    /// </summary>
    /// <param name="path">The file to open.</param>
    /// <param name="bytePosition">The byte to seek to; a character boundary
    /// the anchor guarantees.</param>
    /// <returns>A reader positioned at that character.</returns>
    private static StreamReader OpenReaderAt(string path, long bytePosition) {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        stream.Seek(bytePosition, SeekOrigin.Begin);

        // The byte was taken from a known boundary, so decoding from there
        // reads the same characters reading from the start would have.
        return new StreamReader(stream, Utf8NoBom, detectEncodingFromByteOrderMarks: false);
    }

    /// <summary>
    /// Reports whether a byte is the tail of a UTF-8 character rather than
    /// its first — never true of a boundary.
    /// </summary>
    private static bool IsContinuation(byte value) => (value & 0xC0) == 0x80;

    /// <summary>
    /// Steps backwards one line at a time from a line's start, each step
    /// asking where the line before it begins.
    /// </summary>
    private LineMove MoveBack(string path, LineAnchor from, int lines) {
        var current = from;
        var moved = 0;

        while (moved < lines && current.Offset > 0) {
            var step = LineStartOf(path, current, current.Offset - 1);

            if (step.At == current) {
                // No line stands before this one.
                break;
            }

            current = step.At;
            moved++;
        }

        return new LineMove(current, -moved);
    }

    /// <summary>
    /// Finds the line holding a character that stands at or after
    /// <paramref name="near"/>, walking the stream forward: the last break
    /// before the target is the start of its line.
    /// </summary>
    private static LineMove LineStartForward(string path, LineAnchor near, int offset) {
        using var source = OpenReaderAt(path, near.Byte);

        var current = near.Offset;
        var bytes = near.Byte;
        var passed = 0;
        var candidate = near;
        Span<char> buffer = stackalloc char[4096];
        int read;

        while (current < offset && (read = source.Read(buffer)) > 0) {
            var index = 0;

            for (; index < read && current < offset; index++, current++) {
                if (buffer[index] != '\n') {
                    continue;
                }

                candidate = new LineAnchor(
                    bytes + Utf8NoBom.GetByteCount(buffer[..(index + 1)]),
                    current + 1);
                passed++;
            }

            bytes += Utf8NoBom.GetByteCount(buffer[..index]);
        }

        // With no break before the target, its line is the one the walk
        // started on — and `passed` counts the lines crossed to reach it.
        return new LineMove(candidate, passed);
    }

    /// <summary>
    /// Finds the line holding a character that stands before
    /// <paramref name="near"/>, reading the file backwards in whole
    /// character chunks: the last break below the target is the start of
    /// its line.
    /// </summary>
    /// <remarks>
    /// The walk runs from a character boundary and each chunk is trimmed at
    /// both ends to boundaries — a continuation byte has no meaning where a
    /// character begins — so a chunk decodes to exactly the characters it
    /// holds however far back it reaches. The count carries the direction:
    /// every break passed is a line below, and the break that ends the walk
    /// belongs to the target line, so it is not counted.
    /// </remarks>
    private static LineMove LineStartBackward(string path, LineAnchor near, int offset) {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var buffer = new byte[4096];

        var high = near.Byte;
        var highOffset = near.Offset;
        var passed = 0;

        while (high > 0) {
            var low = Math.Max(0, high - buffer.Length);
            stream.Seek(low, SeekOrigin.Begin);
            stream.ReadExactly(buffer.AsSpan(0, (int)(high - low)));

            // Trim the chunk's front to a character's first byte: at most
            // three bytes back from the cut, a character cannot reach further.
            var start = 0;

            if (low > 0) {
                while (start < 4 && IsContinuation(buffer[start])) {
                    start++;
                }
            }

            var text = Utf8NoBom.GetString(buffer, start, (int)(high - low) - start);
            var textOffset = highOffset - text.Length;

            for (var index = text.Length - 1; index >= 0; index--) {
                if (text[index] != '\n') {
                    continue;
                }

                if (textOffset + index < offset) {
                    var atByte = low + start + Utf8NoBom.GetByteCount(text[..index]) + 1;

                    return new LineMove(new LineAnchor(atByte, textOffset + index + 1), -passed);
                }

                passed++;
            }

            highOffset = textOffset;
            high = low + start;
        }

        // Nothing stands below the target: it lives on the first line.
        return new LineMove(new LineAnchor(0, 0), -passed);
    }

    /// <summary>
    /// Writes characters into a file while converting their line breaks to
    /// one convention, holding a carriage return just long enough to learn
    /// whether a line feed follows it, and counting what it writes the way
    /// <see cref="Scan"/> counts a file.
    /// </summary>
    /// <remarks>
    /// The hold is the whole of <see cref="Newline.Apply"/>'s first pass: a
    /// pair is one break, a lone carriage return is one break, and a pair
    /// straddling the seam between a copied stretch and a move's text is
    /// still one break — so what this writes matches what normalising the
    /// assembled text in one string would have written, character for
    /// character. The count rides along the same way: characters as they
    /// land, a word the moment a letter after whitespace appears, a line
    /// per break written — so a save hands back what scanning its result
    /// would say, without reading the file once more.
    /// </remarks>
    private sealed class BreakWriter(TextWriter writer, Newline newline) {
        private bool _pendingCarriageReturn;
        private bool _inWord;
        private int _words;
        private int _characters;
        private int _newlines;

        /// <summary>Writes a move's text through the converter.</summary>
        /// <param name="text">The text to write.</param>
        public void Write(string text) {
            foreach (var character in text) {
                Step(character);
            }
        }

        /// <summary>
        /// Copies <paramref name="count"/> characters of the source through
        /// the converter, stopping early if the file runs out.
        /// </summary>
        /// <param name="source">The file being copied from.</param>
        /// <param name="count">How many characters to take.</param>
        public void Copy(StreamReader source, long count) {
            Span<char> buffer = stackalloc char[4096];

            while (count > 0) {
                var slice = buffer[..(int)Math.Min(buffer.Length, count)];
                var read = source.Read(slice);

                if (read == 0) {
                    return;
                }

                for (var index = 0; index < read; index++) {
                    Step(buffer[index]);
                }

                count -= read;
            }
        }

        /// <summary>
        /// Passes over <paramref name="count"/> characters of the source
        /// without writing any of them — the span a move takes the place of —
        /// stopping early if the file runs out.
        /// </summary>
        /// <param name="source">The file being copied from.</param>
        /// <param name="count">How many characters to drop.</param>
        public void Skip(StreamReader source, long count) {
            Span<char> buffer = stackalloc char[4096];

            while (count > 0) {
                var slice = buffer[..(int)Math.Min(buffer.Length, count)];
                var read = source.Read(slice);

                if (read == 0) {
                    return;
                }

                count -= read;
            }
        }

        /// <summary>
        /// Copies the rest of the source through the converter, to the end
        /// of the file.
        /// </summary>
        /// <param name="source">The file being copied from.</param>
        public void CopyRest(StreamReader source) => Copy(source, long.MaxValue);

        /// <summary>
        /// Writes out a held carriage return, if the stream ended on one.
        /// </summary>
        public void Flush() {
            if (_pendingCarriageReturn) {
                _pendingCarriageReturn = false;
                EmitBreak();
            }
        }

        /// <summary>
        /// Reports the counts and convention of everything written so far —
        /// what <see cref="Scan"/> would say of the file.
        /// </summary>
        public ScanResult Result() => new(
            _words,
            _characters,
            _characters > 0 ? _newlines + 1 : 0,
            _newlines > 0 && newline == Models.Newline.CrLf
                ? Models.Newline.CrLf
                : Models.Newline.Lf);

        /// <summary>Runs one character through the conversion.</summary>
        /// <param name="character">The character to convert.</param>
        private void Step(char character) {
            if (_pendingCarriageReturn) {
                _pendingCarriageReturn = false;

                if (character == '\n') {
                    EmitBreak();
                    return;
                }

                // The pair had no second half: the carriage return was a
                // break of its own, and this character stands alone.
                EmitBreak();
            }

            switch (character) {
                case '\r':
                    // A carriage return ends a word whether or not a line
                    // feed follows it — both outcomes are a break.
                    _pendingCarriageReturn = true;
                    _inWord = false;
                    break;
                case '\n':
                    EmitBreak();
                    break;
                default:
                    if (char.IsWhiteSpace(character)) {
                        _inWord = false;
                    } else if (!_inWord) {
                        _inWord = true;
                        _words++;
                    }

                    _characters++;
                    writer.Write(character);
                    break;
            }
        }

        /// <summary>Writes one line break in the target convention.</summary>
        private void EmitBreak() {
            writer.Write(newline.Text);
            _characters += newline.Text.Length;
            _newlines++;
            _inWord = false;
        }
    }
}
