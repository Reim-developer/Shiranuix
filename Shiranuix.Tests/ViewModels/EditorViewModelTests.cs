using Shiranuix.Models;
using Shiranuix.Tests.Fakes;
using Shiranuix.ViewModels;

namespace Shiranuix.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="EditorViewModel"/>, the coordinator between the window's
/// dialogs, the document and its typography.
/// </summary>
/// <remarks>
/// Everything it touches is faked, so these run without a window, a file system,
/// or a prompt. The rules about when work is lost are the ones worth checking
/// here — they are the ones a screenshot can never confirm — and the rules of
/// the document and the typography themselves are covered in
/// <see cref="DocumentSessionTests"/> and <see cref="TypographyStateTests"/>.
/// </remarks>
public sealed class EditorViewModelTests {
    private const string ExistingPath = "story.txt";
    private const string ExistingText = "Saved content.";

    private readonly FakeFileService _files = new();
    private readonly FakeFileDialogService _dialogs = new();
    private readonly FakeConfirmDialogService _confirm = new();
    private readonly FakeFontCatalog _fonts = new();
    private readonly FakeUserSettings _settings = new();
    private readonly FakeRecentFilesStore _recentFiles = new();
    private readonly FakeFontHistoryStore _fontHistory = new();

    [Fact]
    public void New_Document_Starts_Clean_And_Untitled() {
        var viewModel = CreateViewModel();

        Assert.Equal(string.Empty, viewModel.Document.Text);
        Assert.False(viewModel.Document.IsDirty);
        Assert.Equal("Shiranuix — Untitled", viewModel.Title);
        // Nothing has happened yet, so there is no status to report and no
        // document for the counts to describe.
        Assert.Equal(string.Empty, viewModel.StatusMessage);
        Assert.False(viewModel.HasStatusMessage);
        Assert.False(viewModel.HasDocument);
    }

    // ===== Status bar ===========================================================

    [Fact]
    public void The_Status_Bar_Waits_For_A_Document() {
        var viewModel = CreateViewModel();

        // A fresh window: no status to report, no counts to read.
        Assert.False(viewModel.HasStatusMessage);
        Assert.False(viewModel.HasDocument);

        // Typing is editing a document — the counts have something to describe,
        // though no status line has been earned yet.
        viewModel.Document.Text = "Started writing.";
        Assert.False(viewModel.HasStatusMessage);
        Assert.True(viewModel.HasDocument);
    }

    [Fact]
    public void Opening_An_Empty_File_Counts_As_A_Document() {
        // The file is open, so the bar has something to say even with no words.
        var viewModel = CreateViewModel();
        _files.Files["empty.txt"] = string.Empty;

        viewModel.OpenFromCommandLine("empty.txt");

        Assert.True(viewModel.HasDocument);
        Assert.Equal("Opened empty.txt", viewModel.StatusMessage);
        Assert.True(viewModel.HasStatusMessage);
    }

    [Fact]
    public void Typing_Sets_Dirty_And_Bullets_The_Title() {
        var viewModel = CreateViewModel();

        viewModel.Document.Text = "Started writing.";

        Assert.True(viewModel.Document.IsDirty);
        Assert.Equal("Shiranuix — • Untitled", viewModel.Title);
    }

    [Fact]
    public void Reverting_To_Saved_Text_Clears_Dirty() {
        var viewModel = CreateViewModel(withExistingFile: true);

        viewModel.Document.Text = "edit";
        Assert.True(viewModel.Document.IsDirty);

        viewModel.Document.Text = ExistingText;

        Assert.False(viewModel.Document.IsDirty);
        Assert.Equal("Shiranuix — story.txt", viewModel.Title);
    }

    // ===== Opening ==============================================================

    [Fact]
    public async Task Open_Loads_The_File_And_Reports_It() {
        _files.Files[ExistingPath] = ExistingText;
        var viewModel = CreateViewModel();
        _dialogs.OpenResult = ExistingPath;

        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.Equal(ExistingText, viewModel.Document.Text);
        Assert.Equal(ExistingPath, viewModel.Document.Path);
        Assert.False(viewModel.Document.IsDirty);
        Assert.Equal("Opened story.txt", viewModel.StatusMessage);
        Assert.Equal(ExistingPath, Assert.Single(viewModel.Document.RecentFiles));
    }

