using Shiranuix.Models;
using Shiranuix.Services.Pdf;

namespace Shiranuix.Tests.Services;

/// <summary>
/// Tests for <see cref="SheetLayout"/>: which words land on which row, where
/// that row begins, and which page the rows fall on.
/// </summary>
/// <remarks>
/// The measure is a stub rather than a typeface's, so every fact is exact:
/// each mark stands ten units wide, and the room is built out of whole
/// marks. What the layout is, is its decisions about words and rooms —
/// the widths themselves divide by ten all day.
/// </remarks>
public sealed class SheetLayoutTests {
    /// <summary>Every mark measures ten, whatever it is.</summary>
    private static double TenPerMark(string text) => text.Length * 10.0;

    private const double Sheet = 100.0;

    private static SheetPlan Plan(string text, TextAlign align = TextAlign.Left, double width = Sheet) =>
        SheetLayout.Plan(text,
            TenPerMark,
            width,
            height: 1_000.0,
            lineHeight: 20.0,
            align);

    [Fact]
    public void A_Line_Too_Wide_For_The_Sheet_Wraps_At_A_Word() {
        // Each word is forty wide, the pair is ninety, and the three of
        // them with the spaces would be a hundred and forty.
        var plan = Plan("aaaa bbbb cccc");
        var rows = plan.Pages.Single().Lines;

        Assert.Equal(["aaaa bbbb", "cccc"], rows.Select(row => row.Text).ToArray());
    }

    [Fact]
    public void A_Word_Wider_Than_Everything_Is_Cut_Where_The_Room_Ends() {
        var plan = Plan("aabbc", width: 30.0);
        var rows = plan.Pages.Single().Lines;

        // Five marks against room for three: the whole-words rule makes
        // room for nobody, so the row fills as far as it can and the rest
        // is honored on the next row.
        Assert.Equal(["aab", "bc"], rows.Select(row => row.Text).ToArray());
    }

    [Fact]
    public void A_Space_That_Refuses_To_Give_Way_Still_Gives_Way() {
        // The air a writer's tool slipped in - the space that says the words
        // beside it should never part - is still air between words when a
        // plain row is set: two words are two words, not one, however tight
        // the tool that wrote them held them.
        var plan = Plan("aa\u00A0bb\u00A0cc", width: 30.0);

        var rows = plan.Pages.Single().Lines.Select(row => row.Text).ToArray();
        Assert.Equal(["aa", "bb", "cc"], rows);
        Assert.DoesNotContain(rows, row => row.Contains('\u00A0'));
    }

    [Fact]
    public void Marks_Nobody_Can_See_Are_Never_Written() {
        // The zero-width letters a paste can smuggle in - the joins, the
        // hidden hyphen, the order-marks - name no mark a row can carry:
        // the writing reads as the words a human wrote, without them.
        var plan = Plan("a\u200Bb\u2060c\uFEFFd\u00ADe");

        var row = Assert.Single(plan.Pages.Single().Lines);
        Assert.Equal("abcde", row.Text);
    }

    [Fact]
    public void The_Last_Piece_Of_A_Cut_Word_Waits_For_The_Word_After_It() {
        var plan = Plan("aabbc dd", width: 30.0);
        var rows = plan.Pages.Single().Lines;

        // The remnant "bc" stays in hand rather than a row of its own: the
        // next word could have leaned against it if the two fit. They do
        // not, and the sheet goes out with the remnant alone.
        Assert.Equal(["aab", "bc", "dd"], rows.Select(row => row.Text).ToArray());
    }

    [Fact]
    public void A_Blank_Line_Keeps_Its_Room_Without_Anything_Drawn_In_It() {
        var plan = Plan("first\n\nthird");
        var rows = plan.Pages.Single().Lines;

        // The blank row is kept where it was written, holding nothing: the
        // page keeps its air rather than closing the gap.
        Assert.Equal(["first", "", "third"], rows.Select(row => row.Text).ToArray());
    }

    [Fact]
    public void An_Empty_Document_Is_Still_One_Page() {
        // Nothing written is not nothing printed: a writer who asks for
        // paper of an empty page gets a page, which is what it holds.
        var plan = Plan("");

        var page = Assert.Single(plan.Pages);
        var row = Assert.Single(page.Lines);
        Assert.Equal(string.Empty, row.Text);
    }

    [Fact]
    public void A_Tab_In_A_Line_Stands_Four_Spaces() {
        var plan = Plan("ab\tcd");

        var row = Assert.Single(plan.Pages.Single().Lines);
        Assert.Equal("ab    cd", row.Text);
    }

    [Fact]
    public void Spaces_That_Lead_A_Line_Travel_With_Its_First_Word() {
        // Indentation is the writer's own: a poem's short third line keeps
        // its air instead of starting flush at the margin.
        var plan = Plan("  short");
        var row = Assert.Single(plan.Pages.Single().Lines);

        Assert.Equal("  short", row.Text);
    }

    [Fact]
    public void The_Way_The_Lines_Are_Set_Names_Where_They_Begin() {
        // Left prose starts at the margin and nowhere else; the other two
        // are measured against the room, and never past it.
        Assert.Equal(0.0, Plan("aaaa", TextAlign.Left).Pages.Single().Lines.Single().Left);
        Assert.Equal(30.0, Plan("aaaa", TextAlign.Center, width: 100.0).Pages.Single().Lines.Single().Left);
        Assert.Equal(60.0, Plan("aaaa", TextAlign.Right, width: 100.0).Pages.Single().Lines.Single().Left);
    }

    [Fact]
    public void A_Sheet_Carries_As_Many_Rows_As_Its_Height_Holds_And_Spills_The_Rest() {
        // Room down the page for two rows at a time; five rows to set. The
        // spill is not lost: it takes the pages after.
        var plan = SheetLayout.Plan("a\nb\nc\nd\ne",
            TenPerMark,
            width: 100.0,
            height: 30.0,
            lineHeight: 12.0,
            TextAlign.Left);

        Assert.Equal(3, plan.Pages.Count);
        Assert.Equal(["a", "b"], plan.Pages[0].Lines.Select(row => row.Text).ToArray());
        Assert.Equal(["c", "d"], plan.Pages[1].Lines.Select(row => row.Text).ToArray());
        Assert.Equal(["e"], plan.Pages[2].Lines.Select(row => row.Text).ToArray());
    }
}
