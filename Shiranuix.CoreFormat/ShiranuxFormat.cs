using System.Buffers.Binary;
using System.Text;

namespace Shiranuix.CoreFormat;

/// <summary>
/// Reads and writes the .shiranux format - the app's own file, carrying
/// the text, how each of its paragraphs is set, and how the paper as a
/// whole is set.
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
/// Three sections live at version one: the text, as UTF-8 letters with lines
/// broken by <c>\n</c> - the same letters a .txt file would carry - the
/// alignments, a run of five-byte records, each naming a paragraph and
/// the single byte that says how it is set, and the type, a name and a
/// point size saying what the whole paper is drawn in. A paragraph that is
/// flush left carries no record at all: left is what a page of prose starts
/// at, and most of a page is prose. A paper that says nothing about its
/// type carries no third section, and the reader it reaches keeps whatever
/// type it was already standing on.
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

    /// <summary>The section that holds the typeface and the point size.</summary>
    private const uint TypographyTag = 3;

    /// <summary>
    /// The furthest a point size may reach, so a corrupt size is read as
    /// no size at all rather than as a size no page could ever stand at.
    /// </summary>
    private const double MaxPointSize = 4096;

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
        DocumentTypography? typography = null;

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
            } else if (tag == TypographyTag) {
                typography = ReadTypography(payload);
            }

            // Any other tag is a section this version does not know; the
            // length it carried has already been spent, so the next section
            // starts where it should.
        }

        // A record naming a paragraph that does not exist is a stray from a
        // corrupt file, and the text is worth more than the stray: the
        // alignments are kept only where the paragraphs are.
        var shell = new ShiranuxDocument(text ?? string.Empty, null, typography);
        var kept = alignments is null
            ? null
            : alignments
                .Where(pair => pair.Key >= 0 && pair.Key < shell.ParagraphCount)
                .ToDictionary(pair => pair.Key, pair => pair.Value);

        return kept is null
            ? shell
            : new ShiranuxDocument(shell.Text, kept, typography);
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
        var typography = WriteTypography(document);

        stream.Write(Mark);
        WriteUInt32(stream, CurrentVersion);

        var sections = new List<(uint Tag, byte[] Payload)> {
            (TextTag, text),
            (AlignmentsTag, alignments)
        };

        // The type travels only when there is one to travel: a document
        // holding no typeface writes the same two sections it always has,
        // which is what a file written before this section existed reads
        // back as anyway.
        if (typography is not null) {
            sections.Add((TypographyTag, typography));
        }

        WriteUInt32(stream, (uint)sections.Count);
        foreach (var (tag, payload) in sections) {
            WriteSection(stream, tag, payload);
        }
    }

    /// <summary>
    /// Finds where the text section stands in a stream, so a reader can
    /// stream the letters out without decoding the whole file first.
    /// </summary>
    /// <param name="stream">The stream to look through, at its start. Must
    /// be seekable, since a section it does not want is stepped over rather
    /// than read.</param>
    /// <returns>
    /// The byte the text begins at, and how many bytes it holds. A file
    /// with no text section has none: zero length at the position the
    /// sections ran out, which reads back as an empty document.
    /// </returns>
    /// <remarks>
    /// The same header checks <see cref="Read"/> makes — the mark, the
    /// version, the furthest a section may reach — so a wrong file fails
    /// here exactly as it would there. Only the section it came for is
    /// stopped at; the rest are stepped over.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    /// The file does not open with the mark, or was written at a version
    /// this app does not know, or carries a section past what the format
    /// allows.
    /// </exception>
    /// <exception cref="EndOfStreamException">The file ends before its own shape promised.</exception>
    public static (long Offset, long Length) LocateText(Stream stream) {
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

        for (var i = 0; i < sectionCount; i++) {
            var tag = ReadUInt32(stream);
            var length = ReadUInt32(stream);
            if (length > MaxSectionBytes) {
                throw new InvalidDataException(
                    $"A section of {length} bytes is past what this format carries.");
            }

            if (tag == TextTag) {
                return (stream.Position, length);
            }

            stream.Seek(length, SeekOrigin.Current);
        }

        return (stream.Position, 0);
    }

    /// <summary>
    /// Reads only what a file says about how it is set, without reading its
    /// letters: the typeface it names and the point size it stands at.
    /// </summary>
    /// <param name="stream">The stream to read from, at its start. Must be
    /// seekable, since a section it does not want is stepped over rather
    /// than read.</param>
    /// <returns>
    /// How the file says it is set, or null when it says nothing at all —
    /// an older file, a plain one, or a set section written in a shape this
    /// reader does not read.
    /// </returns>
    /// <remarks>
    /// The same header checks <see cref="Read"/> makes, so a wrong file
    /// fails here exactly as it would there. The text section — usually the
    /// largest part of the file — is stepped over rather than decoded, so
    /// asking what a large paper is set in costs a few bytes at its head
    /// rather than the whole of it.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    /// The file does not open with the mark, or was written at a version
    /// this app does not know, or carries a section past what the format
    /// allows.
    /// </exception>
    /// <exception cref="EndOfStreamException">The file ends before its own shape promised.</exception>
    public static DocumentTypography? ReadTypography(Stream stream) {
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

        for (var i = 0; i < sectionCount; i++) {
            var tag = ReadUInt32(stream);
            var length = ReadUInt32(stream);
            if (length > MaxSectionBytes) {
                throw new InvalidDataException(
                    $"A section of {length} bytes is past what this format carries.");
            }

            if (tag != TypographyTag) {
                stream.Seek(length, SeekOrigin.Current);
                continue;
            }

            var payload = new byte[length];
            ReadExactly(stream, payload);
            return ReadTypography(payload);
        }

        return null;
    }

    /// <summary>
    /// Reads only how a file sets its paragraphs, without reading its
    /// letters: which lines stand centred or flush right, and where.
    /// </summary>
    /// <param name="stream">The stream to read from, at its start. Must be
    /// seekable, since a section it does not want is stepped over rather
    /// than read.</param>
    /// <returns>
    /// How each line is set, by its number in the text, or null when the
    /// file says nothing of the sort — a file that has never been aligned
    /// keeps no section at all, and every one of its lines is flush left.
    /// </returns>
    /// <remarks>
    /// The same header checks <see cref="ReadTypography"/> makes, so a wrong
    /// file fails here exactly as it would there, and the text section —
    /// usually the largest part of the file — is stepped over rather than
    /// decoded. Asking how a paper is set therefore costs a few bytes at
    /// its head rather than the whole of it, which is what makes it cheap
    /// to ask on the way in and again on the way out of a save.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    /// The file does not open with the mark, or was written at a version
    /// this app does not know, or carries a section past what the format
    /// allows.
    /// </exception>
    /// <exception cref="EndOfStreamException">The file ends before its own shape promised.</exception>
    public static IReadOnlyDictionary<int, ParagraphAlignment>? ReadAlignments(Stream stream) {
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

        for (var i = 0; i < sectionCount; i++) {
            var tag = ReadUInt32(stream);
            var length = ReadUInt32(stream);
            if (length > MaxSectionBytes) {
                throw new InvalidDataException(
                    $"A section of {length} bytes is past what this format carries.");
            }

            if (tag != AlignmentsTag) {
                stream.Seek(length, SeekOrigin.Current);
                continue;
            }

            var payload = new byte[length];
            ReadExactly(stream, payload);
            var alignments = ReadAlignments(payload);
            return alignments.Count == 0 ? null : alignments;
        }

        return null;
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

    private static DocumentTypography? ReadTypography(byte[] payload) {
        // A set section this reader cannot read stands for no set at all:
        // the paper falls back to whatever type the reader was already
        // standing on, rather than guessing at letters it cannot read. The
        // same rule the alignments follow — what is not understood is left
        // out, and the file still opens.
        if (payload.Length < 12) {
            return null;
        }

        var named = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0, 4));
        if (4L + named + 8 > payload.Length) {
            return null;
        }

        var family = Encoding.UTF8.GetString(payload, 4, (int)named).Trim();
        if (family.Length == 0) {
            return null;
        }

        var size = BinaryPrimitives.ReadDoubleLittleEndian(payload.AsSpan(4 + (int)named, 8));
        if (!double.IsFinite(size) || size < 1 || size > MaxPointSize) {
            return null;
        }

        return new DocumentTypography(family, size);
    }

    private static byte[]? WriteTypography(ShiranuxDocument document) {
        var set = document.Typography;
        if (set is null || string.IsNullOrWhiteSpace(set.FontFamily)) {
            return null;
        }

        // A set the reader would refuse is not written at all, so what lands
        // on disk is always what reads back — a size outside what a page can
        // stand would otherwise be a section carrying nothing but a name.
        if (!double.IsFinite(set.FontSize) || set.FontSize < 1 || set.FontSize > MaxPointSize) {
            return null;
        }

        var family = Encoding.UTF8.GetBytes(set.FontFamily.Trim());
        if (family.Length == 0) {
            return null;
        }

        var payload = new byte[4 + family.Length + 8];
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(0, 4), (uint)family.Length);
        family.CopyTo(payload.AsSpan(4));
        BinaryPrimitives.WriteDoubleLittleEndian(payload.AsSpan(4 + family.Length), set.FontSize);
        return payload;
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
