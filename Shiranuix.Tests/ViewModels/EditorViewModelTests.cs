using Shiranuix.Tests.Fakes;
using Shiranuix.ViewModels;

namespace Shiranuix.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="EditorViewModel"/>, the open/save/dirty logic behind
/// the writing surface.
/// </summary>
/// <remarks>
/// Every collaborator is faked, so these run without a window, a file system,
/// or a prompt. That is the whole point of keeping the view model free of UI
/// types: the rules about when work is lost are the ones most worth checking,
/// and they are the ones a screenshot can never confirm.
/// </remarks>
public sealed class EditorViewModelTests {
    private const string ExistingPath = "story.txt";
    private const string ExistingText = "Nội dung đã lưu.";

    private readonly FakeFileService _files = new();
    private readonly FakeFileDialogService _dialogs = new();
    private readonly FakeConfirmDialogService _confirm = new();

    [Fact]
    public void New_Document_Starts_Clean_And_Untitled() {
        var viewModel = CreateViewModel();

        Assert.Equal(string.Empty, viewModel.Text);
        Assert.False(viewModel.IsDirty);
        Assert.Equal("Shiranuix — Untitled", viewModel.Title);
        Assert.Equal("Ready", viewModel.StatusMessage);
    }

    [Fact]
    public void Typing_Sets_Dirty_And_Bullets_The_Title() {
        var viewModel = CreateViewModel();

        viewModel.Text = "Bắt đầu viết.";

        Assert.True(viewModel.IsDirty);
        Assert.Equal("Shiranuix — • Untitled", viewModel.Title);
    }

    [Fact]
    public void Reverting_To_Saved_Text_Clears_Dirty() {
        // Dirty is a comparison against the last read/write, not a latch that
        // once set stays set: backspacing your own edit returns you to clean.
        var viewModel = CreateViewModel(withExistingFile: true);

        viewModel.Text = "sửa";
        Assert.True(viewModel.IsDirty);

        viewModel.Text = ExistingText;

        Assert.False(viewModel.IsDirty);
        Assert.Equal("Shiranuix — story.txt", viewModel.Title);
    }

    [Fact]
    public void Counts_Refresh_As_You_Type() {
        var viewModel = CreateViewModel();

        // "hai" (3) + space + "người" (5) = 9 UTF-16 units.
        viewModel.Text = "hai người";

        Assert.Equal(2, viewModel.WordCount);
        Assert.Equal(9, viewModel.CharacterCount);
        Assert.Equal(1, viewModel.LineCount);
    }

    [Fact]
    public void Start_Screen_Shows_Only_While_The_Document_Is_Empty() {
        Assert.True(CreateViewModel().IsStartVisible);

        var viewModel = CreateViewModel(withExistingFile: true);

        // The start screen is drawn *over* the sheet, so getting this
        // backwards shows an empty page on top of a document that loaded
        // perfectly well — the one failure a screenshot caught and this test
        // exists to keep caught.
        Assert.False(viewModel.IsStartVisible);

        // A lone space is something the user typed, not an empty document.
        viewModel.Text = " ";
        Assert.False(viewModel.IsStartVisible);

        viewModel.Text = string.Empty;
        Assert.True(viewModel.IsStartVisible);
    }

    [Fact]
    public async Task Save_On_Unsaved_Document_Prompts_For_A_Path() {
        var viewModel = CreateViewModel();
        viewModel.Text = "Bản nháp";
        _dialogs.SaveResult = "drafts/ban-cap.txt";

        await viewModel.SaveCommand.ExecuteAsync(null);

        // The dialog must be primed with a usable name rather than blank.
        Assert.Equal(new[] { "Untitled.txt" }, _dialogs.SuggestedNames);
        Assert.Equal("drafts/ban-cap.txt", viewModel.FilePath);
        Assert.False(viewModel.IsDirty);
        Assert.Equal(new[] { "drafts/ban-cap.txt" }, _files.Writes);
        Assert.Equal("Bản nháp", _files.Files["drafts/ban-cap.txt"]);
    }

