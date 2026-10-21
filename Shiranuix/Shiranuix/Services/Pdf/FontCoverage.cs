using System.Buffers.Binary;
using System.Text;

namespace Shiranuix.Services.Pdf;

/// <summary>
/// Reads a typeface file and says which letters it holds.
/// </summary>
/// <remarks>
/// <para>
/// A carry-any-write face is found by reading the file itself: the table a
/// TrueType file keeps — its <c>cmap</c> — is the same list a pen reads when
/// asked what a letter turns into, and a letter the list does not answer is
/// a square on paper. Two shapes of that table are read, the two that hold
/// everything a written page answers for: the small one that carries the
/// basic plane and the long one that carries the rest.
/// </para>
/// <para>
/// This is file reading and nothing more: the bytes go in, an answer comes
/// back. A file that is not a typeface, or is a typeface with no reading
/// table at all, carries nothing — the faces are asked in turn until one
/// does.
/// </para>
/// </remarks>
public static class FontCoverage {
    /// <summary>Says whether the file holds a mark for every codepoint asked.</summary>
    /// <param name="font">The typeface file's own bytes.</param>
    /// <param name="codepoints">The codepoints the document draws.</param>
    public static bool CoversAll(byte[] font, IEnumerable<int> codepoints) {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(codepoints);

        foreach (var codepoint in codepoints) {
            if (!Covers(font, codepoint)) {
                return false;
            }
        }

        return true;
    }

    /// <summary>Says whether the file holds a mark for the codepoint.</summary>
    public static bool Covers(byte[] font, int codepoint) {
        ArgumentNullException.ThrowIfNull(font);

        var table = CmapTable(font);

        if (table < 0) {
            return false;
        }

        var small = -1;
        var longRange = -1;

        foreach (var subtable in Subtables(font, table)) {
            var shape = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(subtable));

            if (shape == 4) {
                small = subtable;
            }

            if (shape == 12) {
                longRange = subtable;
            }
        }

        var glyph = longRange >= 0
            ? GlyphOfRanges(codepoint, font, longRange)
            : 0;

        return glyph > 0 ||
            (small >= 0 && GlyphOfRuns(codepoint, font, small) > 0);
    }

    /// <summary>The cell of bytes that holds a file's reading table, or minus one.</summary>
    private static int CmapTable(byte[] font) {
        if (font.Length < 16) {
            return -1;
        }

        var tables = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4));

        for (var table = 0; table < tables; ++table) {
            var record = 12 + table * 16;

            if (record + 16 > font.Length) {
                break;
            }

            if (Encoding.ASCII.GetString(font, record, 4) == "cmap") {
                return (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record + 8));
            }
        }

        return -1;
    }

    /// <summary>The cells of bytes that hold each shape of a reading table.</summary>
    private static IEnumerable<int> Subtables(byte[] font, int table) {
        if (table + 2 > font.Length) {
            yield break;
        }

        var count = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(table + 2));

        for (var subtable = 0; subtable < count; ++subtable) {
            var record = table + 4 + subtable * 8;

            if (record + 8 > font.Length) {
                yield break;
            }

            var offset = (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record + 4));

            if (offset >= 0 && offset < font.Length) {
                yield return table + offset;
            }
        }
    }

    /// <summary>
    /// The mark the long table holds for a codepoint: rows of whole ranges,
    /// the mark of a row's start stretched across it.
    /// </summary>
    private static int GlyphOfRanges(int codepoint, byte[] font, int subtable) {
        if (subtable + 16 > font.Length) {
            return 0;
        }

        var rows = BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(subtable + 12));

        for (var row = 0; row < rows; ++row) {
            var record = subtable + 16 + row * 12;

            if (record + 12 > font.Length) {
                break;
            }

            var start = BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record));
            var end = BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record + 4));

            if (codepoint < start || codepoint > end) {
                continue;
            }

            return (int)(BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(record + 8)) + (codepoint - start));
        }

        return 0;
    }

    /// <summary>
    /// The mark the small table holds for a codepoint: runs of ranges, each
    /// run answering directly by arithmetic or pointing away — the two ways
    /// a run keeps its answer.
    /// </summary>
    private static int GlyphOfRuns(int codepoint, byte[] font, int subtable) {
        if (subtable + 14 > font.Length) {
            return 0;
        }

        var runs = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(subtable + 6)) / 2;
        var endAt = subtable + 14;
        var startAt = endAt + 2 * runs + 2;
        var shiftAt = startAt + 2 * runs;
        var lookAt = shiftAt + 2 * runs;

        for (var run = 0; run < runs; ++run) {
            var start = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(startAt + run * 2));
            var end = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(endAt + run * 2));

            if (codepoint < start || codepoint > end) {
                continue;
            }

            var look = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(lookAt + run * 2));

            int raw;

            if (look == 0) {
                raw = codepoint;
            } else {
                var at = lookAt + run * 2 + look + 2 * (codepoint - start);

                if (at + 2 > font.Length) {
                    return 0;
                }

                raw = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(at));

                if (raw == 0) {
                    return 0;
                }
            }

            return (raw + BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(shiftAt + run * 2))) & 0xFFFF;
        }

        return 0;
    }
}
