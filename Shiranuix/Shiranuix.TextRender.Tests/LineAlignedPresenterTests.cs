using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Shiranuix.TextRender;

namespace Shiranuix.TextRender.Tests;

/// <summary>
/// Tests for the page that stands each of its lines at its own edge: what
/// the letters do, and — through the framework's own machinery, which is
/// the whole point — where the caret and a click land beside them.
/// </summary>
/// <remarks>
/// Every case lays text out the way the editor's page does, through the
/// presenter, at a width the assertions can name. The measurements are
/// real: the headless platform shapes the same letters with the same font
/// the screen would, so a line measured in from the edge here is a line
/// in from the edge there.
/// </remarks>
public sealed class LineAlignedPresenterTests {
    private const double Width = 400;
    private const double Height = 600;

    [AvaloniaFact]
    public void The_Page_Stands_Its_Lines_Left_When_Nobody_Asked_For_More() {
        // With no answer to give, the page is the framework's own, and the
        // framework stands lines flush left.
        var layout = LayOut("aa\nbb", provider: null);

        Assert.True(FirstGlyphX(layout.TextLines[0]) < 1);
        Assert.True(FirstGlyphX(layout.TextLines[1]) < 1);
    }

    [AvaloniaFact]
    public void A_Line_Can_Stand_Centred_While_The_One_Above_Stays_Left() {
        // The page the writer sees: the heading centred, the line under it
        // still at the margin it was.
        var layout = LayOut("aa\nbb", line => line == 1 ? TextAlignment.Center : TextAlignment.Left);

        Assert.True(FirstGlyphX(layout.TextLines[0]) < 1);
        Assert.True(FirstGlyphX(layout.TextLines[1]) > Width / 4);
    }

    [AvaloniaFact]
    public void A_Line_Can_Stand_Flush_Right() {
        var layout = LayOut("aa\nbb", line => line == 1 ? TextAlignment.Right : TextAlignment.Left);

        var line1 = layout.TextLines[1];
        var right = line1.GetTextBounds(line1.FirstTextSourceIndex, 2)
            .Max(bounds => bounds.Rectangle.Right);

        Assert.True(right > Width - 30, $"the line ends at {right}, near the page's {Width}");
    }

    [AvaloniaFact]
    public void Every_Visual_Line_Of_A_Wrapped_Line_Stands_Together() {
        // A long line that wraps onto a second row is still one line of the
        // writing, and the writing's edge stands each row the way standing
        // that one line centred would: the full row stays where its own
        // width leaves it, and the short row at the end settles in toward
        // the middle — a centred paragraph's rows move together.
        var layout = LayOut(
            "một hai ba bốn năm sáu bảy tám chín mười một hai ba bốn năm sáu bảy tám chín mười",
            _ => TextAlignment.Center,
            TextWrapping.Wrap);

        var row0 = layout.TextLines[0];
        var row1 = layout.TextLines[1];

        var inWhere0 = FirstGlyphX(row0);
        var inWhere1 = FirstGlyphX(row1);
        var centred0 = (Width - row0.Width) / 2;
        var centred1 = (Width - row1.Width) / 2;

        // Off the mark the way standing the line left would show it, or
        // standing it half-way: the row is either at the margin or at its
        // own centre, and there is no in-between it could be hiding in.
        if (Math.Abs(inWhere0 - centred0) > 6 || Math.Abs(inWhere1 - centred1) > 6) {
            throw new Exception(
                $"DIAG row0 in={inWhere0} centred={centred0} (w={row0.Width}); row1 in={inWhere1} centred={centred1} (w={row1.Width})");
        }
    }

    [AvaloniaFact]
    public void The_Caret_Lands_Where_The_Letters_Stand() {
        // Nobody positions a caret by hand: the writer clicks, and where
        // the click lands is answered by the same layout the letters came
        // out of. A caret asked for on a centred line must stand in from
        // the margin with the letters, not back at the edge they left.
        var layout = LayOut("aa\nbb", line => line == 1 ? TextAlignment.Center : TextAlignment.Left);

        var marginCaret = layout.HitTestTextPosition(0);
        var centredCaret = layout.HitTestTextPosition(3);

        Assert.True(marginCaret.X < 1);
        Assert.True(centredCaret.X > Width / 4, $"the caret stands at {centredCaret.X}");
    }

    [AvaloniaFact]
    public void Changing_What_The_Lines_Say_Stands_Them_Up_Again() {
        // The document sets its lines a different way — a chip pressed —
        // and the page answers without being rebuilt.
        var presenter = new AlignedTextPresenter {
            Text = "aa\nbb",
            FontSize = 20,
            LineAlignmentProvider = line => line == 1 ? TextAlignment.Center : TextAlignment.Left,
        };

        LayOut(presenter);
        var centred = FirstGlyphX(presenter.TextLayout.TextLines[1]);

        presenter.LineAlignmentProvider = line => line == 1 ? TextAlignment.Right : TextAlignment.Left;
        LayOut(presenter);
        var flushRight = FirstGlyphX(presenter.TextLayout.TextLines[1]);

        Assert.True(centred > Width / 4);
        Assert.True(flushRight > centred, $"right ({flushRight}) stands past centre ({centred})");
    }

    [AvaloniaFact]
    public void A_Layout_That_Never_Asked_For_A_Per_Line_Edge_Is_The_Frameworks_Own() {
        // The formatter stands in for the whole app, so the whole app's
        // ordinary text had better come out of it unchanged: a label that
        // asked to be centred is still centred.
        var layout = new TextLayout(
            "bb", Typeface.Default, 20, textAlignment: TextAlignment.Center, maxWidth: Width);

        Assert.True(FirstGlyphX(layout.TextLines[0]) > 0);
    }

    /// <summary>
    /// Lays text out through the page, the way the editor's box does:
    /// measured against the width, then arranged against it.
    /// </summary>
    private static TextLayout LayOut(
        string text,
        Func<int, TextAlignment>? provider,
        TextWrapping wrapping = TextWrapping.NoWrap) {
        var presenter = new AlignedTextPresenter {
            Text = text,
            FontSize = 20,
            TextWrapping = wrapping,
            LineAlignmentProvider = provider,
        };

        LayOut(presenter);

        return presenter.TextLayout;
    }

    /// <summary>
    /// Stands the page against the test's width and height, which is when
    /// it builds its layout.
    /// </summary>
    private static void LayOut(AlignedTextPresenter presenter) {
        presenter.Measure(new Size(Width, Height));
        presenter.Arrange(new Rect(0, 0, Width, Height));
    }

    /// <summary>
    /// Where a line's first letter begins, which is the line's edge: the
    /// number every case reads. A letter can come back as more than one
    /// piece, and its edge is the left of them all.
    /// </summary>
    private static double FirstGlyphX(TextLine line) =>
        line.GetTextBounds(line.FirstTextSourceIndex, 1)
            .Min(bounds => bounds.Rectangle.X);
}
