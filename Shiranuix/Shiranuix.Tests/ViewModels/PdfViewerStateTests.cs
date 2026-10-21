using Shiranuix.Tests.Fakes;
using Shiranuix.ViewModels;
using SkiaSharp;

namespace Shiranuix.Tests.ViewModels;

/// <summary>
/// Tests for the reading state of a PDF: what it knows about the paper, the
/// places its pages take, the sizes it can be looked at, and what a page ask
/// keeps or draws again.
/// </summary>
public sealed class PdfViewerStateTests {
    [Fact]
    public void A_Reading_Is_Titled_By_Its_File() {
        var state = new PdfViewerState("somewhere/book.pdf", new FakePdfPages(3));

        // The window's title carries the file's name, not the road to it:
        // the writer knows where their folder is.
        Assert.Equal("book.pdf", state.Title);
    }

    [Fact]
    public void A_Reading_Knows_How_Many_Pages_Its_Paper_Holds() {
        var state = new PdfViewerState("book.pdf", new FakePdfPages(4));

        Assert.Equal(4, state.Count);
    }

    [Fact]
    public void Every_Page_Stands_In_The_Shape_Its_Paper_Drew() {
        var paper = new FakePdfPages(new[] {
            new SKSize(600f, 300f),
            new SKSize(300f, 900f),
            FakePdfPages.A4,
        });
        var state = new PdfViewerState("book.pdf", paper);

        // A page's place in the gallery is measured by the shape the paper
        // drew the page in, page after page, each its own.
        Assert.Equal(300.0 / 600.0, state.PageShape(0), 6);
        Assert.Equal(900.0 / 300.0, state.PageShape(1), 6);
        Assert.Equal(842.0 / 595.0, state.PageShape(2), 6);
    }

    [Fact]
    public void A_Page_Asked_Of_The_Reading_Is_A_Picture_At_The_Width_Asked() {
        var paper = new FakePdfPages(3);
        var state = new PdfViewerState("book.pdf", paper);

        var picture = state.Draw(1, 900);

        // The render is the paper's own: the second page at the width asked,
        // and what comes back is a picture in bytes, owned by the asker.
        var ask = Assert.Single(paper.Draws);
        Assert.Equal(1, ask.Index);
        Assert.Equal(900, ask.Width);
        Assert.NotNull(picture);
        Assert.NotEmpty(picture);
    }

    [Fact]
    public void A_Page_While_It_Stands_Is_Drawn_Once() {
        var paper = new FakePdfPages(3);
        var state = new PdfViewerState("book.pdf", paper);

        var first = state.Draw(1, 900);
        var again = state.Draw(1, 900);

        // A page the reading still holds is the same picture standing again,
        // not a page drawn another time; nothing native travels out at all,
        // so a held page cannot be borrowed dead.
        var ask = Assert.Single(paper.Draws);
        Assert.Equal(1, ask.Index);
        Assert.Equal(900, ask.Width);
        Assert.Same(first, again);
    }

    [Fact]
    public void A_Wider_Ask_Is_Drawn_Again() {
        var paper = new FakePdfPages(3);
        var state = new PdfViewerState("book.pdf", paper);

        state.Draw(0, 800);
        state.Draw(0, 900);

        // The reading holds both: a page drawn at one width is not the page
        // asked at another.
        Assert.Equal(2, paper.Draws.Count);
        Assert.Equal((0, 800), paper.Draws[0]);
        Assert.Equal((0, 900), paper.Draws[1]);
    }

    [Fact]
    public void Pages_The_Reading_Let_Go_Are_Drawn_Again_When_They_Return() {
        var paper = new FakePdfPages(PdfViewerState.Hold + 2);
        var state = new PdfViewerState("book.pdf", paper);
        var first = state.Draw(0, 900);

        for (var place = 0; place <= PdfViewerState.Hold; place++) {
            state.Draw(place, 900);
        }

        var returned = state.Draw(0, 900);

        // The first page was let go so the others could stand; when it comes
        // back it is drawn again, not remembered.
        Assert.Equal(PdfViewerState.Hold + 2, paper.Draws.Count);
        Assert.NotSame(first, returned);
    }

