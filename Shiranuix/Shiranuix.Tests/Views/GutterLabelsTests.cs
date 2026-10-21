using Shiranuix.Views;

namespace Shiranuix.Tests.Views;

/// <summary>
/// Tests for the gutter's labels: the window's numbers spread over the rows
/// the editor laid the text out in.
/// </summary>
/// <remarks>
/// The rows here are given as character offsets rather than measured, which
/// is the whole point of the split: the editor's layout is the only thing
/// that knows where a line wraps, and this mapping — number on the row a
/// line starts on, nothing on the rows it wraps over — is what the view
/// owes it back.
/// </remarks>
public sealed class GutterLabelsTests {
    [Fact]
    public void An_Unwrapped_Page_Draws_The_Numbers_It_Was_Given() {
        // No wrapping means one row per line, so the spread has to land back
        // on the run itself — that identity is what keeps the gutter looking
        // exactly as it did before word wrap was ever toggled.
        var rows = new[] { 0, 6, 11 };

        var labels = GutterLabels.For("1\n2\n3", "alpha\nbeta\ngamma", rows);

        Assert.Equal("1\n2\n3", labels);
    }

    [Fact]
    public void A_Wrapped_Line_Takes_Its_Number_On_Its_First_Row_Only() {
        // The bug this pins down: a long line drawn as two rows pushed every
        // number below it beside the wrong text. The second row of the first
        // line carries nothing, and the second line's number waits for the
        // row where that line actually starts.
        var rows = new[] { 0, 10, 20 };

        var labels = GutterLabels.For("1\n2", "aaaaaaaaaaaaaaaaaaa\nb", rows);

        Assert.Equal("1\n\n2", labels);
    }

    [Fact]
    public void The_Numbers_Keep_Their_Place_In_The_File() {
        // The gutter numbers the file, not the window: a window standing at
        // line 40 wraps the same way, and the spread must not renumber it.
        var rows = new[] { 0, 3, 6 };

        var labels = GutterLabels.For("40\n41", "forty\nforty-one", rows);

        Assert.Equal("40\n\n41", labels);
    }

    [Fact]
    public void A_CRLF_Line_Keeps_Its_Number() {
        // A Windows file's line ends with two characters, and the row after
        // them still starts a line — the number must land on it, not treat
        // the pair as a continuation.
        var rows = new[] { 0, 3 };

        var labels = GutterLabels.For("1\n2", "a\r\nb", rows);

        Assert.Equal("1\n2", labels);
    }

    [Fact]
    public void A_Lone_Carriage_Return_Breaks_The_Layout_But_Not_The_Numbers() {
        // The layout breaks a lone carriage return, but the counts — and so
        // the file's lines — only count newlines. The row after it keeps its
        // number where it is rather than skipping ahead of the counts.
        var rows = new[] { 0, 2 };

        var labels = GutterLabels.For("1", "a\rb", rows);

        Assert.Equal("1\n", labels);
    }

    [Fact]
    public void An_Empty_Page_Still_Numbers_Its_First_Line() {
        // An empty document is one line with the cursor on it, laid out as
        // one empty row: the number the counts worked out still lands.
        var rows = new[] { 0 };

        var labels = GutterLabels.For("1", string.Empty, rows);

        Assert.Equal("1", labels);
    }

    [Fact]
    public void Rows_Beyond_The_Last_Number_Draw_Nothing() {
        // A layout ahead of its numbers (a wrapping that grew while the
        // counts had yet to catch up) draws blanks rather than inventing
        // numbers for rows nobody has counted.
        var rows = new[] { 0, 2, 4, 6 };

        var labels = GutterLabels.For("1\n2", "a\nb\nc\nd", rows);

        Assert.Equal("1\n2\n\n", labels);
    }

    [Fact]
    public void Numbers_Beyond_The_Last_Row_Are_Dropped() {
        // Fewer rows than numbers means the layout is behind the counts; the
        // gutter shows what has a row to sit beside and no more.
        var rows = new[] { 0 };

        var labels = GutterLabels.For("1\n2\n3", "a\nb\nc", rows);

        Assert.Equal("1", labels);
    }

    [Fact]
    public void No_Rows_Leaves_The_Numbers_As_They_Were() {
        // Before the template has laid anything out there is nothing to
        // spread over, and blanking the gutter would flash it away.
        var rows = Array.Empty<int>();

        var labels = GutterLabels.For("1\n2\n3", "a\nb", rows);

        Assert.Equal("1\n2\n3", labels);
    }
}
