using Shiranuix.Tests.Fakes;
using Shiranuix.ViewModels;

namespace Shiranuix.Tests.ViewModels;

/// <summary>
/// Tests for the reading state of a PDF: what it knows about the paper, where
/// its page pointer can go, and what a page ask reaches.
/// </summary>
public sealed class PdfViewerStateTests {
    [Fact]
    public void A_Paper_Knows_Its_Own_Name_Only() {
        var paper = new FakePdfPages(3);
        var state = new PdfViewerState("somewhere/book.pdf", paper);

        // The window's title carries the file's name, not the road to it:
        // the writer knows where their folder is.
        Assert.Equal("book.pdf", state.Title);
        Assert.Equal(3, state.Count);
        Assert.Equal(0, state.Index);
        Assert.Equal("1 / 3", state.PageMark);
    }

    [Fact]
    public void A_Paper_Turned_Past_Its_Last_Page_Stays_There() {
        var state = new PdfViewerState("book.pdf", new FakePdfPages(3));

        state.Next();
        state.Next();
        state.Next();
        state.Next();
        state.Next();

        Assert.Equal(2, state.Index);
        Assert.Equal("3 / 3", state.PageMark);
    }

    [Fact]
    public void A_Paper_Turned_Back_Past_Its_First_Page_Stays_There() {
        var state = new PdfViewerState("book.pdf", new FakePdfPages(3));

        state.Previous();

        Assert.Equal(0, state.Index);
        Assert.Equal("1 / 3", state.PageMark);
    }

    [Fact]
    public void A_Turn_Is_Announced_By_Its_Page_Mark() {
        var state = new PdfViewerState("book.pdf", new FakePdfPages(3));
        var said = new List<string?>();
        state.PropertyChanged += (_, e) => said.Add(e.PropertyName);

        state.Next();

        // The view binds the mark; the mark has to say it moved.
        Assert.Contains(nameof(PdfViewerState.PageMark), said);
        Assert.Contains(nameof(PdfViewerState.Index), said);
    }

    [Fact]
    public void A_Page_Asked_Of_The_State_Is_The_Paper_Page_At_That_Size() {
        var paper = new FakePdfPages(3);
        var state = new PdfViewerState("book.pdf", paper);

        state.Draw(1, 900);

        // The render is the paper's own: second page, at the width asked.
        var ask = Assert.Single(paper.Draws);
        Assert.Equal(1, ask.Index);
        Assert.Equal(900, ask.Width);
    }

    [Fact]
    public void A_Page_Off_The_Paper_Is_None_At_All() {
        var paper = new FakePdfPages(3);
        var state = new PdfViewerState("book.pdf", paper);

        Assert.Null(state.Draw(3, 900));
        Assert.Null(state.Draw(-1, 900));

        // The paper never saw an ask it could not answer.
        Assert.Empty(paper.Draws);
    }
}