    [Fact]
    public void A_Reading_Stands_On_The_Paper_First_Page() {
        var state = new PdfViewerState("book.pdf", new FakePdfPages(3));

        Assert.Equal(0, state.Standing);
    }

    [Fact]
    public void A_Step_Moves_The_Reading_From_Page_To_Page() {
        var state = new PdfViewerState("book.pdf", new FakePdfPages(5));

        state.Step(2);
        Assert.Equal(2, state.Standing);

        state.Step(1);
        Assert.Equal(3, state.Standing);

        state.Step(-2);
        Assert.Equal(1, state.Standing);
    }

    [Fact]
    public void A_Step_Never_Walks_Off_The_Paper() {
        var state = new PdfViewerState("book.pdf", new FakePdfPages(3));

        state.Step(999);
        Assert.Equal(2, state.Standing);

        state.Step(-999);
        Assert.Equal(0, state.Standing);
    }

    [Fact]
    public void A_Reading_Of_A_Single_Page_Stands_Where_It_Stood() {
        var state = new PdfViewerState("book.pdf", new FakePdfPages(1));

        state.Step(5);
        state.Step(-5);

        Assert.Equal(0, state.Standing);
    }

    [Fact]
    public void A_Reading_Looks_Its_Paper_At_The_Fit_To_Begin_With() {
        var state = new PdfViewerState("book.pdf", new FakePdfPages(3));

        // A paper opened cold is looked at at the size its glass offers on
        // its own — neither grown nor shrunk, with both ways still open,
        // since the fit stands among the sizes rather than at either end.
        Assert.Equal(1.0, state.Zoom, 6);
        Assert.True(state.CanZoomIn);
        Assert.True(state.CanZoomOut);
    }

    [Fact]
    public void A_Rung_Up_Looks_The_Paper_Larger_And_A_Rung_Down_Smaller() {
        var state = new PdfViewerState("book.pdf", new FakePdfPages(3));

        state.ZoomBy(1);
        Assert.Equal(1.25, state.Zoom, 6);

        state.ZoomBy(-2);
        Assert.Equal(0.75, state.Zoom, 6);

        state.ZoomBy(3);
        Assert.Equal(1.5, state.Zoom, 6);
    }

    [Fact]
    public void A_Rung_Never_Leaves_The_Sizes_The_Paper_Can_Be_Looked_At() {
        var state = new PdfViewerState("book.pdf", new FakePdfPages(3));

        state.ZoomBy(999);

        // Past the largest stands at the largest: there is no size above it
        // to ask for, and the step under it is still there.
        Assert.Equal(4.0, state.Zoom, 6);
        Assert.False(state.CanZoomIn);
        Assert.True(state.CanZoomOut);

        state.ZoomBy(-999);

        Assert.Equal(0.25, state.Zoom, 6);
        Assert.False(state.CanZoomOut);
        Assert.True(state.CanZoomIn);
    }

    [Fact]
    public void The_Fit_Puts_The_Reading_Back_On_The_Size_Its_Glass_Offers() {
        var state = new PdfViewerState("book.pdf", new FakePdfPages(3));

        state.ZoomBy(4);
        state.ZoomFit();

        Assert.Equal(1.0, state.Zoom, 6);
        Assert.True(state.CanZoomIn);
        Assert.True(state.CanZoomOut);
    }

    [Fact]
    public void A_Page_Off_The_Paper_Is_None_At_All() {
        var paper = new FakePdfPages(3);
        var state = new PdfViewerState("book.pdf", paper);

        Assert.Null(state.Draw(3, 900));
        Assert.Null(state.Draw(-1, 900));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.PageShape(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.PageShape(-1));

        // The paper never saw an ask it could not answer.
        Assert.Empty(paper.Draws);
    }
}
