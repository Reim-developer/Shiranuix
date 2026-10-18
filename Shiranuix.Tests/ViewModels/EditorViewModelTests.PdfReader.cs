using System.IO;
using Shiranuix.Tests.Fakes;
using Shiranuix.ViewModels;

namespace Shiranuix.Tests.ViewModels;

/// <summary>
/// Tests for how a PDF is met: the tree, the file picker and the command line
/// all hand a paper to the reader, and none of them turn the document into
/// something it is not.
/// </summary>
public sealed partial class EditorViewModelTests {
    private readonly FakePdfViewerService _readers = new();

    [Fact]
    public void A_Pdf_At_The_Command_Line_Goes_To_The_Reader_And_Not_The_Writer() {
        var viewModel = CreateViewModel();

        // Nothing is registered on the fake disk for this path: a route that
        // tries to read it as text would fail, and the fact would hear it.
        viewModel.OpenFromCommandLine("story.pdf");

        Assert.Equal(("story.pdf"), Assert.Single(_readers.Opened));
        Assert.False(viewModel.HasDocument);
        Assert.Equal("Opened story.pdf to read.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task A_Pdf_At_The_Tree_Goes_To_The_Reader() {
        var viewModel = CreateViewModel();

        var outcome = await viewModel.OpenPathAsync("story.pdf");

        Assert.True(outcome.Succeeded);
        Assert.Equal("story.pdf", Assert.Single(_readers.Opened));
        Assert.False(viewModel.HasDocument);
        Assert.Equal("Opened story.pdf to read.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task A_Reader_Never_Takes_The_Writing_Down() {
        var viewModel = CreateViewModel(withExistingFile: true);
        viewModel.Document.Text = "Đang viết dở";
        _readers.Answer = DocumentOutcome.Done;

        await viewModel.OpenPathAsync("story.pdf");

        // The reader shows a paper beside the writing; nothing is replaced,
        // so the discard question is never asked and the writing stands.
        Assert.Empty(_confirm.AskedAbout);
        Assert.Equal("Đang viết dở", viewModel.Document.Text);
        Assert.Equal(ExistingPath, viewModel.Document.Path);
        Assert.True(viewModel.Document.IsDirty);
    }

    [Fact]
    public async Task A_Pdf_Picked_To_Open_Reads_While_The_Writing_Stands() {
        var viewModel = CreateViewModel(withExistingFile: true);
        viewModel.Document.Text = "Đang viết dở";
        _dialogs.OpenResult = "somewhere/story.pdf";

        await viewModel.OpenCommand.ExecuteAsync(null);

        // The reader was reached without a discard prompt: opening a paper
        // for reading is not a change to what is being written.
        Assert.Equal(1, _dialogs.OpenAsks);
        Assert.Empty(_confirm.AskedAbout);
        Assert.Equal("somewhere/story.pdf", Assert.Single(_readers.Opened));
        Assert.Equal("Đang viết dở", viewModel.Document.Text);
    }

    [Fact]
    public async Task The_Picker_Goes_First_And_The_Discard_Follows() {
        var viewModel = CreateViewModel(withExistingFile: true);
        viewModel.Document.Text = "Đang viết dở";
        _dialogs.OpenResult = "other.txt";
        _files.Files["other.txt"] = "Other content";
        _confirm.Reply = false;

        await viewModel.OpenCommand.ExecuteAsync(null);

        // The file was asked for before anything about the writing was. A
        // refused discard leaves the file question answered but the page
        // untouched, instead of asking about a file that was never named.
        Assert.Equal(1, _dialogs.OpenAsks);
        Assert.Single(_confirm.AskedAbout);
        Assert.Equal("Đang viết dở", viewModel.Document.Text);
        Assert.Equal(ExistingPath, viewModel.Document.Path);
    }

    [Fact]
    public void A_Paper_The_Reader_Cannot_Show_Says_What_Failed() {
        var viewModel = CreateViewModel();
        _readers.Answer = DocumentOutcome.Failed("broken paper");

        viewModel.OpenFromCommandLine("story.pdf");

        Assert.Single(_readers.Opened);
        Assert.Equal("Could not open: broken paper", viewModel.StatusMessage);
        Assert.False(viewModel.HasDocument);
    }
}
