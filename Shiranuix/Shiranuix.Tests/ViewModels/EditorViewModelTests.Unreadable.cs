using Shiranuix.Tests.Fakes;
using Shiranuix.ViewModels;

namespace Shiranuix.Tests.ViewModels;

/// <summary>
/// Tests for the one open that stops and tells the user why: a file whose bytes
/// are not the shape this app knows how to read.
/// </summary>
/// <remarks>
/// Every other failure — the file is not there, the folder is barred, the disk
/// is full — reaches the status bar and stops there. This one gets a window,
/// and that difference is what is checked here. It also has to keep the page
/// exactly as it was: a file that cannot be read has not been opened, and the
/// writing standing on the screen is still someone's work.
/// </remarks>
public sealed partial class EditorViewModelTests {
    private const string Broken = "The file does not open with the mark a .shiranuix file carries.";

    [Fact]
    public async Task A_File_That_Will_Not_Read_Stops_And_Says_Why_In_A_Window() {
        var dialogs = new FakeDialogService();
        var viewModel = CreateViewModel(withExistingFile: true, dialogs);
        viewModel.Document.Text = "Đang viết dở";
        _files.FailReadsWith = new InvalidDataException(Broken);
        _dialogs.OpenResult = "broken.shiranuix";

        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.Equal("Đang viết dở", viewModel.Document.Text);
        Assert.Equal(ExistingPath, viewModel.Document.Path);

        // The work standing on the page is still standing, and it is still
        // unsaved: nothing was opened, so nothing was discarded either.
        Assert.True(viewModel.Document.IsDirty);
        Assert.Contains("Could not read broken.shiranuix", viewModel.StatusMessage);

        var asked = Assert.Single(dialogs.Asked);
        Assert.Contains("cannot be read", asked.Title);
        Assert.Contains("broken.shiranuix", asked.Message);
        Assert.Equal(["Choose another file", "Close"], asked.Buttons);
    }

    [Fact]
    public async Task A_File_That_Will_Not_Read_Offers_Another_File() {
        var dialogs = new FakeDialogService { Result = true };
        var viewModel = CreateViewModel(withExistingFile: true, dialogs);
        viewModel.Document.Text = "Đang viết dở";

        // Only the first file is broken. A flow that opens a broken file and
        // is then offered a whole one needs the two to differ, or the second
        // pick is broken too and the window asks for a third.
        _files.BrokenFiles["broken.shiranuix"] = new InvalidDataException(Broken);
        _dialogs.OpenResults.Enqueue("broken.shiranuix");
        _dialogs.OpenResults.Enqueue("good.txt");
        _files.Files["good.txt"] = "Trang mới.";

        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.Equal(2, _dialogs.OpenAsks);
        Assert.Equal("Trang mới.", viewModel.Document.Text);
        Assert.Equal("good.txt", viewModel.Document.Path);
        Assert.Equal("Opened good.txt", viewModel.StatusMessage);
    }

    [Fact]
    public async Task A_File_That_Will_Not_Read_Says_Nothing_When_The_Window_Is_Shut() {
        // A window closed with nothing pressed is not consent to anything.
        var dialogs = new FakeDialogService { Result = null };
        var viewModel = CreateViewModel(withExistingFile: true, dialogs);
        viewModel.Document.Text = "Đang viết dở";
        _files.FailReadsWith = new InvalidDataException(Broken);
        _dialogs.OpenResult = "broken.shiranuix";

        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.Equal("Đang viết dở", viewModel.Document.Text);
        Assert.Equal(ExistingPath, viewModel.Document.Path);
        Assert.Equal(1, _dialogs.OpenAsks);
    }

    [Fact]
    public async Task A_File_That_Is_Simply_Absent_Gets_No_Window() {
        // An absent file is a fact about the machine, not about the file, and
        // no window will tell anyone anything they did not already know.
        var dialogs = new FakeDialogService();
        var viewModel = CreateViewModel(withExistingFile: true, dialogs);
        viewModel.Document.Text = "Đang viết dở";
        _files.FailReadsWith = new FileNotFoundException("There is nothing here called that.");
        _dialogs.OpenResult = "vanished.shiranuix";

        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.Empty(dialogs.Asked);
        Assert.Contains("Could not open: There is nothing here called that.", viewModel.StatusMessage);
        Assert.Equal("Đang viết dở", viewModel.Document.Text);
    }

    [Fact]
    public async Task An_Explorer_Launch_Over_A_Broken_File_Is_Explained() {
        // Double-clicked from Explorer, a file that cannot be read would
        // otherwise leave the user looking at a blank window with no idea
        // that anything at all was asked of the machine.
        var dialogs = new FakeDialogService();
        var viewModel = CreateViewModel(dialogService: dialogs);
        _files.FailReadsWith = new EndOfStreamException("The file ends before its own head promised.");

        await viewModel.OpenFromCommandLineAsync("broken.shiranuix");

        var asked = Assert.Single(dialogs.Asked);
        Assert.Contains("broken.shiranuix", asked.Message);
    }
}