    [Fact]
    public async Task Failed_Open_Keeps_The_Current_Document() {
        var viewModel = CreateViewModel(withExistingFile: true);
        viewModel.Document.Text = "Đang viết dở";
        _dialogs.OpenResult = "missing.txt";

        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.Equal("Đang viết dở", viewModel.Document.Text);
        Assert.Equal(ExistingPath, viewModel.Document.Path);
        Assert.Contains("Could not open", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Cancelled_Open_Changes_Nothing() {
        var viewModel = CreateViewModel(withExistingFile: true);
        var status = viewModel.StatusMessage;
        _dialogs.OpenResult = null;

        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.Equal(ExistingText, viewModel.Document.Text);
        Assert.Equal(ExistingPath, viewModel.Document.Path);
        Assert.False(viewModel.Document.IsDirty);
        // Nothing happened, so the status line is untouched.
        Assert.Equal(status, viewModel.StatusMessage);
    }

    [Fact]
    public async Task Dirty_Document_Prompts_Before_Opening() {
        var viewModel = CreateViewModel(withExistingFile: true);
        viewModel.Document.Text = "Đang viết dở";
        _dialogs.OpenResult = "other.txt";
        _files.Files["other.txt"] = "Other content";
        _confirm.Reply = false;

        await viewModel.OpenCommand.ExecuteAsync(null);

        Assert.Single(_confirm.AskedAbout);
        Assert.Equal("Đang viết dở", viewModel.Document.Text);
    }

    // ===== Saving ===============================================================

    [Fact]
    public async Task Save_On_Unsaved_Document_Prompts_For_A_Path() {
        var viewModel = CreateViewModel();
        viewModel.Document.Text = "Draft text";
        _dialogs.SaveResult = "drafts/draft.txt";

        await viewModel.SaveCommand.ExecuteAsync(null);

        // The dialog must be primed with a usable name rather than blank.
        Assert.Equal(new[] { "Untitled.txt" }, _dialogs.SuggestedNames);
        Assert.Equal("drafts/draft.txt", viewModel.Document.Path);
        Assert.False(viewModel.Document.IsDirty);
        Assert.Equal(new[] { "drafts/draft.txt" }, _files.Writes);
        Assert.Equal("Draft text", _files.Files["drafts/draft.txt"]);
        Assert.Equal("Saved draft.txt", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Save_On_An_Existing_File_Writes_Without_Prompting() {
        var viewModel = CreateViewModel(withExistingFile: true);
        viewModel.Document.Text = "Chapter two";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_dialogs.SuggestedNames);
        Assert.Equal(new[] { ExistingPath }, _files.Writes);
        Assert.False(viewModel.Document.IsDirty);
        Assert.Equal("Chapter two", _files.Files[ExistingPath]);
    }

    [Fact]
    public async Task Cancelled_Save_Leaves_The_Document_Dirty() {
        var viewModel = CreateViewModel();
        viewModel.Document.Text = "Draft text";
        _dialogs.SaveResult = null; // user pressed Cancel

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Null(viewModel.Document.Path);
        Assert.True(viewModel.Document.IsDirty);
        Assert.Empty(_files.Writes);
        Assert.False(viewModel.HasStatusMessage);
    }

    [Fact]
    public async Task Failed_Save_Reports_The_Reason_And_Keeps_The_Document() {
        var viewModel = CreateViewModel();
        viewModel.Document.Text = "Draft text";
        _dialogs.SaveResult = "readonly.txt";
        _files.FailWritesWith = new IOException("read-only");

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Null(viewModel.Document.Path);
        Assert.True(viewModel.Document.IsDirty);
        Assert.Contains("Could not save", viewModel.StatusMessage);
        Assert.Empty(_files.Writes);
    }

    // ===== Prompts ==============================================================

    [Fact]
    public async Task Clean_Document_Does_Not_Prompt_Before_New() {
        var viewModel = CreateViewModel(withExistingFile: true);

        await viewModel.NewDocumentCommand.ExecuteAsync(null);

        Assert.Empty(_confirm.AskedAbout);
        Assert.Equal(string.Empty, viewModel.Document.Text);
        Assert.Null(viewModel.Document.Path);
        Assert.Equal("New document", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Dirty_Document_Prompts_Before_New() {
        var viewModel = CreateViewModel(withExistingFile: true);
        viewModel.Document.Text = "Đang viết dở";
        _confirm.Reply = true;

        await viewModel.NewDocumentCommand.ExecuteAsync(null);

        Assert.Equal("story.txt", Assert.Single(_confirm.AskedAbout));
        Assert.Equal(string.Empty, viewModel.Document.Text);
        Assert.False(viewModel.Document.IsDirty);
    }

    [Fact]
    public async Task Declining_The_Prompt_Keeps_The_Work() {
        // The refusal branch is the one that stops a writer losing a chapter.
        var viewModel = CreateViewModel(withExistingFile: true);
        viewModel.Document.Text = "Đang viết dở";
        _confirm.Reply = false;

        await viewModel.NewDocumentCommand.ExecuteAsync(null);

        Assert.Single(_confirm.AskedAbout);
        Assert.Equal("Đang viết dở", viewModel.Document.Text);
        Assert.True(viewModel.Document.IsDirty);
    }

    [Fact]
    public void CommandLine_Open_Skips_The_Prompt_Entirely() {
        // Launched with a path, so there is no prior document worth saving;
        // asking here would be a dialog the user never asked for.
        var viewModel = CreateViewModel(withExistingFile: true);
        viewModel.Document.Text = "Đang viết dở";
        _files.Files["other.txt"] = "Other content";

        viewModel.OpenFromCommandLine("other.txt");

        Assert.Empty(_confirm.AskedAbout);
        Assert.Equal("other.txt", viewModel.Document.Path);
        Assert.False(viewModel.Document.IsDirty);
    }

    // ===== Delegation ===========================================================

    [Fact]
    public void Cycling_The_Theme_Is_The_Typography_s_To_Do() {
        var viewModel = CreateViewModel();

        viewModel.CycleThemeCommand.Execute(null);

        // The coordinator does not own the theme — it hands the request over
        // and reports the result. The state and its store are asserted in
        // TypographyStateTests.
        Assert.Equal(ThemeMode.Light, viewModel.Typography.ThemeMode);
        Assert.Equal(ThemeMode.Light, _settings.Saves.Single().ThemeMode);
        Assert.False(viewModel.HasStatusMessage);
    }

    /// <summary>
    /// Builds a view model over the fakes, optionally pre-seeding a saved file
    /// so the document starts with a path and matching content.
    /// </summary>
    /// <param name="withExistingFile">Whether story.txt exists on the fake disk.</param>
    /// <returns>A view model wired to the shared fakes.</returns>
    private EditorViewModel CreateViewModel(bool withExistingFile = false) {
        var document = new DocumentSession(_files, _recentFiles);
        var typography = new TypographyState(_fonts, _settings, _fontHistory);
        var viewModel = new EditorViewModel(_dialogs, _confirm, document, typography);

        if (withExistingFile) {
            _files.Files[ExistingPath] = ExistingText;
            viewModel.OpenFromCommandLine(ExistingPath);
        }

        return viewModel;
    }
}
