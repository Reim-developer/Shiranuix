using Shiranuix.CoreFormat;
namespace Shiranuix.CoreEditor.Tests.Document;

/// <summary>
/// Tests for how <see cref="DocumentSession"/> carries the way each line is
/// set: what arrives with a file, what a press of the bar's chips puts down,
/// when that counts as a change, and what a save hands back.
/// </summary>
/// <remarks>
/// The fake disk keeps the map exactly as it is handed over rather than
/// carrying it to where the letters went — that carrying is the writer's own
/// work, and it is tested against the real one beside the file service. These
/// cases are therefore about the document's half: arriving, being chosen,
/// being remembered as clean or dirty, and being asked for on the way out.
/// </remarks>
public sealed class DocumentSessionAlignmentTests {
    private const string OwnPath = "story.shiranux";
    private const string PlainPath = "story.txt";

    private readonly SessionFactory _factory = new();

    private FakeFileService _files => _factory.Files;

    private DocumentSession CreateSession() => _factory.CreateSession();

    /// <summary>
    /// Puts a file of the app's own on the fake disk, with its lines set.
    /// </summary>
    /// <param name="text">What the file holds.</param>
    /// <param name="set">Which lines stand out, by their number.</param>
    /// <returns>A session that has opened it.</returns>
    private DocumentSession OpenOwn(
        string text,
        IReadOnlyDictionary<int, ParagraphAlignment>? set = null) {
        _files.Files[OwnPath] = text;
        _files.Alignments[OwnPath] = set ?? new Dictionary<int, ParagraphAlignment>();

        var session = CreateSession();
        session.Open(OwnPath);

        return session;
    }

