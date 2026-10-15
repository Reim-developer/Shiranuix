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
    public void WriteCombined(
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
            }

            File.Move(tempPath, fullPath, overwrite: true);
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
    /// Writes characters into a file while converting their line breaks to
    /// one convention, holding a carriage return just long enough to learn
    /// whether a line feed follows it.
    /// </summary>
    /// <remarks>
    /// The hold is the whole of <see cref="Newline.Apply"/>'s first pass: a
    /// pair is one break, a lone carriage return is one break, and a pair
    /// straddling the seam between a copied stretch and a move's text is
    /// still one break — so what this writes matches what normalising the
    /// assembled text in one string would have written, character for
    /// character.
    /// </remarks>
    private sealed class BreakWriter(TextWriter writer, Newline newline) {
        private bool _pendingCarriageReturn;

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
                    _pendingCarriageReturn = true;
                    break;
                case '\n':
                    EmitBreak();
                    break;
                default:
                    writer.Write(character);
                    break;
            }
        }

        /// <summary>Writes one line break in the target convention.</summary>
        private void EmitBreak() => writer.Write(newline.Text);
    }
}
