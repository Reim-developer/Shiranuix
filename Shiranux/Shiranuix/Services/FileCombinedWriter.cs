using System.Text;
using Shiranuix.CoreFormat;
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
    /// <param name="newline">The convention every break is written on; a
    /// <c>.shiranux</c> target spends no convention of its own and always
    /// writes line feeds.</param>
    /// <param name="typography">How the document is set. Spent only where a
    /// file can hold it; a <c>.txt</c> target keeps its letters and nothing
    /// else, and a null for a <c>.shiranux</c> keeps whatever the source
    /// file was standing on rather than dropping it on the floor.</param>
    /// <param name="alignments">How each line of the document is set, by
    /// its number in the whole writing as it now stands — the same numbers
    /// the gutter shows, so every entry lands beside the very letters it
    /// was chosen over. Spent only where a file can hold it. Left out, a
    /// <c>.shiranux</c> target keeps whatever alignments the source file
    /// already carried, carried from the source's own numbering to where
    /// their letters now stand.</param>
    /// <returns>What a scan would say of the file just written.</returns>
    public static ScanResult WriteCombined(
        string targetPath,
        string? sourcePath,
        IReadOnlyList<TextReplacement> replacements,
        Newline newline,
        DocumentTypography? typography = null,
        IReadOnlyDictionary<int, ParagraphAlignment>? alignments = null) {
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

            if (Utf8File.IsShiranux(fullPath)) {
                // The app's own file is a different shape: the writing has
                // to be in hand before it can be laid into the binary, so
                // this path composes first rather than streaming.
                result = WriteShiranux(tempPath, sourcePath, replacements, typography, alignments);
            } else {
                using (var writer = new StreamWriter(tempPath, append: false, TextFile.Encoding)) {
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
    /// Writes the app's own file: the whole writing composed, the map laid
    /// into the binary beside it, then both laid down.
    /// </summary>
    /// <param name="tempPath">The temporary to build, beside the target.</param>
    /// <param name="sourcePath">The file to read through, or null for a
    /// document not yet saved anywhere.</param>
    /// <param name="replacements">The document's moves, in the order they
    /// fall in the file.</param>
    /// <param name="typography">How the document is set, or null to keep
    /// whatever the source file was standing on.</param>
    /// <param name="alignments">How each line of the document is set, by
    /// its number in the whole writing as it now stands, or null to keep
    /// whatever the source file already carried.</param>
    /// <returns>What a scan would say of the file just written.</returns>
    /// <remarks>
    /// <para>
    /// The shape of this file is the reason it cannot stream like a
    /// <c>.txt</c> does: the text section's length travels ahead of its
    /// bytes, so the writing has to exist before the container can say how
    /// long it is. Composing it as one string is the cost, and it is the
    /// whole cost — nothing else here reads more than it must.
    /// </para>
    /// <para>
    /// The map arrives from the document when it has any, and it arrives
    /// already numbered against this very writing — the same numbers the
    /// gutter shows, since the document carries its faces along at every
    /// move — so it is laid into the binary as it stands, each entry
    /// beside the letters it was chosen over. A writer that brought none
    /// keeps the one the source container already carried, and that one
    /// still speaks in the source's own numbers: each of its ways of
    /// setting a line is carried through the moves and re-counted against
    /// the writing that came out, the same walk the letters themselves
    /// took. A source that is not a <c>.shiranux</c> — a <c>.txt</c>, or
    /// no file at all — carries none of its own, so there is nothing to
    /// carry either way.
    /// </para>
    /// <para>
    /// The convention argument is spent here: this file's lines are always
    /// written as a line feed, which is what the format promises and what
    /// the reader it will be opened by expects. A document whose status bar
    /// says CRLF is written on line feeds regardless, and the save reports
    /// line feeds back so the status bar tells the truth about the file.
    /// </para>
    /// </remarks>
    private static ScanResult WriteShiranux(
        string tempPath,
        string? sourcePath,
        IReadOnlyList<TextReplacement> replacements,
        DocumentTypography? typography,
        IReadOnlyDictionary<int, ParagraphAlignment>? alignments) {
        var composed = ReadCombined(sourcePath, replacements);

        IReadOnlyDictionary<int, ParagraphAlignment>? aligned = null;
        var set = typography;
        ShiranuxDocument? source = null;

        if (sourcePath is not null && Utf8File.IsShiranux(sourcePath)) {
            using var container = new FileStream(
                sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            source = ShiranuxFormat.Read(container);

            // The document's own set wins; a writer that brought none keeps
            // the one the file already carried, so saving can never be the
            // moment a paper forgets what it was drawn in.
            set ??= source.Typography;
        }

        if (alignments is { Count: > 0 }) {
            // The document's own map lands as it stands: its numbers were
            // worked against this very writing as the document made it —
            // the same numbers the gutter shows — so every entry is laid
            // beside the letters it was set over, with no carrying left
            // for the save to do.
            aligned = alignments;
        } else if (source?.Alignments is { Count: > 0 }) {
            // A writer that brought none keeps the one the file already
            // carried, and carries it from the source's own numbering:
            // each way of setting a line moves with its own letters, the
            // line's opening position walked through the moves and
            // re-counted against the writing that came out.
            aligned = AlignmentRebaser.Rebase(
                source.Text, source.Alignments, replacements, composed);
        }

        var document = new ShiranuxDocument(composed, aligned, set);

        using (var target = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
            ShiranuxFormat.Write(target, document);
        }

        // Measured from the text as it now stands — the same letters a scan
        // of this file reads back through the bounded reader — so the counts
        // and the convention are exactly what reopening would say.
        var measured = TextStatistics.Measure(document.Text);
        return new ScanResult(measured.Words, measured.Characters, measured.Lines, Newline.Lf);
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
            _newlines > 0 && newline == Newline.CrLf
                ? Newline.CrLf
                : Newline.Lf);

        /// <summary>Runs one character through the conversion.</summary>
        /// <param name="character">The character to convert.</param>
        private void Step(char character) {
            if (_pendingCarriageReturn) {
                _pendingCarriageReturn = false;

                if (Newline.IsLineFeed(character)) {
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
