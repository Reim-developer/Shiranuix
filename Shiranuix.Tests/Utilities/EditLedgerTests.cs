using Shiranuix.Models;
using Shiranuix.Utilities;

namespace Shiranuix.Tests.Utilities;

/// <summary>
/// Tests for <see cref="EditLedger"/>, the moves against the file behind the
/// window.
/// </summary>
/// <remarks>
/// Each case drives the ledger with plain strings — no window, no file, no
/// binding — because what is worth checking here is the arithmetic: where a
/// move lands in the file once earlier moves have shifted the text, which
/// spans a new move swallows, and when a move cancels itself. The list a
/// test ends with is the list a save would be written from, so a test that
/// assembles the file from <see cref="EditLedger.Build"/> checks both the
/// shape of the moves and their effect on the disk.
/// </remarks>
public sealed class EditLedgerTests {
    [Fact]
    public void A_Note_About_Unchanged_Text_Stands_Against_Nothing() {
        var ledger = Window(0, "same");

        ledger.Note("same", "same");

        Assert.True(ledger.IsEmpty);
        Assert.Empty(ledger.Build());
    }

    [Fact]
    public void An_Insert_Falls_Where_It_Happened() {
        var ledger = Window(0, "Saved content.");

        ledger.Note("Saved content.", "Saved content.!");

        Assert.Equal([new TextReplacement(14, 0, "!")], ledger.Build());
    }

    [Fact]
    public void A_Replace_Carries_What_It_Took() {
        var ledger = Window(0, "aaaa");

        ledger.Note("aaaa", "aXa");

        // The move names the file's own "aa" and the "X" that took its
        // place — not the text around it, which the writer copies itself.
        Assert.Equal([new TextReplacement(1, 2, "X")], ledger.Build());
    }

    [Fact]
    public void A_Later_Move_Counts_From_The_File_Not_From_The_Text() {
        // The text is shorter than the file after the first move, so a
        // position at the end of the text is one further along in the file.
        var ledger = Window(0, "aaaa");
        ledger.Note("aaaa", "aXa");

        ledger.Note("aXa", "aXab");

        // The text is shorter than the file where the first move shrank it,
        // so the move at the end of the text lands past the shrunken span —
        // and the file's character between them is copied, not claimed.
        Assert.Equal(
            [new TextReplacement(1, 2, "X"), new TextReplacement(4, 0, "b")],
            ledger.Build());
        Assert.Equal("aXab", Assemble("aaaa", ledger));
    }

    [Fact]
    public void An_Edit_That_Reaches_An_Earlier_Move_Swallows_It() {
        var ledger = Window(0, "aaaa");
        ledger.Note("aaaa", "aXa");

        ledger.Note("aXa", "zz");

        // One move over the whole stretch stands, not the swallowed one
        // beside it: half an entry cannot survive being reached into.
        Assert.Equal([new TextReplacement(0, 4, "zz")], ledger.Build());
    }

    [Fact]
    public void Typing_A_Move_Back_To_The_File_s_Words_Cancels_It() {
        var ledger = Window(0, "aaaa");
        ledger.Note("aaaa", "aXa");

        ledger.Note("aXa", "aaaa");

        // The file and the document say the same thing again, however many
        // moves it took to get there.
        Assert.True(ledger.IsEmpty);
    }

    [Fact]
    public void A_Run_Of_Adjacent_Moves_Stands_As_One() {
        var ledger = Window(0, "ab");

        ledger.Note("ab", "aXb");
        ledger.Note("aXb", "aXYb");

        Assert.Single(ledger.Build());
        Assert.Equal("aXYb", Assemble("ab", ledger));
    }

    [Fact]
    public void Moves_With_File_Between_Them_Stay_Apart() {
        var ledger = Window(0, "hello world");

        ledger.Note("hello world", "HELLO world");
        ledger.Note("HELLO world", "HELLO brave world");

        // "hello" and the insertion five characters on are two moves with
        // one untouched space between them, and the space stays untouched.
        Assert.Equal(
            [new TextReplacement(0, 5, "HELLO"), new TextReplacement(6, 0, "brave ")],
            ledger.Build());
        Assert.Equal("HELLO brave world", Assemble("hello world", ledger));
    }

    [Fact]
    public void Cancelling_A_Later_Move_Leaves_An_Earlier_One() {
        var ledger = Window(0, "hello world");
        ledger.Note("hello world", "HELLO world");
        ledger.Note("HELLO world", "HELLO brave world");

        ledger.Note("HELLO brave world", "HELLO world");

        Assert.Equal([new TextReplacement(0, 5, "HELLO")], ledger.Build());
    }

