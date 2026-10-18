using System.Buffers.Binary;
using System.Text;

namespace Shiranuix.CoreFormat;

/// <summary>
/// Reads and writes the .shiranux format - the app's own file, carrying
/// the text and how each of its paragraphs is set.
/// </summary>
/// <remarks>
/// <para>
/// The file opens with a mark and a number: the mark "SHIRANUX", so a
/// wrong file is caught at the door, and the version it was written at, so
/// a future shape can still be read by an app that knows the older one.
/// After them comes a run of tagged sections. Each section says what it
/// holds by its tag, how long it is in bytes, and then its bytes - a
/// length-prefixed shape that lets a reader skip a section it does not
/// know without losing its place, which is how the format grows a new kind
/// of section without breaking the app written before it.
/// </para>
/// <para>
/// Two sections live at version one: the text, as UTF-8 letters with lines
/// broken by <c>\n</c> - the same letters a .txt file would carry - and
/// the alignments, a run of five-byte records, each naming a paragraph and
/// the single byte that says how it is set. A paragraph that is flush left
/// carries no record at all: left is what a page of prose starts at, and
/// most of a page is prose.
/// </para>
/// </remarks>
public static class ShiranuxFormat {
    /// <summary>The mark the file opens with, so a wrong file is caught at once.</summary>
    private static readonly byte[] Mark = "SHIRANUX"u8.ToArray();

    /// <summary>The shape this writer writes, and the oldest shape it still reads.</summary>
    private const uint CurrentVersion = 1;

    /// <summary>The section that holds the text.</summary>
    private const uint TextTag = 1;

    /// <summary>The section that holds the alignments.</summary>
    private const uint AlignmentsTag = 2;

    /// <summary>
    /// The furthest one section may reach: a corrupt length should fail at
    /// the door rather than ask for the machine's whole memory first.
    /// </summary>
    private const uint MaxSectionBytes = 256 * 1024 * 1024;

    /// <summary>
    /// Reads a document from a stream.
    /// </summary>
    /// <param name="stream">The stream to read from, at its start.</param>
    /// <returns>The document.</returns>
    /// <exception cref="InvalidDataException">
    /// The file does not open with the mark, or was written at a version
    /// this app does not know, or carries a section past what the format
    /// allows.
    /// </exception>
    /// <exception cref="EndOfStreamException">The file ends before its own shape promised.</exception>
    public static ShiranuxDocument Read(Stream stream) {
        ArgumentNullException.ThrowIfNull(stream);

        Span<byte> mark = stackalloc byte[Mark.Length];
        ReadExactly(stream, mark);
        if (!mark.SequenceEqual(Mark)) {
            throw new InvalidDataException(
                "The file does not open with the mark a .shiranux file carries.");
        }

        var version = ReadUInt32(stream);
        if (version > CurrentVersion) {
            throw new InvalidDataException(
                $"The file was written at version {version}, and this app reads up to version {CurrentVersion}.");
        }

        var sectionCount = ReadUInt32(stream);
        string? text = null;
        Dictionary<int, ParagraphAlignment>? alignments = null;

        for (var i = 0; i < sectionCount; i++) {
            var tag = ReadUInt32(stream);
            var length = ReadUInt32(stream);
            if (length > MaxSectionBytes) {
                throw new InvalidDataException(
                    $"A section of {length} bytes is past what this format carries.");
            }

            var payload = new byte[length];
            ReadExactly(stream, payload);

            if (tag == TextTag) {
                text = Encoding.UTF8.GetString(payload);
            } else if (tag == AlignmentsTag) {
                alignments = ReadAlignments(payload);
            }

            // Any other tag is a section this version does not know; the
            // length it carried has already been spent, so the next section
            // starts where it should.
        }

        // A record naming a paragraph that does not exist is a stray from a
        // corrupt file, and the text is worth more than the stray: the
        // alignments are kept only where the paragraphs are.
        var shell = new ShiranuxDocument(text ?? string.Empty);
        var kept = alignments is null
            ? null
            : alignments
                .Where(pair => pair.Key >= 0 && pair.Key < shell.ParagraphCount)
                .ToDictionary(pair => pair.Key, pair => pair.Value);

        return kept is null ? shell : new ShiranuxDocument(shell.Text, kept);
    }

    /// <summary>
    /// Writes a document to a stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="document">The document to write.</param>
    public static void Write(Stream stream, ShiranuxDocument document) {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(document);

        var text = Encoding.UTF8.GetBytes(document.Text);
        var alignments = WriteAlignments(document);

        stream.Write(Mark);
        WriteUInt32(stream, CurrentVersion);

        var sections = new List<(uint Tag, byte[] Payload)> {
            (TextTag, text),
            (AlignmentsTag, alignments)
        };

        WriteUInt32(stream, (uint)sections.Count);
        foreach (var (tag, payload) in sections) {
            WriteSection(stream, tag, payload);
        }
    }

    private static void ReadExactly(Stream stream, Span<byte> buffer) {
        var offset = 0;
        while (offset < buffer.Length) {
            var read = stream.Read(buffer[offset..]);
            if (read == 0) {
                throw new EndOfStreamException("The file ends before the shape it promised.");
            }
            offset += read;
        }
    }

    private static uint ReadUInt32(Stream stream) {
        Span<byte> bytes = stackalloc byte[4];
        ReadExactly(stream, bytes);
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    }

    private static void WriteUInt32(Stream stream, uint value) {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteSection(Stream stream, uint tag, byte[] payload) {
        WriteUInt32(stream, tag);
        WriteUInt32(stream, (uint)payload.Length);
        stream.Write(payload);
    }

    private static Dictionary<int, ParagraphAlignment> ReadAlignments(byte[] payload) {
        var alignments = new Dictionary<int, ParagraphAlignment>();
        var offset = 0;

        while (offset + 5 <= payload.Length) {
            var index = (int)BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(offset, 4));
            var alignment = (ParagraphAlignment)payload[offset + 4];

            // A byte this version does not name is left out rather than
            // guessed at: the paragraph falls back to flush left, which is
            // what it was before the file was opened. A record that names
            // flush left is left out for the same reason the writer never
            // puts one in - left is the shape of a paragraph that has not
            // been given a face of its own.
            if (alignment is ParagraphAlignment.Center or ParagraphAlignment.Right) {
                alignments[index] = alignment;
            }

            offset += 5;
        }

        return alignments;
    }

    private static byte[] WriteAlignments(ShiranuxDocument document) {
        var records = document.Alignments
            .Where(pair => pair.Value != ParagraphAlignment.Left)
            .OrderBy(pair => pair.Key)
            .ToArray();

        var payload = new byte[records.Length * 5];
        var offset = 0;

        foreach (var (index, alignment) in records) {
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(offset, 4), (uint)index);
            payload[offset + 4] = (byte)alignment;
            offset += 5;
        }

        return payload;
    }
}
