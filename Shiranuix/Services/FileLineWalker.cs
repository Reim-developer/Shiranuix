using Shiranuix.Models;

namespace Shiranuix.Services;

/// <summary>
/// Walks a file's lines without reading its text: how far a number of lines
/// is in bytes and characters, and where a line begins under a character.
/// </summary>
/// <remarks>
/// Every walk works in pieces — a chunk of the stream at a time, counted as
/// it passes — so the file's size never becomes the size of a string, and
/// the anchors it hands back carry both the byte a window seeks to and the
/// character position the caret counts from. The direction lives in the
/// counts: forward walks count up, the walk back counts below.
/// </remarks>
public static class FileLineWalker {
    /// <summary>
    /// Moves a number of lines from a line's start: forward past their
    /// breaks, or back to the starts of the ones before it.
    /// </summary>
    /// <param name="path">The file to walk.</param>
    /// <param name="from">The line the walk starts on.</param>
    /// <param name="lines">How many lines: forward is positive, back is
    /// negative.</param>
    /// <returns>Where the walk stopped, and how many lines it actually
    /// moved.</returns>
    public static LineMove MoveLines(string path, LineAnchor from, int lines) {
        if (lines == 0) {
            return new LineMove(from, 0);
        }

        if (lines < 0) {
            return MoveBack(path, from, -lines);
        }

        using var source = Utf8File.OpenReaderAt(path, from.Byte);

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

            bytes += Utf8File.Text.GetByteCount(buffer[..(index < read ? index + 1 : index)]);

            if (index < read) {
                break;
            }
        }

        return new LineMove(
            found >= lines
                ? new LineAnchor(bytes, offset)
                : new LineAnchor(Utf8File.TextLength(path), offset),
            found);
    }

    /// <summary>
    /// Finds the line holding a character that stands at or after
    /// <paramref name="near"/>, walking the stream forward: the last break
    /// before the target is the start of its line.
    /// </summary>
    /// <param name="path">The file to walk.</param>
    /// <param name="near">The line the walk starts on.</param>
    /// <param name="offset">The character the line is wanted for.</param>
    /// <returns>The line's start, and how many lines the walk crossed.</returns>
    public static LineMove LineStartForward(string path, LineAnchor near, int offset) {
        using var source = Utf8File.OpenReaderAt(path, near.Byte);

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
                    bytes + Utf8File.Text.GetByteCount(buffer[..(index + 1)]),
                    current + 1);
                passed++;
            }

            bytes += Utf8File.Text.GetByteCount(buffer[..index]);
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
    public static LineMove LineStartBackward(string path, LineAnchor near, int offset) {
        // The text alone, as bytes: the anchors this walk hands back are
        // counted from the writing's first letter, and for a file that wraps
        // its letters in a header those are not the file's own first bytes.
        using var stream = Utf8File.OpenBytes(path);
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

            var text = Utf8File.Text.GetString(buffer, start, (int)(high - low) - start);
            var textOffset = highOffset - text.Length;

            for (var index = text.Length - 1; index >= 0; index--) {
                if (text[index] != '\n') {
                    continue;
                }

                if (textOffset + index < offset) {
                    var atByte = low + start + Utf8File.Text.GetByteCount(text[..index]) + 1;

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
    /// Steps backwards one line at a time from a line's start, each step
    /// asking where the line before it begins.
    /// </summary>
    private static LineMove MoveBack(string path, LineAnchor from, int lines) {
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
    /// Finds the line holding a character, walking whichever way from
    /// <paramref name="near"/> the character actually stands.
    /// </summary>
    /// <param name="path">The file to walk.</param>
    /// <param name="near">The line the walk starts on.</param>
    /// <param name="offset">The character the line is wanted for.</param>
    /// <returns>The line's start, and how many lines the walk crossed.</returns>
    public static LineMove LineStartOf(string path, LineAnchor near, int offset) =>
        offset < near.Offset
            ? LineStartBackward(path, near, offset)
            : LineStartForward(path, near, offset);

    /// <summary>
    /// Reports whether a byte is the tail of a UTF-8 character rather than
    /// its first — never true of a boundary.
    /// </summary>
    private static bool IsContinuation(byte value) => (value & 0xC0) == 0x80;
}
