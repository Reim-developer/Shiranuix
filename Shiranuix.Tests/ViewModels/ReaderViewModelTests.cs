using Shiranuix.Models;
using Shiranuix.Tests.Fakes;
using Shiranuix.ViewModels;

namespace Shiranuix.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="ReaderViewModel"/>, the reading-position logic.
/// </summary>
public sealed class ReaderViewModelTests
{
    private readonly FakeImageLoader _loader = new();

    [Fact]
    public void Open_Starts_At_Page_One()
    {
        var viewModel = new ReaderViewModel(_loader);

        viewModel.Open(MakeChapter(pageCount: 3));

        Assert.Equal(1, viewModel.CurrentPageIndex);
        Assert.Equal(3, viewModel.PageCount);
        Assert.True(viewModel.CanGoNext);
        Assert.False(viewModel.CanGoPrevious);
        Assert.Equal("1 / 3", viewModel.PageLabel);
    }

    [Fact]
    public void NextCommand_Advances_And_Updates_Flags()
    {
        var viewModel = new ReaderViewModel(_loader);
        viewModel.Open(MakeChapter(pageCount: 3));

        viewModel.NextCommand.Execute(null);

        Assert.Equal(2, viewModel.CurrentPageIndex);
        Assert.True(viewModel.CanGoNext);
        Assert.True(viewModel.CanGoPrevious);
    }

    [Fact]
    public void PreviousCommand_Is_Disabled_At_First_Page()
    {
        var viewModel = new ReaderViewModel(_loader);
        viewModel.Open(MakeChapter(pageCount: 3));

        // CanExecute is false on the first page, so it must not move.
        Assert.False(viewModel.PreviousCommand.CanExecute(null));
        viewModel.PreviousCommand.Execute(null);

        Assert.Equal(1, viewModel.CurrentPageIndex);
    }

    [Fact]
    public void NextCommand_Is_Disabled_On_Last_Page()
    {
        var viewModel = new ReaderViewModel(_loader);
        viewModel.Open(MakeChapter(pageCount: 2));
        viewModel.GoToPageCommand.Execute(2);

        Assert.False(viewModel.NextCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(-5, 1)]   // clamp low
    [InlineData(0, 1)]
    [InlineData(2, 2)]
    [InlineData(99, 3)]   // clamp high
    public void GoToPage_Clamps_Into_Range(int requested, int expected)
    {
        var viewModel = new ReaderViewModel(_loader);
        viewModel.Open(MakeChapter(pageCount: 3));

        viewModel.GoToPageCommand.Execute(requested);

        Assert.Equal(expected, viewModel.CurrentPageIndex);
    }

    [Fact]
    public void BackCommand_Raises_BackRequested()
    {
        var viewModel = new ReaderViewModel(_loader);
        viewModel.Open(MakeChapter(pageCount: 3));
        bool raised = false;
        viewModel.BackRequested += (_, _) => raised = true;

        viewModel.BackCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void Loads_Image_For_The_Current_Page()
    {
        var viewModel = new ReaderViewModel(_loader);
        viewModel.Open(MakeChapter(pageCount: 3));

        Assert.Equal("page1.png", _loader.LoadedPaths.Last());
    }

    [Fact]
    public void Navigation_Loads_Each_New_Page()
    {
        var viewModel = new ReaderViewModel(_loader);
        viewModel.Open(MakeChapter(pageCount: 3));

        viewModel.NextCommand.Execute(null);
        viewModel.NextCommand.Execute(null);

        Assert.Equal(new[] { "page1.png", "page2.png", "page3.png" }, _loader.LoadedPaths);
    }

    /// <summary>Builds a chapter with the requested number of dummy pages.</summary>
    private static Chapter MakeChapter(int pageCount)
    {
        var pages = Enumerable
            .Range(1, pageCount)
            .Select(i => new MangaPage($"page{i}.png", i))
            .ToList();
        return new Chapter("folder", "Chapter", pages);
    }
}