    [Fact]
    public void A_File_Arriving_Brings_The_Way_Its_Lines_Are_Set() {
        var session = OpenOwn(
            "Chương Một\nTiêu đề\nĐêm đó",
            new Dictionary<int, ParagraphAlignment> { [1] = ParagraphAlignment.Center });

        Assert.Single(session.Alignments);
        Assert.Equal(ParagraphAlignment.Center, session.Alignments[1]);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void The_Line_The_Caret_Stands_On_Says_What_The_File_Says() {
        // The caret starts on the first line, which nobody set; the second
        // line is the one the file centred, and the caret stands on it the
        // moment it moves there.
        var session = OpenOwn(
            "Chương Một\nTiêu đề\nĐêm đó",
            new Dictionary<int, ParagraphAlignment> { [1] = ParagraphAlignment.Center });

        Assert.Equal(ParagraphAlignment.Left, session.AlignmentAtCaret);

        session.CaretIndex = session.Text.IndexOf('\n') + 1;

        Assert.Equal(ParagraphAlignment.Center, session.AlignmentAtCaret);
    }

    [Fact]
    public void Setting_The_Line_The_Caret_Stands_On_Is_A_Change_A_Save_Would_Write() {
        var session = OpenOwn("Chương Một\nTiêu đề");
        Assert.False(session.IsDirty);

        session.CaretIndex = 0;
        session.AlignCaret(ParagraphAlignment.Center);

        Assert.Equal(ParagraphAlignment.Center, session.AlignmentAtCaret);
        Assert.Single(session.Alignments);
        Assert.True(session.IsDirty);
    }

    [Fact]
    public void Taking_The_Line_Back_To_Left_Leaves_It_Out_Of_The_Map() {
        // Flush left is what a line is when nothing said anything about it,
        // so a line set back to it is no longer a line the file would be
        // asked to remember — and a file that is carrying one centred line
        // would be written without it, which is exactly the sort of change
        // a save makes.
        var session = OpenOwn(
            "Chương Một\nTiêu đề",
            new Dictionary<int, ParagraphAlignment> { [1] = ParagraphAlignment.Center });
        session.CaretIndex = session.Text.IndexOf('\n') + 1;

        session.AlignCaret(ParagraphAlignment.Left);

        Assert.Empty(session.Alignments);
        Assert.Equal(ParagraphAlignment.Left, session.AlignmentAtCaret);
        Assert.True(session.IsDirty);
    }

    [Fact]
    public void Setting_The_Line_Back_To_What_The_File_Already_Says_Cleans_The_Document() {
        // A press that leaves the file exactly as it stands is not a change
        // to it: the map the document ends on is the map it was handed.
        var session = OpenOwn(
            "Chương Một\nTiêu đề",
            new Dictionary<int, ParagraphAlignment> { [1] = ParagraphAlignment.Center });
        session.CaretIndex = session.Text.IndexOf('\n') + 1;

        session.AlignCaret(ParagraphAlignment.Center);

        Assert.Equal(ParagraphAlignment.Center, session.Alignments[1]);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void A_Plain_File_Holds_No_Way_Of_Setting_A_Line_So_Choosing_One_Changes_Nothing() {
        // A .txt keeps its letters and nothing else: the chips still light
        // and the page still draws, but the choice is not a change to a file
        // that could never carry it — the same footing the typeface stands
        // on over such a document.
        _files.Files[PlainPath] = "Một\nHai";
        var session = CreateSession();
        session.Open(PlainPath);

        session.AlignCaret(ParagraphAlignment.Center);

        Assert.Equal(ParagraphAlignment.Center, session.AlignmentAtCaret);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void A_Save_Writes_The_Lines_The_Document_Chose_And_Leaves_It_Clean() {
        var session = OpenOwn("Chương Một\nTiêu đề");
        session.CaretIndex = session.Text.IndexOf('\n') + 1;
        session.AlignCaret(ParagraphAlignment.Center);

        var outcome = session.Save(OwnPath);

        Assert.Equal(DocumentOutcome.Done, outcome);
        Assert.Equal(ParagraphAlignment.Center, _files.Alignments[OwnPath][1]);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void Reopening_A_Saved_File_Brings_The_Same_Lines_Back() {
        // The numbers a save remembers are the ones the file it wrote names,
        // so a close and open later reads the same answers — not the ones the
        // document happened to hold before the write.
        var session = OpenOwn("Chương Một\nTiêu đề");
        session.CaretIndex = session.Text.IndexOf('\n') + 1;
        session.AlignCaret(ParagraphAlignment.Right);
        session.Save(OwnPath);

        var reopened = CreateSession();
        reopened.Open(OwnPath);

        Assert.Equal(ParagraphAlignment.Right, reopened.Alignments[1]);
        Assert.False(reopened.IsDirty);
    }

    [Fact]
    public void Choosing_Where_A_Line_Stands_Over_A_Plain_File_Writes_It_Into_A_File_Of_The_Apps_Own() {
        // The choice held over a .txt is held for a target that can carry
        // it: saving as the app's own file is the moment it becomes the
        // file's own, flush left lines included as the absence they are.
        _files.Files[PlainPath] = "Một\nHai";
        var session = CreateSession();
        session.Open(PlainPath);
        session.CaretIndex = session.Text.IndexOf('\n') + 1;
        session.AlignCaret(ParagraphAlignment.Center);

        var outcome = session.Save(OwnPath);

        Assert.Equal(DocumentOutcome.Done, outcome);
        Assert.Equal(ParagraphAlignment.Center, _files.Alignments[OwnPath][1]);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void A_New_Document_Stands_On_No_Lines_At_All() {
        // A blank document replaced the file its lines belonged to, so it
        // inherits none of them — every line of it is flush left the way a
        // page nothing has been said about is.
        var session = OpenOwn(
            "Chương Một\nTiêu đề",
            new Dictionary<int, ParagraphAlignment> { [1] = ParagraphAlignment.Center });
        session.CaretIndex = session.Text.IndexOf('\n') + 1;

        session.New();

        Assert.Empty(session.Alignments);
        Assert.Equal(ParagraphAlignment.Left, session.AlignmentAtCaret);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void A_File_Nobody_Aligned_Opens_Clean_And_Flush_Left() {
        // A file of the app's own that has never had a line set keeps no
        // section at all — it is not a file missing something, and opening
        // it is not a change to anything.
        _files.Files[OwnPath] = "Một\nHai";
        _files.Alignments[OwnPath] = new Dictionary<int, ParagraphAlignment>();

        var session = CreateSession();
        session.Open(OwnPath);

        Assert.Empty(session.Alignments);
        Assert.Equal(ParagraphAlignment.Left, session.AlignmentAtCaret);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void Typing_Does_Not_Move_The_Way_A_Line_Is_Set() {
        // The map is numbered against the file, which typing does not move,
        // so the centred heading stays the centred heading while letters are
        // added to the line before it — it is the save that carries the map
        // onto the writing that came out.
        var session = OpenOwn(
            "mở đầu\ntiêu đề",
            new Dictionary<int, ParagraphAlignment> { [1] = ParagraphAlignment.Center });

        session.Text = "vài dòng\nmở đầu\ntiêu đề";

        Assert.Equal(ParagraphAlignment.Center, session.Alignments[1]);
    }

    [Fact]
    public void Opening_A_Plain_File_Lands_On_No_Lines_Even_When_The_Last_One_Had() {
        // The map is the arriving file's, never the previous document's: a
        // plain file sets no line, and inheriting the last file's centred
        // heading would draw a say the writing never made.
        var session = OpenOwn(
            "mở đầu\ntiêu đề",
            new Dictionary<int, ParagraphAlignment> { [1] = ParagraphAlignment.Center });

        _files.Files[PlainPath] = "Một\nHai";
        session.Open(PlainPath);

        Assert.Empty(session.Alignments);
        Assert.Equal(ParagraphAlignment.Left, session.AlignmentAtCaret);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void Any_Line_Of_The_Window_Can_Be_Asked_What_The_File_Says() {
        // The page draws every line it holds, not only the one the caret
        // stands on, and each is answered the same way that one is: what the
        // file said about the file line it stands on, flush left when the
        // file said nothing about it.
        var session = OpenOwn(
            "Chương Một\nTiêu đề\nĐêm đó",
            new Dictionary<int, ParagraphAlignment> { [1] = ParagraphAlignment.Center });

        Assert.Equal(ParagraphAlignment.Left, session.AlignmentAtWindowLine(0));
        Assert.Equal(ParagraphAlignment.Center, session.AlignmentAtWindowLine(1));
        Assert.Equal(ParagraphAlignment.Left, session.AlignmentAtWindowLine(2));
    }

    [Fact]
    public void The_Answer_Follows_The_Letters_When_Typing_Moves_Them_Down() {
        // A line typed in above the centred heading pushes the heading down
        // a line in the window, and what stands beside it moves with the
        // letters — the same way a save carries the map onto them.
        var session = OpenOwn(
            "mở đầu\ntiêu đề",
            new Dictionary<int, ParagraphAlignment> { [1] = ParagraphAlignment.Center });

        session.Text = "vài dòng\nmở đầu\ntiêu đề";

        Assert.Equal(ParagraphAlignment.Left, session.AlignmentAtWindowLine(0));
        Assert.Equal(ParagraphAlignment.Left, session.AlignmentAtWindowLine(1));
        Assert.Equal(ParagraphAlignment.Center, session.AlignmentAtWindowLine(2));
    }

    [Fact]
    public void Asking_About_A_Line_The_Text_Does_Not_Reach_Is_Flush_Left() {
        // A number past every line the text has stands on the empty line a
        // last newline leaves behind, and nothing has ever set that one.
        var session = OpenOwn(
            "Chương Một\nTiêu đề",
            new Dictionary<int, ParagraphAlignment> { [0] = ParagraphAlignment.Right });

        Assert.Equal(ParagraphAlignment.Left, session.AlignmentAtWindowLine(99));
    }
}
