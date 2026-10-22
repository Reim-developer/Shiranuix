using System.Buffers.Binary;
using System.Text;

namespace Shiranuix.Tests.Services;

/// <summary>
/// Hand-built typeface files: the smallest things a reading table can
/// honestly be, so the coverage and the choosing are tested against bytes
/// rather than a machine's faces.
/// </summary>
/// <remarks>
/// <para>
/// A built file carries the runs named. A letter inside a run is marked one
/// past itself, so a held letter is any at all and a plain zero is never a
/// held one by accident. A run of nothing at the end of the small sky —
/// one wide of a single letter — stands where the table keeps nothing, the
/// way real files do.
/// </para>
/// <para>
/// The layout the builds answer to is the one
/// <see cref="FontCoverage"/> reads: the small reading table's runs, ends
/// first, then the nothing between, then where each run begins, what each
/// shifts, and where each looks.
/// </para>
/// </remarks>
internal static class TestFont {
    /// <summary>A small-table file holding the runs named, each letter one past itself.</summary>
    internal static byte[] Small(params (int Start, int End)[] runs) {
        var ordered = runs.OrderBy(run => run.Start).ToList();
        ordered.Add((0xFFFF, 0xFFFF)); // the sentinel every small table ends with

        var count = ordered.Count;
        var font = Head(40 + 16 + count * 8);

        var table = 40;
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(table), 4);
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(table + 2), (ushort)(16 + count * 8));
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(table + 6), (ushort)(count * 2));

        var endAt = table + 14;
        var startAt = endAt + count * 2 + 2;
        var shiftAt = startAt + count * 2;
        var lookAt = shiftAt + count * 2;

        for (var run = 0; run < count; ++run) {
            BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(endAt + run * 2), (ushort)ordered[run].End);
            BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(startAt + run * 2), (ushort)ordered[run].Start);
            // Each held letter points one mark past itself.
            BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(shiftAt + run * 2), 1);
            BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(lookAt + run * 2), 0);
        }

        return font;
    }

    /// <summary>
    /// A small-table file whose one run reads its marks at a shelf laid
    /// after the table: the letter at the shelf's edge answers zero (the
    /// empty square), the rest answer what was laid there.
    /// </summary>
    internal static byte[] SmallLookingAway(int[] marks) {
        var count = 1; // the run named, plus the sentinel at the sky's end
        var font = Head(40 + 16 + (count + 1) * 8 + marks.Length * 2);

        var table = 40;
        var length = 16 + (count + 1) * 8;
        var glyphBase = table + length;

        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(table), 4);
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(table + 2), (ushort)length);
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(table + 6), (ushort)((count + 1) * 2));

        var endAt = table + 14;
        var startAt = endAt + (count + 1) * 2 + 2;
        var shiftAt = startAt + (count + 1) * 2;
        var lookAt = shiftAt + (count + 1) * 2;

        var a = (int)'a';

        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(endAt), (ushort)(a + count)); // one wide: a..a+count
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(endAt + 2), 0xFFFF);
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(startAt), (ushort)a);
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(startAt + 2), 0xFFFF);
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(shiftAt), 0);
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(shiftAt + 2), 0);
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(lookAt), (ushort)(glyphBase - lookAt));
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(lookAt + 2), 0);

        for (var mark = 0; mark < marks.Length; ++mark) {
            BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(glyphBase + mark * 2), (ushort)marks[mark]);
        }

        return font;
    }

    /// <summary>A long-table file holding the one row named, each letter one past itself.</summary>
    internal static byte[] Long((int Start, int End) range) {
        var font = Head(40 + 16 + 12, encoding: 10);

        var table = 40;
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(table), 12);
        BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(table + 4), 16 + 12); // length
        BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(table + 12), 1);      // one row
        BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(table + 16), (uint)range.Start);
        BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(table + 20), (uint)range.End);
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(table + 24), 1); // each letter one past itself

        return font;
    }

    /// <summary>A file of nothing — bytes with no reading table at all.</summary>
    internal static byte[] Empty() => new byte[20];

    /// <summary>
    /// A file whose name table says what the test asked it to say: the
    /// records in the order given, the Windows ones spelled the wide way
    /// and the Mac ones the plain way.
    /// </summary>
    internal static byte[] Named(params (ushort Platform, ushort Encoding, ushort NameId, string Text)[] records) =>
        Named(records, collection: false);

    /// <summary>A collection whose first face says what the test asked it to say.</summary>
    internal static byte[] NamedCollection(params (ushort Platform, ushort Encoding, ushort NameId, string Text)[] records) =>
        Named(records, collection: true);

    /// <summary>Builds one offset table over one name table, optionally wrapped in a collection.</summary>
    private static byte[] Named(
        (ushort Platform, ushort Encoding, ushort NameId, string Text)[] records,
        bool collection) {
        var spoken = records
            .Select(record => record.Platform == 1
                ? Encoding.Latin1.GetBytes(record.Text)
                : Encoding.BigEndianUnicode.GetBytes(record.Text))
            .ToArray();

        var entries = records.Length;
        var stringsAt = 6 + entries * 12;
        var length = stringsAt + spoken.Sum(said => said.Length);
        var at = collection ? 16 : 0;

        var font = new byte[at + 28 + length];

        // The offset table: one table, and it is the name.
        BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(at), 0x0001_0000); // a plain TrueType file
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(at + 4), 1);       // one table
        Encoding.ASCII.GetBytes("name").CopyTo(font, at + 12);
        BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(at + 20), (uint)(at + 28));
        BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(at + 24), (uint)length);

        // The name table: its opening, then each record, then the strings
        // each record points at.
        var name = at + 28;
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(name + 2), (ushort)entries);
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(name + 4), (ushort)stringsAt);

        var text = name + stringsAt;
        var offset = 0;

        for (var entry = 0; entry < entries; ++entry) {
            var (platform, encoding, nameId, said) = records[entry];
            var record = name + 6 + entry * 12;

            BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(record), platform);
            BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(record + 2), encoding);
            BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(record + 4), platform == 3 ? (ushort)0x0409 : (ushort)0);
            BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(record + 6), nameId);
            BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(record + 8), (ushort)spoken[entry].Length);
            BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(record + 10), (ushort)offset);

            spoken[entry].CopyTo(font, text + offset);
            offset += spoken[entry].Length;
        }

        if (!collection) {
            return font;
        }

        // The collection's own opening: its tag, its version, the one face
        // it holds, and where that face starts.
        Encoding.ASCII.GetBytes("ttcf").CopyTo(font, 0);
        BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(4), 0x0001_0000);
        BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(12), (uint)at);

        return font;
    }

    /// <summary>
    /// The part every build shares: the file's opening, and its one table of
    /// contents saying a reading table sits twelve short of the answer at
    /// byte forty.
    /// </summary>
    private static byte[] Head(int length, ushort encoding = 1) {
        var font = new byte[length];

        font[0] = 0x00;
        font[1] = 0x01; // a plain TrueType file, version 1.0

        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(4), 1); // one table
        Encoding.ASCII.GetBytes("cmap").CopyTo(font, 12);
        BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(20), 28); // the table sits here

        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(28), 0); // table version
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(30), 1); // one reading shape
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(32), 3); // Windows
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(34), encoding); // the sky the table reads
        BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(36), 12); // twelve past the header

        return font;
    }
}
