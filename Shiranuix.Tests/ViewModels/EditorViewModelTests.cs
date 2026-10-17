using Shiranuix.Models;
using Shiranuix.Services;
using Shiranuix.Tests.Fakes;
using Shiranuix.Utilities;
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
    private readonly RecordingLoggerFactory _log = new();
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
    public void One_Character_Hides_The_Start_Screen() {
        // The failure this pins down: typing one letter left the start screen
        // up, because the change reached the document but never the view model
        // the view was bound to.
        var viewModel = CreateViewModel();
        Assert.True(viewModel.IsStartVisible);

        viewModel.Text = "a";

        Assert.False(viewModel.IsStartVisible);
    }

    [Fact]
    public void A_Composed_Character_Hides_The_Start_Screen_Before_It_Is_Text() {
        // A Vietnamese or Japanese writer composes a character first: it is drawn
        // in the editor and is not yet in the document, so an empty buffer is
        // not the question. The editor reports the typing; the document never
        // hears about it.
        var viewModel = CreateViewModel();
        Assert.True(viewModel.IsStartVisible);

        viewModel.IsEditing = true;

        Assert.False(viewModel.IsStartVisible);
        Assert.True(viewModel.Document.IsEmpty);
    }

    [Fact]
    public void Typing_Again_After_Deleting_Everything_Does_Not_Bring_The_Invitation_Back() {
        var viewModel = CreateViewModel();
        viewModel.IsEditing = true;
        viewModel.Text = "a";

        viewModel.Text = string.Empty;

        // The writer is mid-document: an empty buffer now is a page being
        // rewritten, not a page waiting to be begun.
        Assert.False(viewModel.IsStartVisible);
    }

    [Fact]
    public void The_Status_Bar_Appears_Once_There_Is_A_Document() {
        var viewModel = CreateViewModel();
        Assert.False(viewModel.HasDocument);

        viewModel.Text = "a";

        Assert.True(viewModel.HasDocument);
    }

    [Fact]
    public void An_Opened_Empty_File_Counts_As_A_Document() {
        var viewModel = CreateViewModel();
        _files.Files["empty.txt"] = string.Empty;

        viewModel.OpenFromCommandLine("empty.txt");

        // The file is open, so the bar has something to say even with no words.
        Assert.True(viewModel.HasDocument);
    }

    [Fact]
    public void The_Status_Message_Shows_Itself_Once_There_Is_One() {
        var viewModel = CreateViewModel();
        Assert.False(viewModel.HasStatusMessage);

        _files.Files["other.txt"] = "Other content";
        viewModel.OpenFromCommandLine("other.txt");

        // "Opened other.txt" is a line worth reading, so the bar shows it.
        Assert.True(viewModel.HasStatusMessage);
        Assert.Equal("Opened other.txt", viewModel.StatusMessage);
    }

    [Fact]
    public void The_Caret_Drives_Line_And_Column() {
        var viewModel = CreateViewModel();

        viewModel.Text = "alpha\nbeta";
        viewModel.CaretIndex = 6;

        Assert.Equal(2, viewModel.CaretLine);
        Assert.Equal(1, viewModel.CaretColumn);
    }

    [Fact]
    public void Moving_The_Caret_Reports_Itself() {
        // The caret's index arrives from the editor, which is the only thing that
        // knows where it is; the line and column then follow from it.
        var viewModel = CreateViewModel();
        viewModel.Text = "alpha\nbeta";

        viewModel.CaretIndex = 9;

        Assert.Equal(2, viewModel.CaretLine);
        Assert.Equal(4, viewModel.CaretColumn);
    }

    [Fact]
    public void A_Composing_Character_Moves_The_Status_Bar() {
        // The whole chain for an input method's uncommitted keystroke: the view
        // writes what it sees into the view model, the document works out the
        // place, and the change reaches whoever binds the status bar — under the
        // names the view model forwards, which is the part that has gone wrong
        // in this window more than once.
        var viewModel = CreateViewModel();
        var raised = new List<string?>();
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        viewModel.Composition = Composition.Of("â", null);

        Assert.Equal(2, viewModel.CaretColumn);
        Assert.Contains(nameof(EditorViewModel.Composition), raised);
        Assert.Contains(nameof(EditorViewModel.CaretColumn), raised);
    }

    [Fact]
    public void A_Composing_Keystroke_Brings_The_Status_Bar_Up_Without_Any_Text() {
        // The failure this pins down: with Vietnamese on, the session's first
        // keystroke is a composition, not text — the buffer is still empty, so
        // the bar stayed hidden until the character committed. The line and
        // column the composing caret sits on are worth reading before that.
        var viewModel = CreateViewModel();
        Assert.False(viewModel.HasDocument);
        var raised = new List<string?>();
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        viewModel.Composition = Composition.Of("â", null);

        Assert.True(viewModel.HasDocument);
        Assert.Contains(nameof(EditorViewModel.HasDocument), raised);
        Assert.True(viewModel.Document.IsEmpty);
    }

    [Fact]
    public void A_Composition_That_Ends_Without_Text_Takes_The_Status_Bar_Away() {
        // The other half of the same rule: nothing was committed on an empty
        // document, so the window is a blank sheet again and the bar goes back
        // to having no counts to describe.
        var viewModel = CreateViewModel();
        viewModel.Composition = Composition.Of("â", null);

        viewModel.Composition = Composition.None;

        Assert.False(viewModel.HasDocument);
    }

    [Fact]
    public void A_New_Document_Starts_Clean_And_Untitled() {
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

    // ===== The status bar's line sequence ======================================

    [Fact]
    public void The_Line_Sequence_Can_Be_Chosen_From_The_Status_Bar() {
        // The segment shows the document's convention and ticks the choice;
        // the command is what the flyout's items run.
        var viewModel = CreateViewModel();
        Assert.Equal(viewModel.Document.Newline.Name, viewModel.Newline);

        viewModel.SetNewlineCommand.Execute("CRLF");

        Assert.Equal("CRLF", viewModel.Newline);
        Assert.True(viewModel.NewlineIsCrLf);
        Assert.False(viewModel.NewlineIsLf);
    }

    [Fact]
    public void An_Unknown_Line_Sequence_Changes_Nothing() {
        var viewModel = CreateViewModel();
        var before = viewModel.Document.Newline;

        viewModel.SetNewlineCommand.Execute("bogus");

        Assert.Equal(before, viewModel.Document.Newline);
    }

    [Fact]
    public void The_Window_S_Edges_And_Slides_Are_Forwarded_To_The_Document() {
        var viewModel = CreateViewModel();
        _files.Files["big.txt"] = string.Join('\n', Enumerable.Range(0, 60).Select(i => $"line {i}"));
        viewModel.OpenFromCommandLine("big.txt");

        // The first window begins the file: one arrow's worth of news and
        // no more.
        Assert.False(viewModel.HasLinesAbove);
        Assert.True(viewModel.HasLinesBelow);
        Assert.Equal(40, viewModel.PageLines);

        viewModel.SlideBy(3);

        // Sliding is the document's move; the edges follow it under the
        // same names the view binds.
        Assert.True(viewModel.HasLinesAbove);
        Assert.Equal(
            string.Join('\n', Enumerable.Range(3, 50).Select(i => $"line {i}")),
            viewModel.Text);

        viewModel.SlideToEnd();
        Assert.False(viewModel.HasLinesBelow);

        viewModel.SlideToStart();
        Assert.Equal(
            string.Join('\n', Enumerable.Range(0, 50).Select(i => $"line {i}")),
            viewModel.Text);
    }

    /// <summary>
    /// Builds a view model over the fakes, optionally pre-seeding a saved file
    /// so the document starts with a path and matching content.
    /// </summary>
    /// <param name="withExistingFile">Whether story.txt exists on the fake disk.</param>
    /// <returns>A view model wired to the shared fakes.</returns>
    private EditorViewModel CreateViewModel(bool withExistingFile = false) {
        var window = new DocumentWindow(_files);
        var ledger = new EditLedger();
        var document = new DocumentSession(
            new DocumentFile(_files, _recentFiles), new DocumentHistory(), window, ledger,
            new WindowNavigator(window, ledger));
        var typography = new TypographyState(_fonts, _settings, _fontHistory);
        var viewModel = new EditorViewModel(_dialogs, _confirm, document, typography, new PanelRegistry(), new RecordingLoggerFactory());

        if (withExistingFile) {
            _files.Files[ExistingPath] = ExistingText;
            viewModel.OpenFromCommandLine(ExistingPath);
        }

        return viewModel;
    }
}
