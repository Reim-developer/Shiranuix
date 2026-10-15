using System.Text;
using Shiranuix.Models;
using Shiranuix.Services;
using Shiranuix.Utilities;

namespace Shiranuix.Tests.Services;

/// <summary>
/// Tests for <see cref="FileService"/>, against real files in a temporary
/// directory.
/// </summary>
/// <remarks>
/// The fake assembles strings in memory, which cannot notice that the real
/// writer drops a chunk, converts a break twice, or leaves its temporary file
/// behind — so the stream itself is what is worth checking here. Each test
/// gets its own directory, so nothing is shared between them.
/// </remarks>
public sealed class FileServiceTests : IDisposable {
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "ShiranuixTests", Guid.NewGuid().ToString("N"));

    private readonly FileService _files = new();

    public FileServiceTests() {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose() {
        if (Directory.Exists(_directory)) {
            Directory.Delete(_directory, recursive: true);
        }
    }

    // ===== Scan ==============================================================

    [Fact]
    public void A_Scan_Says_What_The_File_s_Text_Would_Say() {
        // The counts on the status bar come off the stream, and they must
        // mean what TextStatistics means over the same bytes — otherwise a
        // document that later shows only part of itself would show totals
        // that never matched the file.
        var samples = new[] {
            string.Empty,
            "hello world\nsecond line",
            "one\r\ntwo\r\nthree",
            "  spaced   words \t here\n\nlast line",
            "a\rb",
            "no breaks at all",
        };

        foreach (var sample in samples) {
            var path = Write("sample.txt", sample, withBom: false);

            var scan = _files.Scan(path);
            var measure = TextStatistics.Measure(sample);

            Assert.Equal(measure.Words, scan.Words);
            Assert.Equal(measure.Characters, scan.Characters);
            Assert.Equal(measure.Lines, scan.Lines);
            Assert.Equal(Newline.Detect(sample), scan.Newline);
        }
    }

    [Fact]
    public void A_Carriage_Return_With_No_Line_Feed_Is_Not_The_Windows_Convention() {
        // Newline.Detect asks of the whole text whether a pair exists at all;
        // the stream must give the same answer while never seeing the whole.
        var lone = Write("lone.txt", "one\rtwo\rthree", withBom: false);
        var pair = Write("pair.txt", "one\r\ntwo", withBom: false);

        Assert.Equal(Newline.Lf, _files.Scan(lone).Newline);
        Assert.Equal(Newline.CrLf, _files.Scan(pair).Newline);
    }

    [Fact]
    public void Reading_A_File_That_Is_Not_There_Fails_Loudly() {
        var missing = Path.Combine(_directory, "missing.txt");

        Assert.Throws<FileNotFoundException>(() => _files.Scan(missing));
    }

    // ===== ReadRange =========================================================

    [Fact]
    public void A_Range_Is_A_Slice_Of_The_File() {
        var path = Write("alphabet.txt", "abcdefghij", withBom: false);

        Assert.Equal("defg", _files.ReadRange(path, 3, 7));
        Assert.Equal("abcdefghij", _files.ReadRange(path, 0, 10));
    }

    [Fact]
    public void A_Range_Past_The_End_Is_Only_As_Long_As_What_Is_There() {
        var path = Write("alphabet.txt", "abcdefghij", withBom: false);

        Assert.Equal("fghij", _files.ReadRange(path, 5, 100));
        Assert.Equal(string.Empty, _files.ReadRange(path, 50, 60));
    }

    [Fact]
    public void A_Range_That_Runs_Backwards_Is_Refused() {
        var path = Write("alphabet.txt", "abcdefghij", withBom: false);

        Assert.Throws<ArgumentOutOfRangeException>(() => _files.ReadRange(path, 7, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => _files.ReadRange(path, -1, 3));
    }

    // ===== WriteCombined =====================================================

    [Fact]
    public void A_Save_Lays_The_Moves_Over_The_File() {
        var path = Write("story.txt", "hello world", withBom: false);

        _files.WriteCombined(path, path, [new TextReplacement(6, 0, "brave ")], Newline.Lf);

        Assert.Equal("hello brave world", File.ReadAllText(path));
    }

    [Fact]
    public void A_Save_Over_A_Different_File_Leaves_The_Source_Standing() {
        var source = Write("story.txt", "hello world", withBom: false);
        var target = Path.Combine(_directory, "copy.txt");

        _files.WriteCombined(target, source, [new TextReplacement(0, 5, "HELLO")], Newline.Lf);

        Assert.Equal("HELLO world", File.ReadAllText(target));
        Assert.Equal("hello world", File.ReadAllText(source));
    }

    [Fact]
    public void A_Save_With_No_File_Behind_It_Writes_Only_The_Moves() {
        var target = Path.Combine(_directory, "draft.txt");

        _files.WriteCombined(
            target, null, [new TextReplacement(0, 0, "line one"), new TextReplacement(0, 0, "line two")],
            Newline.Lf);

        Assert.Equal("line oneline two", File.ReadAllText(target));
    }

    [Fact]
    public void A_Read_After_A_Write_Round_Trips() {
        var path = Write("story.txt", "hello world", withBom: false);

        _files.WriteCombined(path, path, [new TextReplacement(0, 5, "HELLO")], Newline.Lf);

        var scan = _files.Scan(path);

        Assert.Equal("HELLO world", _files.ReadRange(path, 0, scan.Characters));
        Assert.Equal(TextStatistics.Measure("HELLO world").Characters, scan.Characters);
    }

    [Fact]
    public void Every_Break_Is_Written_In_The_Chosen_Convention() {
        // A file that mixed both conventions is normalised in the stream the
        // way Newline.Apply normalises a string — including the lone carriage
        // return, which is a break of its own rather than half a pair.
        var path = Write("mixed.txt", "a\nb\rc\r\nd", withBom: false);

        _files.WriteCombined(path, path, [], Newline.CrLf);
        Assert.Equal("a\r\nb\r\nc\r\nd", File.ReadAllText(path));

        _files.WriteCombined(path, path, [], Newline.Lf);
        Assert.Equal("a\nb\nc\nd", File.ReadAllText(path));
    }

    [Fact]
    public void A_Break_Split_Across_A_Move_Seam_Is_Still_One_Break() {
        // The move lands between the carriage return and its line feed — the
        // seam where a writer that forgot the hold would write the pair
        // twice, or lose it entirely.
        var path = Path.Combine(_directory, "pair.txt");

        WriteFresh(path, "a\r\nb");
        _files.WriteCombined(path, path, [new TextReplacement(2, 0, "X")], Newline.Lf);
        Assert.Equal("a\nX\nb", File.ReadAllText(path));

        WriteFresh(path, "a\r\nb");
        _files.WriteCombined(path, path, [new TextReplacement(2, 0, "X")], Newline.CrLf);
        Assert.Equal("a\r\nX\r\nb", File.ReadAllText(path));
    }

    [Fact]
    public void A_Move_That_Carries_Breaks_Has_Them_Converted_Too() {
        // The move's own text goes through the same conversion as the file's
        // copy: what normalising the assembled text in one string would have
        // written is what lands on disk.
        var path = Write("story.txt", "line one\nline two", withBom: false);

        _files.WriteCombined(path, path, [new TextReplacement(9, 0, "A\nB\n")], Newline.CrLf);

        Assert.Equal("line one\r\nA\r\nB\r\nline two", File.ReadAllText(path));
    }

    [Fact]
    public void A_Failed_Save_Leaves_The_Old_File_And_No_Files_Apart_From_It() {
        // Out-of-order moves are refused before anything is assembled: the
        // file that was there must stand, and the temporary must not survive
        // the failure to hide beside it.
        var path = Write("story.txt", "one\ntwo", withBom: false);

        Assert.Throws<InvalidDataException>(() =>
            _files.WriteCombined(
                path, path, [new TextReplacement(6, 0, "x"), new TextReplacement(3, 0, "y")], Newline.Lf));

        Assert.Equal("one\ntwo", File.ReadAllText(path));
        Assert.Equal(
            new[] { "story.txt" },
            Directory.GetFiles(_directory).Select(name => Path.GetFileName(name)).ToArray());
    }

    [Fact]
    public void A_Save_Leaves_Nothing_Beside_The_Files_It_Wrote() {
        var source = Write("story.txt", "hello", withBom: false);
        var target = Path.Combine(_directory, "copy.txt");

        _files.WriteCombined(target, source, [new TextReplacement(0, 1, "H")], Newline.Lf);

        Assert.Equal(
            new[] { "copy.txt", "story.txt" },
            Directory.GetFiles(_directory)
                .Select(name => Path.GetFileName(name))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public void A_Byte_Order_Mark_Is_Read_As_A_Character_And_Written_Back() {
        // A file that arrived with a mark keeps it: a save that did not
        // touch those bytes must not quietly drop half a character of them.
        // The mark written by the file's own preamble is the character the
        // reader sees, one U+FEFF ahead of the text.
        var path = Write("marked.txt", "hello", withBom: true);

        var scan = _files.Scan(path);
        Assert.Equal(6, scan.Characters);

        _files.WriteCombined(path, path, [], Newline.Lf);

        var bytes = File.ReadAllBytes(path);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.Equal("\uFEFFhello", _files.ReadRange(path, 0, scan.Characters));
    }

    // ===== Helpers ===========================================================

    /// <summary>
    /// Writes a file into the test's directory.
    /// </summary>
    /// <param name="name">The file's name.</param>
    /// <param name="text">The text to write.</param>
    /// <param name="withBom">Whether the file starts with a byte-order mark.</param>
    /// <returns>The file's full path.</returns>
    private string Write(string name, string text, bool withBom) {
        var path = Path.Combine(_directory, name);
        WriteFresh(path, text, withBom);
        return path;
    }

    /// <summary>
    /// Writes a file at a path that is already decided, without a mark.
    /// </summary>
    /// <param name="path">The file's full path.</param>
    /// <param name="text">The text to write.</param>
    private static void WriteFresh(string path, string text) =>
        WriteFresh(path, text, withBom: false);

    /// <summary>
    /// Writes a file at a path that is already decided.
    /// </summary>
    /// <param name="path">The file's full path.</param>
    /// <param name="text">The text to write.</param>
    /// <param name="withBom">Whether the file starts with a byte-order mark.</param>
    private static void WriteFresh(string path, string text, bool withBom) =>
        File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: withBom));
}
