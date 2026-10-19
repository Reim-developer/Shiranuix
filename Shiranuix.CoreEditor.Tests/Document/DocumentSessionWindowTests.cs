namespace Shiranuix.CoreEditor.Tests.Document;

/// <summary>
/// Tests for the window <see cref="DocumentSession"/> shows onto a file
/// larger than the screen: where it stands, how it travels, and how the
/// counts, the gutter and the caret follow it.
/// </summary>
/// <remarks>
/// Every case opens a file that runs past one window, so the rules about
/// what the writer sees — and what the document still knows about the
/// parts they cannot — are checked without a window, a file system, or a
/// prompt.
/// </remarks>
public sealed class DocumentSessionWindowTests {
    /// <summary>The large file these cases open.</summary>
    private const string BigPath = "big.txt";

    private readonly SessionFactory _factory = new();

    private FakeFileService _files => _factory.Files;

    private DocumentSession CreateSession() => _factory.CreateSession();

    /// <summary>
    /// A file of numbered lines — enough of them to run past a window.
    /// </summary>
    /// <param name="count">How many lines.</param>
    /// <returns>The file's text.</returns>
    private static string ManyLines(int count) =>
        string.Join('\n', Enumerable.Range(0, count).Select(index => $"line {index}"));

    /// <summary>The numbers the gutter shows for a run of lines.</summary>
    /// <param name="first">The first number.</param>
    /// <param name="count">How many numbers.</param>
    private static string Gutter(int first, int count) =>
        string.Join('\n', Enumerable.Range(first, count));

    [Fact]
    public void Opening_A_Large_File_Shows_Its_First_Window_And_Whole_File_Counts() {
        _files.Files[BigPath] = ManyLines(60);
        var session = CreateSession();

        session.Open(BigPath);

        // The window holds what it can hold; every number beside it belongs
        // to the whole file, which is what the writer is actually editing.
        Assert.Equal(ManyLines(50), session.Text);
        Assert.Equal(ManyLines(60).Length, session.CharacterCount);
        Assert.Equal(120, session.WordCount);
        Assert.Equal(60, session.LineCount);
        Assert.Equal(Gutter(1, 50), session.LineNumbersText);
        Assert.Equal(1, session.CaretLine);
    }

    [Fact]
    public void The_Caret_At_The_Window_S_End_Slides_It_Further_Into_The_File() {
        _files.Files[BigPath] = ManyLines(60);
        var session = CreateSession();
        session.Open(BigPath);

        // Standing at the end of the window is the writer asking to see
        // what comes next: the window moves on, ten lines of context stay
        // above the caret, and the numbers keep counting the file.
        session.CaretIndex = session.Text.Length;

        Assert.Equal(
            string.Join('\n', Enumerable.Range(39, 21).Select(index => $"line {index}")),
            session.Text);
        Assert.Equal(Gutter(40, 21), session.LineNumbersText);
        Assert.Equal(60, session.LineCount);

        // The caret itself does not jump to the window's new end — it keeps
        // its place in the file (the last line of where it was standing),
        // with the fresh context showing past it.
        Assert.Equal(50, session.CaretLine);
    }

    [Fact]
    public void The_Caret_At_The_Window_S_Start_Slides_It_Back() {
        _files.Files[BigPath] = ManyLines(60);
        var session = CreateSession();
        session.Open(BigPath);
        session.CaretIndex = session.Text.Length;

        session.CaretIndex = 0;

        Assert.Equal(ManyLines(50), session.Text);
        Assert.Equal(Gutter(1, 50), session.LineNumbersText);

        // The window slid under the caret: it still marks the same line of
        // the file it marked before — the one the earlier window began on.
        Assert.Equal(40, session.CaretLine);
    }

    [Fact]
    public void A_Move_Beyond_The_Window_Still_Counts_In_The_File() {
        _files.Files[BigPath] = ManyLines(60);
        var session = CreateSession();
        session.Open(BigPath);

        session.Text = "EXTRA " + session.Text;
        session.CaretIndex = session.Text.Length;

        // The move is not on screen anymore, but the file still carries
        // it: six characters and one word more than the scan counted.
        Assert.Equal(ManyLines(60).Length + 6, session.CharacterCount);
        Assert.Equal(121, session.WordCount);
        Assert.Equal(60, session.LineCount);
    }

