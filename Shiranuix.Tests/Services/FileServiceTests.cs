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

        Assert.Equal("HELLO world", _files.ReadFrom(path, new LineAnchor(0, 0), scan.Characters));
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
        Assert.Equal("\uFEFFhello", _files.ReadFrom(path, new LineAnchor(0, 0), scan.Characters));
    }

    // ===== ReadCombined ======================================================

    [Fact]
    public void A_Whole_File_Streams_Back_When_Nothing_Moved() {
        var path = Write("story.txt", "hello world", withBom: false);

        Assert.Equal("hello world", _files.ReadCombined(path, []));
    }

    [Fact]
    public void The_Whole_File_Is_The_Moves_Laid_Over_It_As_They_Fall() {
        var path = Write("story.txt", "hello world", withBom: false);

        Assert.Equal("hello brave world", _files.ReadCombined(path, [new TextReplacement(6, 0, "brave ")]));
    }

    [Fact]
    public void A_Whole_File_Keeps_The_Letters_Its_Window_Never_Showed() {
        // Two hundred lines; a window holds fifty. Every line the screen has
        // never carried is still the document's own writing.
        var lines = string.Join("\r\n", Enumerable.Range(1, 200).Select(index => $"line {index}"));
        var path = Write("long.txt", lines, withBom: false);

        Assert.Equal(lines, _files.ReadCombined(path, []));
    }

    [Fact]
    public void A_Document_With_No_File_Behind_It_Is_Its_Moves_Alone() {
        Assert.Equal(
            "line oneline two",
            _files.ReadCombined(
                null, [new TextReplacement(0, 0, "line one"), new TextReplacement(0, 0, "line two")]));
    }

    [Fact]
    public void The_Moves_Must_Fall_In_The_Order_They_Fall() {
        var path = Write("story.txt", "one\ntwo", withBom: false);

        Assert.Throws<InvalidDataException>(() =>
            _files.ReadCombined(path, [new TextReplacement(6, 0, "x"), new TextReplacement(3, 0, "y")]));
    }

    // ===== ReadFrom ==========================================================

    [Fact]
    public void An_Anchor_Reads_The_Characters_It_Names() {
        var path = Write("alphabet.txt", "abcdefghij", withBom: false);

        Assert.Equal("defg", _files.ReadFrom(path, new LineAnchor(3, 3), 4));
        Assert.Equal("fghij", _files.ReadFrom(path, new LineAnchor(5, 5), 100));
        Assert.Equal(string.Empty, _files.ReadFrom(path, new LineAnchor(10, 10), 5));
    }

    [Fact]
    public void An_Anchor_In_A_Multibyte_File_Seeks_Bytes_And_Reads_Characters() {
        // The whole point of the pair: bytes seek, characters count. A file
        // where they disagree is the only place a walk can go quietly wrong.
        var path = Write("accented.txt", "café\nnaïve\n!", withBom: false);

        Assert.Equal("é", _files.ReadFrom(path, new LineAnchor(3, 3), 1));
        Assert.Equal("ïve\n!", _files.ReadFrom(path, new LineAnchor(8, 7), 5));
    }

    // ===== ReadLines =========================================================

    [Fact]
    public void Lines_Are_The_Text_Without_Their_Last_Newline() {
        // The window ends before the newline that terminates it — a line the
        // box cannot show would draw a line the gutter does not count.
        var path = Write("story.txt", "one\ntwo\nthree\nfour", withBom: false);

        var read = _files.ReadLines(path, new LineAnchor(0, 0), 3);

        Assert.Equal("one\ntwo\nthree", read.Text);
        Assert.Equal(new LineAnchor(14, 14), read.End.At);
        Assert.Equal(3, read.End.Lines);
        Assert.True(read.More);
    }

    [Fact]
    public void The_Last_Window_Ends_At_The_File_And_Says_No_More() {
        // Even a file that ends on a newline: the window holds the last line,
        // not the empty one after its terminator.
        var path = Write("story.txt", "one\ntwo\n", withBom: false);

        var read = _files.ReadLines(path, new LineAnchor(0, 0), 50);

        Assert.Equal("one\ntwo", read.Text);
        Assert.Equal(2, read.End.Lines);
        Assert.False(read.More);

        var past = _files.ReadLines(path, read.End.At, 50);
        Assert.Equal(string.Empty, past.Text);
        Assert.False(past.More);
    }

    [Fact]
    public void A_Window_Of_Lines_Carries_Its_Byte_And_Character_Positions_Together() {
        // "café\nnaïve\n!": two lines are 11 characters but 13 bytes, and
        // every walk after them depends on both numbers having moved alike.
        var path = Write("accented.txt", "café\nnaïve\n!", withBom: false);

        var read = _files.ReadLines(path, new LineAnchor(0, 0), 2);

        Assert.Equal("café\nnaïve", read.Text);
        Assert.Equal(new LineAnchor(13, 11), read.End.At);
        Assert.True(read.More);
    }

    // ===== MoveLines =========================================================

    [Fact]
    public void A_Step_Forward_Lands_On_A_Line_S_Start() {
        var path = Write("story.txt", "one\ntwo\nthree\nfour", withBom: false);

        var step = _files.MoveLines(path, new LineAnchor(0, 0), 2);

        Assert.Equal(new LineAnchor(8, 8), step.At);
        Assert.Equal(2, step.Lines);
    }

    [Fact]
    public void A_Step_Forward_Multibyte_Lands_Bytes_And_Characters_Alike() {
        var path = Write("accented.txt", "café\nnaïve\n!", withBom: false);

        var step = _files.MoveLines(path, new LineAnchor(0, 0), 2);

        Assert.Equal(new LineAnchor(13, 11), step.At);
        Assert.Equal(2, step.Lines);
    }

    [Fact]
    public void A_Step_That_Runs_Off_The_End_Lands_On_Its_End() {
        var path = Write("story.txt", "one\ntwo", withBom: false);

        var step = _files.MoveLines(path, new LineAnchor(0, 0), 50);

        Assert.Equal(new LineAnchor(7, 7), step.At);
        Assert.Equal(1, step.Lines);
    }

    [Fact]
    public void A_Step_Back_Lands_On_The_Line_Before() {
        var path = Write("story.txt", "one\ntwo\nthree\nfour", withBom: false);
        var end = new LineAnchor(14, 14);

        var step = _files.MoveLines(path, end, -2);

        Assert.Equal(new LineAnchor(4, 4), step.At);
        Assert.Equal(-2, step.Lines);
    }

    [Fact]
    public void A_Step_Back_Multibyte_Lands_Bytes_And_Characters_Alike() {
        var path = Write("accented.txt", "café\nnaïve\n!", withBom: false);

        var step = _files.MoveLines(path, new LineAnchor(13, 11), -1);

        Assert.Equal(new LineAnchor(6, 5), step.At);
        Assert.Equal(-1, step.Lines);
    }

    [Fact]
    public void A_Step_Back_That_Runs_Off_The_Start_Lands_On_Zero() {
        var path = Write("story.txt", "one\ntwo\nthree", withBom: false);

        var step = _files.MoveLines(path, new LineAnchor(8, 8), -50);

        Assert.Equal(new LineAnchor(0, 0), step.At);
        Assert.Equal(-2, step.Lines);
    }

    [Fact]
    public void A_Step_Of_No_Lines_Stays_Where_It_Stand() {
        var path = Write("story.txt", "one\ntwo", withBom: false);
        var middle = new LineAnchor(4, 4);

        Assert.Equal(new LineMove(middle, 0), _files.MoveLines(path, middle, 0));
    }

    // ===== LineStartOf =======================================================

    [Fact]
    public void A_Line_Below_Is_Found_By_Walking_Back() {
        var path = Write("story.txt", "one\ntwo\nthree\nfour", withBom: false);

        // The character sits on the third line; the walk starts from the
        // fourth and comes back to its beginning.
        var found = _files.LineStartOf(path, new LineAnchor(14, 14), 10);

        Assert.Equal(new LineAnchor(8, 8), found.At);
        Assert.Equal(-1, found.Lines);
    }

    [Fact]
    public void A_Line_Above_Is_Found_By_Walking_Forward() {
        var path = Write("story.txt", "one\ntwo\nthree\nfour", withBom: false);

        var found = _files.LineStartOf(path, new LineAnchor(0, 0), 10);

        Assert.Equal(new LineAnchor(8, 8), found.At);
        Assert.Equal(2, found.Lines);
    }

    [Fact]
    public void A_Character_At_A_Line_S_Start_Finds_That_Line() {
        var path = Write("story.txt", "one\ntwo\nthree", withBom: false);

        Assert.Equal(
            new LineMove(new LineAnchor(4, 4), 0),
            _files.LineStartOf(path, new LineAnchor(4, 4), 4));
        Assert.Equal(
            new LineMove(new LineAnchor(4, 4), -1),
            _files.LineStartOf(path, new LineAnchor(8, 8), 4));
    }

    [Fact]
    public void A_Character_On_The_First_Line_Finds_The_File_S_Beginning() {
        var path = Write("story.txt", "one\ntwo\nthree", withBom: false);

        var found = _files.LineStartOf(path, new LineAnchor(8, 8), 1);

        Assert.Equal(new LineAnchor(0, 0), found.At);
        Assert.Equal(-2, found.Lines);
    }

    [Fact]
    public void A_Walk_Across_A_Chunk_Boundary_Decodes_Only_Whole_Characters() {
        // 800 lines of "aéé\n" — 6 bytes, 4 characters each — so the backward
        // walk's cut at byte 6N−4096 falls on the second byte of an é: the
        // chunk must step back to that character's first byte or decode a
        // phantom into the counts.
        var path = Write("accented.txt", string.Concat(Enumerable.Repeat("aéé\n", 800)), withBom: false);

        // Character 3190 sits on line 598, which begins at character 3188 —
        // byte 4782.
        var found = _files.LineStartOf(path, new LineAnchor(4800, 3200), 3190);

        Assert.Equal(new LineAnchor(4782, 3188), found.At);
        Assert.Equal(-3, found.Lines);
    }

    // ===== WriteCombined =====================================================

    [Fact]
    public void A_Save_Reports_What_A_Scan_Would_Say_Of_What_It_Wrote() {
        // The document's baseline after a save comes from this result — if
        // it disagreed with a scan of the same bytes, every count would
        // jump by the difference the moment the file was written.
        var path = Write("story.txt", "one\ntwo three", withBom: false);

        var result = _files.WriteCombined(
            path, path, [new TextReplacement(4, 0, "RED ")], Newline.CrLf);

        Assert.Equal("one\r\nRED two three", File.ReadAllText(path));
        Assert.Equal(_files.Scan(path), result);
    }

    [Fact]
    public void The_Report_Of_An_Empty_Save_Is_An_Empty_File() {
        var target = Path.Combine(_directory, "draft.txt");

        var result = _files.WriteCombined(target, null, [], Newline.Lf);

        Assert.Equal(string.Empty, File.ReadAllText(target));
        Assert.Equal(new ScanResult(0, 0, 0, Newline.Lf), result);
    }

    [Fact]
    public void The_Report_Of_A_Marked_File_Means_The_Same_As_Its_Scan() {
        // A mark counts as a character on both sides, so the two stay equal
        // however the file arrived.
        var path = Write("marked.txt", "hello world", withBom: true);

        var result = _files.WriteCombined(path, path, [], Newline.Lf);

        Assert.Equal(_files.Scan(path), result);
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
