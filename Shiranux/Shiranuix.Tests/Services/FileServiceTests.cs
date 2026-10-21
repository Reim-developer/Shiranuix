using System.Text;
using Shiranuix.CoreFormat;
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

    // ===== .shiranux =========================================================

    [Fact]
    public void A_Shiranux_File_Reads_Back_As_The_Letters_It_Carries() {
        // The letters sit between a header and the alignments that follow:
        // a scan must count the letters and stop where they stop, never
        // reaching into the binary that wraps them.
        var text = "Chương Một\nĐêm đó trời mưa.\nCuối cùng";
        var path = WriteShiranux("story.shiranux", text,
            new Dictionary<int, ParagraphAlignment> { [1] = ParagraphAlignment.Center });

        var scan = _files.Scan(path);
        var measure = TextStatistics.Measure(text);

        Assert.Equal(measure.Words, scan.Words);
        Assert.Equal(measure.Characters, scan.Characters);
        Assert.Equal(measure.Lines, scan.Lines);
        Assert.Equal(Newline.Lf, scan.Newline);
        Assert.Equal(text, _files.ReadFrom(path, new LineAnchor(0, 0), measure.Characters));
    }

    [Fact]
    public void A_Window_Over_A_Shiranux_File_Shows_What_Its_Txt_Twin_Shows() {
        var text = "Chương Một\nĐêm đó trời mưa.\nCuối cùng";
        var own = WriteShiranux("story.shiranux", text);
        var plain = Write("story.txt", text, withBom: false);

        var fromOwn = _files.ReadLines(own, new LineAnchor(0, 0), 2);
        var fromPlain = _files.ReadLines(plain, new LineAnchor(0, 0), 2);

        Assert.Equal(fromPlain.Text, fromOwn.Text);
        Assert.Equal(fromPlain.End.At, fromOwn.End.At);
        Assert.Equal(fromPlain.More, fromOwn.More);
    }

    [Fact]
    public void Every_Walk_A_Shiranux_File_Takes_Lands_Where_Its_Txt_Twin_Lands() {
        // Anchors count bytes from the writing's first letter, so every walk
        // — forward, backward, and off the end — measures the same bytes
        // over the app's own file as over the plain text beside it.
        var lines = Enumerable.Range(0, 40)
            .Select(index => $"Dòng {index}: Tiếng Việt có dấu ữ ế ơ ư")
            .ToArray();
        var text = string.Join('\n', lines);
        var own = WriteShiranux("story.shiranux", text);
        var plain = Write("story.txt", text, withBom: false);

        // A valid anchor taken from the file itself: line ten's beginning.
        var at = _files.ReadLines(plain, new LineAnchor(0, 0), 10).End.At;

        Assert.Equal(_files.MoveLines(plain, at, 3), _files.MoveLines(own, at, 3));
        Assert.Equal(_files.MoveLines(plain, at, -3), _files.MoveLines(own, at, -3));
        Assert.Equal(_files.LineStartOf(plain, at, 700), _files.LineStartOf(own, at, 700));
        Assert.Equal(_files.LineStartOf(plain, at, 100), _files.LineStartOf(own, at, 100));

        // Running off the end lands on the writing's own end — the byte
        // after its last letter, not the byte after the alignments.
        var end = new LineAnchor(TextFile.Encoding.GetByteCount(text), text.Length);
        Assert.Equal(end, _files.MoveLines(plain, at, 10_000).At);
        Assert.Equal(end, _files.MoveLines(own, at, 10_000).At);
    }

    [Fact]
    public void A_Window_That_Runs_Off_A_Shiranux_File_Ends_There_And_Says_No_More() {
        // The StreamReader must find nothing past the text: what follows it
        // in the file is another section's bytes, and a reader that trusted
        // them would report more lines than the writing has.
        var text = "one\ntwo\nthree";
        var path = WriteShiranux("story.shiranux", text,
            new Dictionary<int, ParagraphAlignment> { [2] = ParagraphAlignment.Right });

        var read = _files.ReadLines(path, new LineAnchor(0, 0), 50);

        Assert.Equal("one\ntwo\nthree", read.Text);
        Assert.False(read.More);
        Assert.Equal(new LineAnchor(13, 13), read.End.At);
    }

    [Fact]
    public void A_Shiranux_Save_Writes_The_Letters_And_Reports_Like_A_Scan() {
        var path = WriteShiranux("story.shiranux", "hello world");

        var result = _files.WriteCombined(path, path, [new TextReplacement(6, 0, "brave ")], Newline.Lf);

        Assert.Equal("hello brave world", ReadShiranux(path).Text);
        Assert.Equal(_files.Scan(path), result);
    }

    [Fact]
    public void A_Shiranux_Save_Carries_A_Face_To_The_Paragraph_Its_Letters_Now_Open() {
        // Two lines go on over the top; the heading's letters slide down
        // two paragraphs, and the centring goes with them rather than
        // staying on a number the heading no longer opens.
        var path = WriteShiranux("story.shiranux", "mở đầu\ntiêu đề",
            new Dictionary<int, ParagraphAlignment> { [1] = ParagraphAlignment.Center });

        _files.WriteCombined(
            path, path, [new TextReplacement(0, 0, "dòng một\ndòng hai\n")], Newline.Lf);

        var saved = ReadShiranux(path);

        Assert.Equal("dòng một\ndòng hai\nmở đầu\ntiêu đề", saved.Text);
        Assert.Equal(ParagraphAlignment.Left, saved.AlignmentOf(0));
        Assert.Equal(ParagraphAlignment.Left, saved.AlignmentOf(1));
        Assert.Equal(ParagraphAlignment.Left, saved.AlignmentOf(2));
        Assert.Equal(ParagraphAlignment.Center, saved.AlignmentOf(3));
    }

    [Fact]
    public void A_Shiranux_Save_Drops_The_Face_Of_A_Paragraph_Deleted_Whole() {
        // The heading and the break ending it were taken out together. What
        // slides into its number is the paragraph after, which never wore
        // the face — so no face is left pointing at it.
        var path = WriteShiranux("story.shiranux", "mở đầu\ntiêu đề\nkết",
            new Dictionary<int, ParagraphAlignment> { [1] = ParagraphAlignment.Center });

        _files.WriteCombined(path, path, [new TextReplacement(7, 8, string.Empty)], Newline.Lf);

        var saved = ReadShiranux(path);

        Assert.Equal("mở đầu\nkết", saved.Text);
        Assert.Equal(ParagraphAlignment.Left, saved.AlignmentOf(0));
        Assert.Equal(ParagraphAlignment.Left, saved.AlignmentOf(1));
    }

    [Fact]
    public void A_Shiranux_Save_Spends_No_Convention_And_Writes_Line_Feeds() {
        // The status bar may name CRLF; this file's shape promises line
        // feeds regardless, and the report says line feeds so the bar tells
        // the truth about the file it just wrote.
        var path = WriteShiranux("story.shiranux", "one\ntwo");

        var result = _files.WriteCombined(
            path, path, [new TextReplacement(7, 0, "\r\nthree")], Newline.CrLf);

        var saved = ReadShiranux(path);

        Assert.Equal("one\ntwo\nthree", saved.Text);
        Assert.Equal(Newline.Lf, result.Newline);
        Assert.Equal(Newline.Lf, _files.Scan(path).Newline);
        Assert.Equal(_files.Scan(path), result);
    }

    [Fact]
    public void A_Txt_File_Saved_As_Shiranux_Starts_Flush_Left() {
        // Plain text carries no faces, so a file that becomes the app's own
        // begins with every paragraph on the left it started on.
        var source = Write("story.txt", "hello world", withBom: false);
        var target = Path.Combine(_directory, "story.shiranux");

        _files.WriteCombined(target, source, [new TextReplacement(0, 5, "HELLO")], Newline.Lf);

        var saved = ReadShiranux(target);

        Assert.Equal("HELLO world", saved.Text);
        Assert.Empty(saved.Alignments);
        Assert.Equal("hello world", File.ReadAllText(source));
    }

    [Fact]
    public void A_Shiranux_File_Saved_As_Txt_Keeps_Its_Letters_Alone() {
        // The other direction: the binary is unwrapped, and what lands in
        // the plain file is the writing with the faces left behind.
        var source = WriteShiranux("story.shiranux", "hello world",
            new Dictionary<int, ParagraphAlignment> { [0] = ParagraphAlignment.Center });
        var target = Path.Combine(_directory, "copy.txt");

        _files.WriteCombined(target, source, [new TextReplacement(0, 5, "HELLO")], Newline.Lf);

        Assert.Equal("HELLO world", File.ReadAllText(target));
        Assert.Equal("hello world", ReadShiranux(source).Text);
    }

    [Fact]
    public void A_Save_Writes_The_Lines_The_Document_Brought() {
        // The document's own map wins over the source's — it was read off
        // that same file when it opened and has been the document's to
        // change since. A line set back to left is written as the absence
        // it is rather than as a record saying left.
        var path = WriteShiranux("story.shiranux", "mở đầu\ntiêu đề",
            new Dictionary<int, ParagraphAlignment> { [1] = ParagraphAlignment.Center });

        _files.WriteCombined(
            path, path, [], Newline.Lf, null,
            new Dictionary<int, ParagraphAlignment> { [0] = ParagraphAlignment.Right });

        var saved = ReadShiranux(path);

        Assert.Equal(ParagraphAlignment.Right, saved.AlignmentOf(0));
        Assert.Equal(ParagraphAlignment.Left, saved.AlignmentOf(1));
    }

    [Fact]
    public void The_Lines_The_Document_Brought_Are_Written_Where_It_Stands_Them() {
        // The document numbers its map against the writing as it now
        // stands — the same numbers the gutter shows — carrying each face
        // along at every move. The writer lays such a map into the binary
        // as it arrives: the heading was turned into line three by the
        // line typed above it before the save was ever asked, and the
        // file gets exactly that.
        var path = WriteShiranux("story.shiranux", "mở đầu\ntiêu đề");

        _files.WriteCombined(
            path, path, [new TextReplacement(0, 0, "vài dòng\n")], Newline.Lf, null,
            new Dictionary<int, ParagraphAlignment> { [2] = ParagraphAlignment.Center });

        var saved = ReadShiranux(path);

        Assert.Equal("vài dòng\nmở đầu\ntiêu đề", saved.Text);
        Assert.Equal(ParagraphAlignment.Center, saved.AlignmentOf(2));
    }

    [Fact]
    public void A_Txt_Save_Carries_No_Way_Of_Setting_A_Line() {
        // The plain file keeps letters, and letters are all it can keep:
        // the map is spent nowhere, and asking such a file how it sets its
        // lines afterwards is a question it cannot answer.
        var path = Write("plain.txt", "one\ntwo", withBom: false);

        _files.WriteCombined(
            path, path, [], Newline.Lf, null,
            new Dictionary<int, ParagraphAlignment> { [1] = ParagraphAlignment.Center });

        Assert.Null(_files.ReadAlignments(path));
    }

    [Fact]
    public void The_Way_A_Shiranux_File_Sets_Its_Lines_Comes_Back_Out() {
        // The whole loop the type already knows: a save lays the map down,
        // and the next arrival can ask how its lines stand without reading
        // a single letter — the section before the text is stepped over.
        var path = WriteShiranux("story.shiranux", "mở đầu\ntiêu đề",
            new Dictionary<int, ParagraphAlignment> { [1] = ParagraphAlignment.Center });

        var set = _files.ReadAlignments(path);

        Assert.NotNull(set);
        Assert.Equal(ParagraphAlignment.Center, set![1]);
        Assert.False(set.ContainsKey(0));
    }

    [Fact]
    public void A_Plain_File_Sets_No_Line_Any_How() {
        // Letters, and nothing to say about how they stand.
        var path = Write("plain.txt", "one", withBom: false);

        Assert.Null(_files.ReadAlignments(path));
    }

    [Fact]
    public void A_Shiranux_File_That_Is_Not_One_Is_Refused_At_The_Door() {
        // The extension promises the mark; anything else behind it is read
        // as the file it would have been, and fails on the mark it lacks.
        var path = Path.Combine(_directory, "lying.shiranux");
        File.WriteAllText(path, "this is not the format at all");

        Assert.Throws<InvalidDataException>(() => _files.Scan(path));
        Assert.Throws<InvalidDataException>(() =>
            _files.ReadLines(path, new LineAnchor(0, 0), 10));
    }

    // ===== The type =========================================================

    [Fact]
    public void The_Type_A_Shiranux_File_Was_Written_In_Comes_Back_Out() {
        // The whole loop: a save lays the face down, and the next open can
        // ask what it is without reading a single letter.
        var path = Path.Combine(_directory, "set.shiranux");

        _files.WriteCombined(
            path, null, [new TextReplacement(0, 0, "hello world")], Newline.Lf,
            new DocumentTypography("Palatino Linotype", 21.5));

        Assert.Equal(
            new DocumentTypography("Palatino Linotype", 21.5),
            _files.ReadTypography(path));
        Assert.Equal(
            new DocumentTypography("Palatino Linotype", 21.5),
            ReadShiranux(path).Typography);
    }

    [Fact]
    public void A_Txt_File_Holds_No_Type_Any_How() {
        // The plain file is letters, and letters are all it carries — so
        // asking it what it is set in is not a question it can answer.
        var path = Write("plain.txt", "hello world", withBom: false);

        _files.WriteCombined(
            path, path, [new TextReplacement(0, 5, "HELLO")], Newline.Lf,
            new DocumentTypography("Verdana", 14));

        Assert.Null(_files.ReadTypography(path));
    }

    [Fact]
    public void Saving_Without_Bringing_A_Type_Keeps_The_The_File_Held() {
        // A writer that hands over no face — an older caller, a whole-file
        // rewrite — must not be the moment the file forgets the one it had.
        var path = WriteShiranux(
            "set.shiranux", "hello world",
            typography: new DocumentTypography("Verdana", 14));

        _files.WriteCombined(path, path, [new TextReplacement(6, 0, "brave ")], Newline.Lf);

        Assert.Equal(
            new DocumentTypography("Verdana", 14),
            _files.ReadTypography(path));
    }

    [Fact]
    public void The_Type_A_Shiranux_File_Had_Does_Not_Travel_Into_A_Txt_File() {
        // The other direction of the same rule: the plain file cannot carry
        // a face, so the face is left standing where it was rather than
        // smuggled in as letters nobody asked for.
        var source = WriteShiranux(
            "set.shiranux", "hello world",
            typography: new DocumentTypography("Verdana", 14));
        var target = Path.Combine(_directory, "copy.txt");

        _files.WriteCombined(target, source, [new TextReplacement(0, 5, "HELLO")], Newline.Lf);

        Assert.Null(_files.ReadTypography(target));
        Assert.Equal("HELLO world", File.ReadAllText(target));
    }

    [Fact]
    public void Asking_The_Type_Of_A_File_That_Is_Not_One_Fails_As_The_Read_Would() {
        // The extension promises the mark, and the question about the type
        // is refused for the same reason the read is: nothing is guessed at
        // from a file that is not this file.
        var path = Path.Combine(_directory, "lying.shiranux");
        File.WriteAllText(path, "this is not the format at all");

        Assert.Throws<InvalidDataException>(() => _files.ReadTypography(path));
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
    /// Writes an app's-own file: the text and the faces standing on it, in
    /// the shape the format writes them.
    /// </summary>
    /// <param name="name">The file's name.</param>
    /// <param name="text">The writing to carry.</param>
    /// <param name="alignments">The faces standing on it, by paragraph.</param>
    /// <param name="typography">How the paper as a whole is set.</param>
    /// <returns>The file's full path.</returns>
    private string WriteShiranux(
        string name,
        string text,
        IReadOnlyDictionary<int, ParagraphAlignment>? alignments = null,
        DocumentTypography? typography = null) {
        var path = Path.Combine(_directory, name);

        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None)) {
            ShiranuxFormat.Write(stream, new ShiranuxDocument(text, alignments, typography));
        }

        return path;
    }

    /// <summary>
    /// Reads an app's-own file back through the format, the way opening it
    /// would.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The document it carries.</returns>
    private static ShiranuxDocument ReadShiranux(string path) {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return ShiranuxFormat.Read(stream);
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