    [Fact]
    public void An_Untitled_Document_Keeps_Every_Move_At_Its_One_Point() {
        // A blank page has no file at all, so every move hangs at position
        // zero and folds together there.
        var ledger = Window(0, string.Empty);

        ledger.Note(string.Empty, "draft");
        ledger.Note("draft", "draft!");

        Assert.Equal([new TextReplacement(0, 0, "draft!")], ledger.Build());
    }

    [Fact]
    public void Reverting_An_Untitled_Document_To_Blank_Clears_The_Ledger() {
        var ledger = Window(0, string.Empty);
        ledger.Note(string.Empty, "draft");

        ledger.Note("draft", string.Empty);

        Assert.True(ledger.IsEmpty);
    }

    [Fact]
    public void A_Window_Later_In_The_File_Counts_From_Its_Own_Start() {
        var ledger = Window(100, "abc");

        ledger.Note("abc", "aXc");

        Assert.Equal([new TextReplacement(101, 1, "X")], ledger.Build());
    }

    [Fact]
    public void Moving_The_Window_Keeps_The_Moves_Beyond_It() {
        var ledger = Window(100, "abc");
        ledger.Note("abc", "aXc");

        ledger.ResetWindow(0, "0123456789abc");
        ledger.Note("0123456789abc", "0123456789Xabc");

        // The move a thousand characters away stands exactly where it was
        // made; the new window's own move counts from its own start.
        Assert.Equal(
            [new TextReplacement(10, 0, "X"), new TextReplacement(101, 1, "X")],
            ledger.Build());
    }

    [Fact]
    public void Clear_Forgets_Every_Move() {
        var ledger = Window(0, "aaaa");
        ledger.Note("aaaa", "aXa");

        ledger.Clear();

        Assert.True(ledger.IsEmpty);
    }

    // ===== The window's views =================================================

    [Fact]
    public void The_Window_S_Bounds_Are_Its_Stretch_Of_File_Not_Its_Text() {
        // Fixed while the window stands: moves change what the window says,
        // not the stretch it says it over — which is what lets the history
        // tell windows apart by two numbers.
        var ledger = Window(100, "abc");

        ledger.Note("abc", "aXc");

        Assert.Equal(100, ledger.WindowFrom);
        Assert.Equal(103, ledger.WindowTo);
        Assert.Equal("abc", ledger.WindowOriginal);
    }

    [Fact]
    public void Materialize_Lays_The_Moves_Over_The_File_S_Own_Text() {
        // A window slid back over an edited stretch is read from the file,
        // which has none of the moves in it yet — they go back on top here,
        // the same way saving will lay them over the file one day.
        var ledger = Window(0, "one two three");
        ledger.Note("one two three", "one 2 three");
        ledger.Note("one 2 three", "one 2 THREE");

        Assert.Equal("one 2 THREE", ledger.Materialize());
        Assert.Equal(
            Assemble("one two three", ledger),
            ledger.Materialize());
    }

    [Fact]
    public void Materialize_Leaves_The_Moves_Beyond_The_Window_Out() {
        var ledger = Window(0, "abcd");
        ledger.Note("abcd", "aXcd");

        ledger.ResetWindow(10, "zz");

        Assert.Equal("zz", ledger.Materialize());
    }

    [Fact]
    public void Materialize_With_Nothing_Moved_Says_What_The_File_Says() {
        var ledger = Window(0, "saved text");

        Assert.Equal("saved text", ledger.Materialize());
    }

    [Fact]
    public void SpansOutside_Lists_The_Moves_The_Window_Cannot_See() {
        // The window shows one stretch, but the whole file's counts have to
        // carry every move: the ones outside are added over the top.
        var ledger = Window(0, "abcd");
        ledger.Note("abcd", "aXcd");

        ledger.ResetWindow(100, "zz");
        ledger.Note("zz", "z!");

        Assert.Equal([new EditSpan(1, "b", "X")], ledger.SpansOutside());
        Assert.Equal(
            [new EditSpan(1, "b", "X"), new EditSpan(101, "z", "!")],
            ledger.Spans());
    }

