namespace Shiranuix.CoreEditor.Tests.Services;

/// <summary>
/// Tests for <see cref="DocumentWindow"/>: where the window stands, what it
/// remembers, and how it plans its moves.
/// </summary>
/// <remarks>
/// The engine is the session's walking legs, so these check it directly
/// against a faked file: a plan lands on the line asked for, remembers where
/// it stood for the next walk, refuses a position the file does not have, and
/// slides every coordinate after what a save collapsed. The screen-side rules
/// — text, caret, gutter — stay in the session's own tests.
/// </remarks>
public sealed class DocumentWindowTests {
    private const string BigPath = "big.txt";

    private readonly FakeFileService _files = new();
    private readonly EditLedger _ledger = new();
    private readonly DocumentWindow _window;

    public DocumentWindowTests() => _window = new DocumentWindow(_files);

    /// <summary>A file of numbered lines — enough of them to run past a window.</summary>
    private static string ManyLines(int count) =>
        string.Join('\n', Enumerable.Range(0, count).Select(index => $"line {index}"));

    /// <summary>A run of numbered lines, as a window over them would read.</summary>
    private static string LinesFrom(int first, int count) =>
        string.Join('\n', Enumerable.Range(first, count).Select(index => $"line {index}"));

    [Fact]
    public void Begin_Points_The_Window_And_Forgets_Every_Walk_Before_It() {
        _files.Files[BigPath] = ManyLines(60);
        _window.Begin(new LineAnchor(0, 0), 0, atEnd: true);
        _window.Adopt(_window.Plan(BigPath, 40, _ledger));

        _window.Begin(new LineAnchor(0, 0), 0, atEnd: false);

        Assert.Equal(new LineAnchor(0, 0), _window.Start);
        Assert.Equal(0, _window.FileLine);
        Assert.False(_window.AtEnd);

        // With the earlier stands forgotten, the walk back to line 40
        // starts from the file's own beginning and still lands there.
        var plan = _window.Plan(BigPath, 40, _ledger);

        Assert.Equal(40, plan.FileLine);
        Assert.StartsWith("line 40", plan.Original);
    }

    [Fact]
    public void A_Plan_Lands_On_The_Asked_Line_With_The_File_Carrying_On_Past_It() {
        _files.Files[BigPath] = ManyLines(60);
        _window.Begin(new LineAnchor(0, 0), 0, atEnd: true);

        var plan = _window.Plan(BigPath, 5, _ledger);

        Assert.Equal(5, plan.FileLine);
        Assert.Equal(LinesFrom(5, 50), plan.Original);
        Assert.True(plan.AtEnd);

        // A window near the tail runs out of file first: it stops at the
        // last line and says the file does not carry on.
        var tail = _window.Plan(BigPath, 45, _ledger);

        Assert.Equal(45, tail.FileLine);
        Assert.Equal(LinesFrom(45, 15), tail.Original);
        Assert.False(tail.AtEnd);
    }

    [Fact]
    public void An_Adopted_Window_Is_Where_The_Next_Walk_Begins() {
        _files.Files[BigPath] = ManyLines(60);
        _window.Begin(new LineAnchor(0, 0), 0, atEnd: true);
        _window.Adopt(_window.Plan(BigPath, 40, _ledger));

        Assert.Equal(40, _window.FileLine);
        Assert.False(_window.AtEnd);

        // Walking back covers only the lines between the two stands rather
        // than the whole file, and lands on the line asked for.
        var back = _window.Plan(BigPath, 10, _ledger);

        Assert.Equal(10, back.FileLine);
        Assert.Equal(LinesFrom(10, 50), back.Original);
    }

    [Fact]
    public void A_Position_The_File_Does_Not_Stand_On_Comes_Back_With_Nothing() {
        _files.Files[BigPath] = ManyLines(60);
        _window.Begin(new LineAnchor(0, 0), 0, atEnd: true);
        var text = ManyLines(60);

        // One past the line's own start is not a line start at all, so the
        // walk cannot reach it and there is no window to plan.
        Assert.Null(_window.PlanAt(BigPath, text.IndexOf("line 17", StringComparison.Ordinal) + 3, _ledger));

        // A line's own start is exactly what the walk lands on.
        var plan = _window.PlanAt(BigPath, text.IndexOf("line 17", StringComparison.Ordinal), _ledger);

        Assert.NotNull(plan);
        Assert.Equal(17, plan.Value.FileLine);
        Assert.StartsWith("line 17", plan.Value.Original);
    }

    [Fact]
    public void A_Shift_Moves_The_Stand_And_Every_Remembered_One_After_It() {
        _files.Files[BigPath] = ManyLines(60);
        var text = ManyLines(60);
        _window.Begin(new LineAnchor(0, 0), 0, atEnd: true);

        // A move standing at the file's start — two characters and a line
        // more than it was read with — before the window travels there.
        _ledger.ResetWindow(0, text);
        _ledger.Note(text, "X\n" + text);

        var plan = _window.Plan(BigPath, 40, _ledger);
        _window.Adopt(plan);
        _ledger.ResetWindow(plan.Start.Offset, plan.Original);

        _window.Shift(_ledger);

        // The window keeps showing exactly what it showed — only where it
        // stands has moved: two characters in, one line down.
        Assert.Equal(new LineAnchor(plan.Start.Byte + 2, plan.Start.Offset + 2), _window.Start);
        Assert.Equal(41, _window.FileLine);
        Assert.False(_window.AtEnd);
    }
}