    [Fact]
    public async Task Save_On_An_Existing_File_Writes_Without_Prompting() {
        var viewModel = CreateViewModel(withExistingFile: true);
        viewModel.Text = "Chương hai";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_dialogs.SuggestedNames);
        Assert.Equal(new[] { ExistingPath }, _files.Writes);
        Assert.False(viewModel.IsDirty);
        Assert.Equal("Chương hai", _files.Files[ExistingPath]);
    }

    [Fact]
    public async Task Cancelled_Save_Leaves_The_Document_Dirty() {
        var viewModel = CreateViewModel();
        viewModel.Text = "Bản nháp";
        _dialogs.SaveResult = null; // user pressed Cancel

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Null(viewModel.FilePath);
        Assert.True(viewModel.IsDirty);
        Assert.Empty(_files.Writes);
    }

    [Fact]
    public async Task Failed_Save_Does_Not_Repoint_The_Document() {
        // If the write throws, the view model must not start claiming the text
        // lives on disk. Otherwise the user closes the tab believing it saved.
        var viewModel = CreateViewModel();
        viewModel.Text = "Bản nháp";
        _dialogs.SaveResult = "readonly.txt";
        _files.FailWritesWith = new IOException("read-only");

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Null(viewModel.FilePath);
        Assert.True(viewModel.IsDirty);
        Assert.Contains("Could not save", viewModel.StatusMessage);
        Assert.Empty(_files.Writes);
    }

    [Fact]
    public async Task Open_Loads_The_File_And_Cleans_The_Document() {
        _files.Files[ExistingPath] = ExistingText;
        var viewModel = CreateViewModel();
        _dialogs.OpenResult = ExistingPath;

        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.Equal(ExistingText, viewModel.Text);
        Assert.Equal(ExistingPath, viewModel.FilePath);
        Assert.False(viewModel.IsDirty);
        Assert.Contains("story.txt", viewModel.StatusMessage);
        Assert.Equal(ExistingPath, Assert.Single(viewModel.RecentFiles));
    }

    [Fact]
    public async Task Failed_Open_Keeps_The_Current_Document() {
        var viewModel = CreateViewModel(withExistingFile: true);
        viewModel.Text = "Đang viết dở";
        _dialogs.OpenResult = "missing.txt";

        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.Equal("Đang viết dở", viewModel.Text);
        Assert.Equal(ExistingPath, viewModel.FilePath);
        Assert.Contains("Could not open", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Cancelled_Open_Changes_Nothing() {
        var viewModel = CreateViewModel(withExistingFile: true);
        _dialogs.OpenResult = null;

        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.Equal(ExistingText, viewModel.Text);
        Assert.Equal(ExistingPath, viewModel.FilePath);
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public async Task Clean_Document_Does_Not_Prompt_Before_New() {
        var viewModel = CreateViewModel(withExistingFile: true);

        await viewModel.NewDocumentCommand.ExecuteAsync(null);

        Assert.Empty(_confirm.AskedAbout);
        Assert.Equal(string.Empty, viewModel.Text);
        Assert.Null(viewModel.FilePath);
    }

    [Fact]
    public async Task Dirty_Document_Prompts_Before_New() {
        var viewModel = CreateViewModel(withExistingFile: true);
        viewModel.Text = "Đang viết dở";
        _confirm.Reply = true;

        await viewModel.NewDocumentCommand.ExecuteAsync(null);

        Assert.Equal("story.txt", Assert.Single(_confirm.AskedAbout));
        Assert.Equal(string.Empty, viewModel.Text);
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public async Task Declining_The_Prompt_Keeps_The_Work() {
        // The refusal branch is the one that stops a writer losing a chapter.
        var viewModel = CreateViewModel(withExistingFile: true);
        viewModel.Text = "Đang viết dở";
        _confirm.Reply = false;

        await viewModel.NewDocumentCommand.ExecuteAsync(null);

        Assert.Single(_confirm.AskedAbout);
        Assert.Equal("Đang viết dở", viewModel.Text);
        Assert.True(viewModel.IsDirty);
    }

    [Fact]
    public async Task Dirty_Document_Prompts_Before_Opening() {
        var viewModel = CreateViewModel(withExistingFile: true);
        viewModel.Text = "Đang viết dở";
        _dialogs.OpenResult = "khac.txt";
        _files.Files["khac.txt"] = "Bản khác";
        _confirm.Reply = false;

        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.Single(_confirm.AskedAbout);
        Assert.Equal("Đang viết dở", viewModel.Text);
    }

    [Fact]
    public void CommandLine_Open_Skips_The_Prompt_Entirely() {
        // Launched with a path, so there is no prior document worth saving;
        // asking here would be a dialog the user never asked for.
        var viewModel = CreateViewModel(withExistingFile: true);
        viewModel.Text = "Đang viết dở";
        _files.Files["khac.txt"] = "Bản khác";

        viewModel.OpenFromCommandLine("khac.txt");

        Assert.Empty(_confirm.AskedAbout);
        Assert.Equal("khac.txt", viewModel.FilePath);
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public async Task Reopening_A_Recent_File_Moves_It_To_The_Front() {
        var viewModel = CreateViewModel();
        _files.Files["a.txt"] = "A";
        _files.Files["b.txt"] = "B";

        _dialogs.OpenResult = "a.txt";
        await viewModel.OpenCommand.ExecuteAsync(null);
        _dialogs.OpenResult = "b.txt";
        await viewModel.OpenCommand.ExecuteAsync(null);
        _dialogs.OpenResult = "a.txt";
        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "a.txt", "b.txt" }, viewModel.RecentFiles);
    }

    [Fact]
    public async Task RecentFiles_Stops_At_Ten() {
        var viewModel = CreateViewModel();
        _dialogs.OpenResult = "file0.txt";
        await viewModel.OpenCommand.ExecuteAsync(null);

        for (var i = 1; i <= 12; i++) {
            var path = $"file{i}.txt";
            _files.Files[path] = "x";
            _dialogs.OpenResult = path;
            await viewModel.OpenCommand.ExecuteAsync(null);
        }

        Assert.Equal(10, viewModel.RecentFiles.Count);
        // Newest first, oldest evicted.
        Assert.Equal("file12.txt", viewModel.RecentFiles[0]);
        Assert.Equal("file3.txt", viewModel.RecentFiles[^1]);
    }

    /// <summary>
    /// Builds a view model over the fakes, optionally pre-seeding a saved file
    /// so the document starts with a path and matching content.
    /// </summary>
    /// <param name="withExistingFile">Whether story.txt exists on the fake disk.</param>
    /// <returns>A view model wired to the shared fakes.</returns>
    private EditorViewModel CreateViewModel(bool withExistingFile = false) {
        if (withExistingFile) {
            _files.Files[ExistingPath] = ExistingText;
            var vm = new EditorViewModel(_files, _dialogs, _confirm);
            vm.OpenFromCommandLine(ExistingPath);
            return vm;
        }

        return new EditorViewModel(_files, _dialogs, _confirm);
    }
}