    [Fact]
    public void Undo_Across_A_Slide_Returns_To_The_Window_It_Was_Made_In() {
        _files.Files[BigPath] = ManyLines(60);
        var session = CreateSession();
        session.Open(BigPath);

        session.Text = session.Text + "X";
        session.CaretIndex = session.Text.Length;
        session.Text = session.Text + "Y";

        session.Undo();

        // The second move was made in the window now on screen, so nothing
        // travels: only its text comes off.
        Assert.EndsWith("line 59", session.Text);
        Assert.DoesNotContain('Y', session.Text);

        session.Undo();

        // The first was made a window ago: the window goes back with it,
        // or the entry would be inverted against the wrong text.
        Assert.Equal(ManyLines(50), session.Text);
        Assert.Equal(Gutter(1, 50), session.LineNumbersText);
        Assert.False(session.CanUndo);
        Assert.False(session.IsDirty);

        session.Redo();
        Assert.Equal(ManyLines(50) + "X", session.Text);
    }

    [Fact]
    public void Saving_Writes_Every_Window_S_Moves_Into_One_File() {
        _files.Files[BigPath] = ManyLines(60);
        var session = CreateSession();
        session.Open(BigPath);

        session.Text = "EXTRA " + session.Text;
        session.CaretIndex = session.Text.Length;
        session.Text = session.Text + " Y";

        var outcome = session.Save(BigPath);

        // Two windows, two moves, one file: the ledger streams the whole
        // thing together, however little of it was ever on screen.
        Assert.True(outcome.Succeeded);
        Assert.StartsWith("EXTRA line 0", _files.Files[BigPath]);
        Assert.EndsWith("line 59 Y", _files.Files[BigPath]);
        Assert.False(session.IsDirty);
        Assert.Equal(ManyLines(60).Length + 8, session.CharacterCount);
        Assert.Equal(60, session.LineCount);
    }

    [Fact]
    public void Undo_After_A_Save_Returns_The_Window_At_Its_New_Place() {
        // A save moves the file under the entries: what the window covered
        // slid by the moves that collapsed in ahead of it, and the history
        // has to find it there.
        _files.Files[BigPath] = ManyLines(60);
        var session = CreateSession();
        session.Open(BigPath);

        session.Text = "EXTRA " + session.Text;
        session.CaretIndex = session.Text.Length;
        session.Text = session.Text + " Y";
        session.Save(BigPath);

        session.Undo();
        session.Undo();

        Assert.Equal(ManyLines(50), session.Text);
        Assert.Equal(Gutter(1, 50), session.LineNumbersText);
        Assert.True(session.CanRedo);
    }

    [Fact]
    public void An_Edge_Steps_Over_A_Move_It_Would_Cut_In_Half() {
        // A move spanning whole lines — the kind a paste makes — straddles
        // the boundary the slide would land on. It was made whole, and the
        // window steps back to its first line rather than cut it.
        _files.Files[BigPath] = ManyLines(60);
        var session = CreateSession();
        session.Open(BigPath);

        session.Text = session.Text.Replace("line 38\nline 39", "merged\nlines!");
        session.CaretIndex = session.Text.Length;

        Assert.StartsWith("merged\nlines!", session.Text);
        Assert.StartsWith("39\n", session.LineNumbersText);
        Assert.Equal(60, session.LineCount);
    }

    [Fact]
    public void A_Move_Before_The_Window_Renumbers_The_Gutter() {
        // A line added at the top of the first window shifts every line of
        // the file down — including the ones the next window shows, whose
        // numbers come from the walk plus what those moves did.
        _files.Files[BigPath] = ManyLines(60);
        var session = CreateSession();
        session.Open(BigPath);

        session.Text = "new line\n" + session.Text;
        session.CaretIndex = session.Text.Length;

        // The file on disk never saw the added line — the ledger carries it
        // — so the walk lands on the file's own line 40, and the gutter
        // numbers it forty plus the line added above it plus one: the
        // whole-file line the window now shows.
        Assert.StartsWith("line 40", session.Text);
        Assert.StartsWith("42\n", session.LineNumbersText);
        Assert.Equal(61, session.LineCount);
    }

    [Fact]
    public void Scrolling_Moves_The_Window_By_The_Asked_Lines() {
        _files.Files[BigPath] = ManyLines(60);
        var session = CreateSession();
        session.Open(BigPath);

        session.SlideBy(3);

        // Three lines forward, the window still fifty deep, the numbers
        // still the file's.
        Assert.Equal(
            string.Join('\n', Enumerable.Range(3, 50).Select(index => $"line {index}")),
            session.Text);
        Assert.Equal(Gutter(4, 50), session.LineNumbersText);

        // And back past where it came from: clamped to the file's own
        // beginning, which is as far back as the file goes.
        session.SlideBy(-10);

        Assert.Equal(ManyLines(50), session.Text);
        Assert.Equal(Gutter(1, 50), session.LineNumbersText);
    }

