using System.Text;
using Shiranuix.Models;
using Shiranuix.Utilities;

namespace Shiranuix.Services;

/// <summary>
/// Saves a document as a whole file: the source streamed through with the
/// document's moves laid over it as they fall, on one convention, counted
/// as the bytes land.
/// </summary>
/// <remarks>
/// The file's size never becomes the size of a string — a scan counts as it
/// reads, a move's text is written where its span falls, and everything
/// between is copied through untouched. Break conversion runs over that same
/// stream, character by character, so a file written here reads back with
/// every line on the convention the status bar named, exactly as if the
/// whole text had been normalised in one string first. The temporary lands
/// beside the target so the final move stays on one volume, and the target
/// itself is written last, in one step, so a failure part-way leaves the
/// file that was there standing.
/// </remarks>
public static class FileCombinedWriter {
    /// <summary>
    /// Writes a file's new contents: the moves over the source, or the
    /// moves alone when the document has no file behind it yet.
    /// </summary>
    /// <param name="targetPath">Where to write.</param>
    /// <param name="sourcePath">The file to stream through, or null for a
    /// document not yet saved anywhere.</param>
    /// <param name="replacements">The document's moves, in the order they
    /// fall in the file.</param>
    /// <param name="newline">The convention every break is written on.</param>
    /// <returns>What a scan would say of the file just written.</returns>
    public static ScanResult WriteCombined(
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

            using (var writer = new StreamWriter(tempPath, append: false, Utf8File.Text)) {
                var breaks = new BreakWriter(writer, newline);

                if (sourcePath is null) {
                    foreach (var replacement in replacements) {
                        breaks.Write(replacement.Text);
                    }
                } else {
                    using var source = Utf8File.OpenReader(sourcePath);
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

    /// <summary>
    /// Answers a document's whole writing as one string: the source streamed
    /// through with the document's moves laid over it as they fall.
    /// </summary>
    /// <param name="sourcePath">The file to read through, or null for a
    /// document not yet saved anywhere.</param>
    /// <param name="replacements">The document's moves, in the order they
    /// fall in the file.</param>
    /// <returns>
    /// The whole writing, character for character: every letter the moves
    /// do not touch comes off the file itself, therefore letters the window
    /// never shows arrive under their own names. The breaks stay as they
    /// were written — the words carry their own breaks, and nothing here
    /// was asked to rewrite them.
    /// </returns>
    public static string ReadCombined(
        string? sourcePath,
        IReadOnlyList<TextReplacement> replacements) {
        ArgumentNullException.ThrowIfNull(replacements);

        var text = new StringBuilder();

        if (sourcePath is null) {
            foreach (var replacement in replacements) {
                text.Append(replacement.Text);
            }

            return text.ToString();
        }

        using var source = Utf8File.OpenReader(sourcePath);
        var written = 0L;

        foreach (var replacement in replacements) {
            if (replacement.Start < written) {
                throw new InvalidDataException(
                    "Replacements must come in the order they fall in the file.");
            }

            Take(source, replacement.Start - written, text);
            text.Append(replacement.Text);
            PassOver(source, replacement.Length);
            written = replacement.Start + replacement.Length;
        }

        Take(source, long.MaxValue, text);

        return text.ToString();

        static void Take(StreamReader source, long count, StringBuilder text) {
            Span<char> buffer = stackalloc char[4096];

            while (count > 0) {
                var slice = buffer[..(int)Math.Min(buffer.Length, count)];
                var read = source.Read(slice);

                if (read == 0) {
                    return;
                }

                text.Append(slice[..read]);
                count -= read;
            }
        }

        static void PassOver(StreamReader source, long count) {
            Span<char> buffer = stackalloc char[4096];

            while (count > 0) {
                var read = source.Read(buffer[..(int)Math.Min(buffer.Length, count)]);

                if (read == 0) {
                    return;
                }

                count -= read;
            }
        }
    }

    /// <summary>
    /// Writes characters into a file while converting their line breaks to
    /// one convention, holding a carriage return just long enough to learn
    /// whether a line feed follows it, and counting what it writes the way
    /// <see cref="FileScanner.Scan"/> counts a file.
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
        /// what <see cref="FileScanner.Scan"/> would say of the file.
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
