using Shiranuix.Models;
using Shiranuix.ViewModels;

namespace Shiranuix.Tests.ViewModels;

/// <summary>
/// The session's half of the status bar: where the caret stands — counted
/// with the input method's composing character included, because that is
/// what the writer is looking at.
/// </summary>
/// <remarks>
/// The composition is the reason these have a file of their own: a
/// Vietnamese keystroke draws its character at the caret before the text
/// holds it, and every one of these rules — the column, the clamp, the
/// announcement — has to agree with what is on screen at that moment.
/// </remarks>
public sealed partial class DocumentSessionTests {
    // ===== What the status bar shows ============================================

    [Fact]
    public void A_Fresh_Document_Is_On_Line_One_Column_One_UTF8_And_The_Os_Convention() {
        var session = CreateSession();

        Assert.Equal(1, session.CaretLine);
        Assert.Equal(1, session.CaretColumn);
        Assert.Equal("UTF-8", session.Encoding);
        Assert.Equal(Newline.Default.Name, session.Newline.Name);
    }

    [Fact]
    public void The_Caret_Counts_Lines_And_Columns_From_The_Start_Of_Its_Line() {
        var session = CreateSession();
        session.Text = "alpha\nbeta";

        session.CaretIndex = 0;
        Assert.Equal(1, session.CaretLine);
        Assert.Equal(1, session.CaretColumn);

        session.CaretIndex = 5; // the newline itself, still the end of line one
        Assert.Equal(1, session.CaretLine);
        Assert.Equal(6, session.CaretColumn);

        session.CaretIndex = 6; // the first character of the second line
        Assert.Equal(2, session.CaretLine);
        Assert.Equal(1, session.CaretColumn);

        session.CaretIndex = 9; // four characters into "beta"
        Assert.Equal(2, session.CaretLine);
        Assert.Equal(4, session.CaretColumn);
    }

    [Fact]
    public void A_Composing_Character_Counts_In_The_Caret_Column() {
        // The Vietnamese keystroke that has not committed yet: "â" is drawn at
        // the caret and is not in the text, but the writer is looking at a
        // column of two — and the status bar has to agree with their eyes.
        var session = CreateSession();

        session.Composition = Composition.Of("â", null);

        Assert.Equal(1, session.CaretLine);
        Assert.Equal(2, session.CaretColumn);
        Assert.True(session.IsEmpty); // drawn is not written
    }

    [Fact]
    public void The_Caret_Reads_At_The_Input_Methods_Cursor_Inside_The_Composition() {
        // An input method may stand its cursor inside what it is composing. The
        // editor draws the caret there, so the column reads there.
        var session = CreateSession();
        session.Text = "ab";
        session.CaretIndex = 1;

        session.Composition = Composition.Of("x", 0); // page "axb", caret before the x

        Assert.Equal(1, session.CaretLine);
        Assert.Equal(2, session.CaretColumn);
    }

    [Fact]
    public void A_Composition_Committed_Into_The_Text_Is_Not_Counted_Twice() {
        var session = CreateSession();
        session.Composition = Composition.Of("â", null);
        Assert.Equal(2, session.CaretColumn);

        session.Text = "â"; // the input method commits, the caret travels with it
        session.CaretIndex = 1;

        Assert.True(session.Composition.IsEmpty);
        Assert.Equal(2, session.CaretColumn);
    }

    [Fact]
    public void A_Composition_Goes_When_The_Text_Does() {
        // The composition belongs to the text it was composed against. Any
        // other edit leaves it counting a character nobody can see.
        var session = CreateSession();
        session.Text = "word";
        session.Composition = Composition.Of("x", 1);

        session.Text = "words";

        Assert.True(session.Composition.IsEmpty);
    }

    [Fact]
    public void A_Composition_Change_Announces_The_Caret() {
        // The status bar binds the line and the column; a change nobody hears
        // is a status bar that still reads the old place.
        var session = CreateSession();
        var raised = new List<string?>();
        session.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        session.Composition = Composition.Of("â", 1);

        Assert.Contains(nameof(DocumentSession.Composition), raised);
        Assert.Contains(nameof(DocumentSession.CaretLine), raised);
        Assert.Contains(nameof(DocumentSession.CaretColumn), raised);
    }

    [Fact]
    public void A_Caret_Past_The_End_Of_The_Text_Is_Clamped() {
        var session = CreateSession();
        session.Text = "three words";

        // A stale binding can offer an index past the text; the line and
        // column are computed from the text, so they cannot leave it.
        session.CaretIndex = 99;

        Assert.Equal(1, session.CaretLine);
        Assert.Equal(12, session.CaretColumn);
    }

    [Fact]
    public void An_Empty_Document_Has_Its_Caret_On_Line_One() {
        var session = CreateSession();

        Assert.Equal(0, session.CaretIndex);
        Assert.Equal(1, session.CaretLine);
        Assert.Equal(1, session.CaretColumn);
    }
}