    [Fact]
    public void Scrolling_Stops_At_The_File_S_Edges() {
        _files.Files[BigPath] = ManyLines(60);
        var session = CreateSession();
        session.Open(BigPath);

        // Forward as far as it goes: the window starts on the last line it
        // was holding and reads the file's own end from there — eleven
        // lines of tail rather than an empty page past the file.
        session.SlideBy(100);

        Assert.Equal(
            string.Join('\n', Enumerable.Range(49, 11).Select(index => $"line {index}")),
            session.Text);
        Assert.Equal(Gutter(50, 11), session.LineNumbersText);

        // Further forward is refused: the file has nothing beyond to show.
        session.SlideBy(3);
        Assert.Equal(Gutter(50, 11), session.LineNumbersText);

        // Back past the beginning clamps to it, and once there back is
        // refused the same way.
        session.SlideBy(-1000);
        Assert.Equal(ManyLines(50), session.Text);

        session.SlideBy(-5);
        Assert.Equal(ManyLines(50), session.Text);
        Assert.Equal(Gutter(1, 50), session.LineNumbersText);
    }

    [Fact]
    public void Scrolling_Carries_The_Caret_S_Place_In_The_File() {
        _files.Files[BigPath] = ManyLines(60);
        var session = CreateSession();
        session.Open(BigPath);
        session.CaretIndex = session.Text.IndexOf("line 25", StringComparison.Ordinal) + 3;

        var line = session.CaretLine;
        var column = session.CaretColumn;

        session.SlideBy(10);

        // The wheel turns the window around the writer, not the writer
        // around the window: the caret still marks line twenty-six, four
        // columns in, however far its line now sits from the edges.
        Assert.Equal(line, session.CaretLine);
        Assert.Equal(column, session.CaretColumn);
    }

    [Fact]
    public void Scrolling_Far_Aside_Clamps_The_Caret_Into_What_Is_Shown() {
        _files.Files[BigPath] = ManyLines(60);
        var session = CreateSession();
        session.Open(BigPath);
        session.CaretIndex = session.Text.IndexOf("line 2", StringComparison.Ordinal);

        session.SlideBy(20);

        // The caret's line is not on screen any more, so the caret stands
        // at the window's first line — visibly, where the next keystroke
        // would land rather than a place with nothing around it.
        Assert.Equal(21, session.CaretLine);
    }

    [Fact]
    public void Jumping_To_The_End_Shows_The_File_S_Last_Window() {
        _files.Files[BigPath] = ManyLines(60);
        var session = CreateSession();
        session.Open(BigPath);

        session.SlideToEnd();

        // One jump, the last fifty lines — not forty lines of slide.
        Assert.Equal(
            string.Join('\n', Enumerable.Range(10, 50).Select(index => $"line {index}")),
            session.Text);
        Assert.Equal(Gutter(11, 50), session.LineNumbersText);
        Assert.True(session.HasLinesAbove);
        Assert.False(session.HasLinesBelow);
    }

    [Fact]
    public void Jumping_To_The_Start_Shows_The_First_Window() {
        _files.Files[BigPath] = ManyLines(60);
        var session = CreateSession();
        session.Open(BigPath);
        session.SlideToEnd();

        session.SlideToStart();

        Assert.Equal(ManyLines(50), session.Text);
        Assert.Equal(Gutter(1, 50), session.LineNumbersText);
    }

    [Fact]
    public void The_Window_S_Edges_Say_What_Sits_Beyond_Them() {
        var session = CreateSession();

        // A blank document is all window: nothing waits above or below.
        Assert.False(session.HasLinesAbove);
        Assert.False(session.HasLinesBelow);

        _files.Files[BigPath] = ManyLines(60);
        session.Open(BigPath);

        // The first window begins the file, and the file carries on past it.
        Assert.False(session.HasLinesAbove);
        Assert.True(session.HasLinesBelow);

        session.SlideBy(3);

        // Off the beginning now — and still short of the end.
        Assert.True(session.HasLinesAbove);
        Assert.True(session.HasLinesBelow);

        session.SlideBy(100);

        // The tail: more above than ever, nothing below.
        Assert.True(session.HasLinesAbove);
        Assert.False(session.HasLinesBelow);

        session.New();

        Assert.False(session.HasLinesAbove);
        Assert.False(session.HasLinesBelow);
    }
}