    [Fact]
    public void SpansBefore_Lists_The_Moves_A_Position_Stands_After() {
        var ledger = Window(0, "abcdef");
        ledger.Note("abcdef", "XYcdef"); // [0, 2) -> "XY"
        ledger.Note("XYcdef", "XYcd!"); // [5, 6) -> "!"

        // A move ending exactly at the position still lies before it — the
        // window's own seam rule — so both stand before 6, while only the
        // first stands before 5.
        Assert.Equal(
            [new EditSpan(0, "ab", "XY"), new EditSpan(4, "ef", "!")],
            ledger.SpansBefore(6));
        Assert.Equal([new EditSpan(0, "ab", "XY")], ledger.SpansBefore(5));
    }

    [Fact]
    public void Cuts_Finds_An_Edge_That_Would_Half_A_Move() {
        var ledger = Window(0, "abcdef");
        ledger.Note("abcdef", "abXYef"); // [2, 4) -> "XY"

        Assert.True(ledger.Cuts(3));
        Assert.False(ledger.Cuts(2));
        Assert.False(ledger.Cuts(4));
    }

    [Fact]
    public void CutAt_Comes_Back_With_The_Move_The_Edge_Would_Half() {
        // The whole move, not just the yes: the edge that has to move
        // needs both ends to know which way to step.
        var ledger = Window(0, "abcdef");
        ledger.Note("abcdef", "abXYef"); // [2, 4) -> "XY"

        Assert.Equal(new EditSpan(2, "cd", "XY"), ledger.CutAt(3));
        Assert.Null(ledger.CutAt(2));
        Assert.Null(ledger.CutAt(4));
    }

    [Fact]
    public void RebaseToSaved_Slides_The_Window_By_What_Came_Before_It() {
        // Saving collapses the moves into the file: the ones ahead of the
        // window slide every later position — the window included — and the
        // ones inside it are already behind its own text.
        var ledger = Window(0, "abcd");
        ledger.Note("abcd", "abcd!");

        ledger.ResetWindow(6, "gh");
        ledger.Note("gh", "gH");

        var start = ledger.RebaseToSaved("gH");

        Assert.Equal(7, start);
        Assert.Equal(7, ledger.WindowFrom);
        Assert.Equal(9, ledger.WindowTo);
        Assert.Equal("gH", ledger.WindowOriginal);
        Assert.True(ledger.IsEmpty);
    }

    [Fact]
    public void A_Note_After_A_Rebase_Counts_From_The_New_Window() {
        var ledger = Window(0, "abcd");
        ledger.Note("abcd", "abcd!");

        ledger.ResetWindow(6, "gh");
        ledger.RebaseToSaved("gh");

        ledger.Note("gh", "gH");

        Assert.Equal([new TextReplacement(8, 1, "H")], ledger.Build());
    }

    [Fact]
    public void Window_Stats_Stand_With_The_Window_S_Own_Text() {
        var ledger = Window(0, "one\ntwo");

        // The counts are measured when the window arrives...
        Assert.Equal(TextStatistics.Measure("one\ntwo"), ledger.WindowStats);

        // ...a move inside the window changes the document's text but not
        // what the file said over the stretch the window was read from...
        ledger.Note("one\ntwo", "one\nTWO");
        Assert.Equal(TextStatistics.Measure("one\ntwo"), ledger.WindowStats);

        // ...and a save takes what was written as the new window text.
        ledger.RebaseToSaved("one\nTWO");
        Assert.Equal(TextStatistics.Measure("one\nTWO"), ledger.WindowStats);
    }

    /// <summary>
    /// A ledger pointed at a window's stretch of file.
    /// </summary>
    /// <param name="start">Where the window's text begins in the file.</param>
    /// <param name="original">The file's text over the window.</param>
    /// <returns>A ledger ready to note moves against.</returns>
    private static EditLedger Window(int start, string original) {
        var ledger = new EditLedger();
        ledger.ResetWindow(start, original);
        return ledger;
    }

    /// <summary>
    /// The file the writer would produce: the original with every move laid
    /// over the span it names.
    /// </summary>
    /// <param name="original">The file's text before any move.</param>
    /// <param name="ledger">The moves to lay over it.</param>
    /// <returns>The assembled file.</returns>
    private static string Assemble(string original, EditLedger ledger) {
        var text = new System.Text.StringBuilder();
        var cursor = 0;

        foreach (var replacement in ledger.Build()) {
            text.Append(original, cursor, replacement.Start - cursor);
            text.Append(replacement.Text);
            cursor = replacement.Start + replacement.Length;
        }

        return text.ToString() + original[cursor..];
    }
}
