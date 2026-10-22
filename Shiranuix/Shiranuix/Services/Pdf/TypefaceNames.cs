using System.Buffers.Binary;
using System.Text;

namespace Shiranuix.Services.Pdf;

/// <summary>
/// Reads a typeface file's own name table and says what the file calls
/// itself: the family it belongs to, and the style it is cut in.
/// </summary>
/// <remarks>
/// <para>
/// The folders the other systems keep hold files with no registry to
/// introduce them, and the names beside them say only so much — the truth
/// a shelf needs is the one every TrueType and OpenType file keeps in its
/// own name table, the table the Windows registry's titles were written
/// from. The reading is the kind the coverage does: the stream's bytes
/// asked, an answer coming back, every step held inside the file's own
/// length, and nothing at all when nothing readable is there.
/// </para>
/// <para>
/// A collection file answers for the first face it holds. A file with no
/// readable name answers nothing, and nothing is also what the shelf says
/// of such a file: a file that cannot name itself cannot be found by a
/// name.
/// </para>
/// </remarks>
internal static class TypefaceNames {
    /// <summary>What a file calls itself.</summary>
    /// <param name="Family">The family, as the file spells it.</param>
    /// <param name="Subfamily">The style it is cut in — Regular when the file says nothing of one.</param>
    internal readonly record struct Names(string Family, string Subfamily);

    /// <summary>Reads the file's name, or nothing when it holds no readable one.</summary>
    /// <param name="file">
    /// A stream over the whole typeface file — seekable; it is moved about
    /// and left open for whoever handed it over.
    /// </param>
    internal static Names? Read(Stream file) {
        ArgumentNullException.ThrowIfNull(file);

        var head = Take(file, 0, 12);

        if (head is null) {
            return null;
        }

        var face = FaceAt(file, head);

        if (face < 0) {
            return null;
        }

        var header = face == 0 ? head : Take(file, face, 12);

        if (header is null) {
            return null;
        }

        var tables = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4));
        var directory = Take(file, face + 12L, tables * 16);

        if (directory is null) {
            return null;
        }

        var name = NameTableIn(directory);

        if (name is not { Length: > 0 } table) {
            return null;
        }

        var said = Take(file, table.Offset, (int)table.Length);

        return said is null ? null : Say(said);
    }

    /// <summary>
    /// Where the face being read begins: zero for a plain typeface file,
    /// and the first face's own start inside a collection.
    /// </summary>
    private static int FaceAt(Stream file, byte[] head) {
        var marker = BinaryPrimitives.ReadUInt32BigEndian(head);

        // A plain TrueType file, an OpenType one carrying its outlines
        // otherwise, and a file calling itself what it is.
        if (marker is 0x0001_0000 or 0x4F54_544F or 0x7472_7565) {
            return 0;
        }

        // A collection: its tag, its version, how many faces it holds, and
        // where the first of them starts.
        if (marker != 0x7474_6366) {
            return -1;
        }

        var held = Take(file, 8, 8);

        if (held is null || BinaryPrimitives.ReadUInt32BigEndian(held) == 0) {
            return -1;
        }

        var first = BinaryPrimitives.ReadUInt32BigEndian(held.AsSpan(4));

        return first <= int.MaxValue ? (int)first : -1;
    }

    /// <summary>Where the name table sits in the file's table of contents.</summary>
    private static (long Offset, long Length)? NameTableIn(byte[] directory) {
        for (var record = 0; record + 16 <= directory.Length; record += 16) {
            if (!directory.AsSpan(record, 4).SequenceEqual("name"u8)) {
                continue;
            }

            var offset = BinaryPrimitives.ReadUInt32BigEndian(directory.AsSpan(record + 8));
            var length = BinaryPrimitives.ReadUInt32BigEndian(directory.AsSpan(record + 12));

            return offset <= int.MaxValue && length <= int.MaxValue
                ? (offset, length)
                : null;
        }

        return null;
    }

    /// <summary>
    /// What the name table says: the family and the style, out of the
    /// records that speak the languages a shelf can hear — the Windows
    /// spelling first, then the Unicode one, then the Mac shelf's plain
    /// text — and the regular style when nothing says one.
    /// </summary>
    private static Names? Say(byte[] table) {
        if (table.Length < 6) {
            return null;
        }

        var entries = BinaryPrimitives.ReadUInt16BigEndian(table.AsSpan(2));
        var strings = BinaryPrimitives.ReadUInt16BigEndian(table.AsSpan(4));

        (int Rank, string Text)? family = null;
        (int Rank, string Text)? style = null;

        for (var entry = 0; entry < entries; ++entry) {
            var record = 6 + entry * 12;

            if (record + 12 > table.Length) {
                break;
            }

            var nameId = BinaryPrimitives.ReadUInt16BigEndian(table.AsSpan(record + 6));

            if (nameId is not (1 or 2)) {
                continue;
            }

            var platform = BinaryPrimitives.ReadUInt16BigEndian(table.AsSpan(record));
            var rank = RankOf(platform, BinaryPrimitives.ReadUInt16BigEndian(table.AsSpan(record + 2)));

            if (rank < 0 || (nameId == 1 ? family : style) is { Rank: var held } && held <= rank) {
                continue;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(table.AsSpan(record + 8));
            var offset = BinaryPrimitives.ReadUInt16BigEndian(table.AsSpan(record + 10));
            var from = strings + offset;

            if (from + length > table.Length) {
                continue;
            }

            var text = Speak(table, from, platform == 1 ? length : length & ~1, platform);

            if (string.IsNullOrEmpty(text)) {
                continue;
            }

            var answer = (rank, text);

            if (nameId == 1) {
                family = answer;
            } else {
                style = answer;
            }
        }

        return family is { } named
            ? new Names(named.Text, style?.Text ?? "Regular")
            : null;
    }

    /// <summary>How well a record speaks: the Windows spelling, then Unicode, then the Mac shelf's.</summary>
    private static int RankOf(ushort platform, ushort encoding) =>
        platform == 3 && encoding == 1 ? 0 :
        platform == 0 ? 1 :
        platform == 1 && encoding == 0 ? 2 :
        -1;

    /// <summary>The record's own text, read in the way its platform writes one.</summary>
    private static string Speak(byte[] table, int from, int length, ushort platform) =>
        (platform == 1
            ? Encoding.Latin1.GetString(table, from, length)
            : Encoding.BigEndianUnicode.GetString(table, from, length))
        .Trim(' ', '\t', '\r', '\n', '\0');

    /// <summary>Count bytes at a place in the stream, or nothing when they are not all there.</summary>
    private static byte[]? Take(Stream file, long from, int count) {
        if (from < 0 || count <= 0) {
            return null;
        }

        try {
            file.Seek(from, SeekOrigin.Begin);

            var taken = new byte[count];
            var at = 0;

            while (at < count) {
                var read = file.Read(taken, at, count - at);

                if (read == 0) {
                    return null;
                }

                at += read;
            }

            return taken;
        } catch (Exception flaw) when (flaw is IOException or NotSupportedException) {
            // A stream that cannot be read is a file with nothing to say.
            return null;
        }
    }
}
