using System.Linq;
using Shiranuix.Models;
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
    private readonly FakeFontCatalog _fonts = new();
    private readonly FakeUserSettings _settings = new();
    private readonly FakeRecentFilesStore _recentFiles = new();
    private readonly FakeFontHistoryStore _fontHistory = new();

    [Fact]
    public void New_Document_Starts_Clean_And_Untitled() {
        var viewModel = CreateViewModel();

        Assert.Equal(string.Empty, viewModel.Text);
        Assert.False(viewModel.IsDirty);
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
        viewModel.Text = "Bắt đầu viết.";
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

    // ===== Document typography ==============================================

    [Fact]
    public void A_New_Document_Starts_In_The_Default_Font_And_Size() {
        var viewModel = CreateViewModel();

        Assert.Equal(EditorViewModel.DefaultFontFamily, viewModel.FontFamily);
        Assert.Equal(EditorViewModel.DefaultFontSize, viewModel.FontSize);
    }

    [Fact]
    public void The_Line_Height_Follows_The_Point_Size() {
        var viewModel = CreateViewModel();

        // The default: what the sheet was designed around.
        Assert.Equal(
            EditorViewModel.LineHeightFactor * EditorViewModel.DefaultFontSize,
            viewModel.EditorLineHeight,
            4);

        // The size where a fixed line height drew every wrapped line over the
        // one above it: 72pt glyphs stand about 96px tall, so the line box has
        // to clear them.
        viewModel.FontSize = 72;

        Assert.True(
            viewModel.EditorLineHeight > 96,
            $"Expected the line box to clear a 72pt line, got {viewModel.EditorLineHeight}.");
        Assert.Equal(72 * EditorViewModel.LineHeightFactor, viewModel.EditorLineHeight, 4);
    }

    [Fact]
    public void The_Gutter_Shows_One_Number_Per_Line() {
        var viewModel = CreateViewModel();

        // An empty document has no lines to number.
        Assert.Equal(string.Empty, viewModel.LineNumbersText);

        viewModel.Text = "one\ntwo\nthree";

        Assert.Equal("1\n2\n3", viewModel.LineNumbersText);
    }

    [Fact]
    public void The_Gutter_s_Numbers_Follow_The_Line_Count() {
        var viewModel = CreateViewModel();

        viewModel.Text = "one\ntwo";
        Assert.Equal("1\n2", viewModel.LineNumbersText);

        // Removing a line renumbers the gutter; joining them does not.
        viewModel.Text = "onetwo";
        Assert.Equal("1", viewModel.LineNumbersText);
    }

    [Fact]
    public void Changing_The_Point_Size_Tells_The_Line_Height_To_Read_Again() {
        // The line height is a computed property, so nothing updates it on its
        // own: the gutter's numbers and the page's own spacing both size
        // themselves by it, and both would be left at the old size.
        var viewModel = CreateViewModel();
        var changed = new List<string>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

        viewModel.FontSize = 72;

        Assert.Contains(nameof(EditorViewModel.EditorLineHeight), changed);
        Assert.Equal(72 * EditorViewModel.LineHeightFactor, viewModel.EditorLineHeight, 4);
    }

    [Fact]
    public void The_Font_Picker_Offers_The_Document_s_Font_Even_When_The_Machine_Lacks_It() {
        var viewModel = CreateViewModel();

        // A typeface the fake catalog does not report — a stand-in for a
        // document opened on a machine that never had it installed.
        viewModel.FontFamily = "Futura";

        Assert.Contains("Futura", viewModel.AvailableFonts);
        // The installed ones are still there, untouched and in order.
        var families = viewModel.AvailableFonts.ToList();
        Assert.True(families.IndexOf("Consolas") < families.IndexOf("Georgia"));
        Assert.Equal(_fonts.Families.Count + 1, families.Count);
    }

    [Fact]
    public void The_Default_Font_Is_Offered_When_It_Is_Installed() {
        var viewModel = CreateViewModel();

        Assert.Contains(EditorViewModel.DefaultFontFamily, viewModel.AvailableFonts);
        // Not doubled up: the catalog already has it.
        Assert.Equal(1, viewModel.AvailableFonts.Count(f => f == EditorViewModel.DefaultFontFamily));
    }

    [Fact]
    public void A_Size_Outside_The_Ladder_Joins_The_Size_Picker_In_Order() {
        var viewModel = CreateViewModel();
        var sizes = viewModel.FontSizes.ToList();

        // 15 is not on the ladder, and 17 is: the default is present either
        // way, so the picker never reads blank.
        Assert.Contains(17d, sizes);
        viewModel.FontSize = 15;

        sizes = viewModel.FontSizes.ToList();
        var index = sizes.IndexOf(15d);
        Assert.True(index > 0, "15 should have been inserted, not appended blindly.");
        Assert.Equal(14d, sizes[index - 1]);
        Assert.Equal(16d, sizes[index + 1]);
    }

    [Fact]
    public void Re_Picking_A_Ladder_Size_Does_Not_Add_A_Second_One() {
        var viewModel = CreateViewModel();

        viewModel.FontSize = 12;
        var before = viewModel.FontSizes.Count;
        viewModel.FontSize = 14;

        Assert.Equal(before, viewModel.FontSizes.Count);
        Assert.Equal(1, viewModel.FontSizes.Count(size => size == 14d));
    }

    [Fact]
    public void Changing_The_Font_Is_Not_An_Edit_To_The_Document() {
        var viewModel = CreateViewModel(withExistingFile: true);

        viewModel.FontFamily = "Verdana";
        viewModel.FontSize = 24;

        // A .txt file holds text and nothing else. A typeface is how this app
        // draws it, so choosing one must not report unsaved work — there is
        // nothing to write back.
        Assert.False(viewModel.IsDirty);
        Assert.Equal(ExistingText, viewModel.Text);
    }

    // ===== Restored state ======================================================

    [Fact]
    public void A_New_Session_Restores_The_Stored_Font_And_Size() {
        _settings.Stored = new UserSettings {
            FontFamily = "Verdana",
            FontSize = 24,
            ThemeMode = ThemeMode.Dark
        };

        var viewModel = CreateViewModel();

        Assert.Equal("Verdana", viewModel.FontFamily);
        Assert.Equal(24, viewModel.FontSize);
        Assert.Equal(ThemeMode.Dark, viewModel.ThemeMode);
    }

    [Fact]
    public void Restoring_Does_Not_Write_What_It_Just_Read() {
        // A restored choice is not a change. Writing it back would rewrite the
        // row on every launch, and recording a font "use" for a typeface the
        // user never picked this session would push their real history down
        // the picker.
        _settings.Stored = new UserSettings {
            FontFamily = "Verdana",
            FontSize = 24,
            ThemeMode = ThemeMode.Light
        };

        CreateViewModel();

        Assert.Empty(_settings.Saves);
        Assert.Empty(_fontHistory.Uses);
    }

    [Fact]
    public void A_First_Run_Falls_Back_To_The_Defaults() {
        var viewModel = CreateViewModel();

        Assert.Equal(EditorViewModel.DefaultFontFamily, viewModel.FontFamily);
        Assert.Equal(EditorViewModel.DefaultFontSize, viewModel.FontSize);
        Assert.Equal(ThemeMode.System, viewModel.ThemeMode);
    }

    [Fact]
    public void A_Restored_Size_Outside_The_Ladder_Joins_The_Picker() {
        _settings.Stored = new UserSettings {
            FontFamily = "Georgia",
            FontSize = 15.5,
            ThemeMode = ThemeMode.System
        };

        var viewModel = CreateViewModel();

        Assert.Contains(15.5, viewModel.FontSizes);
    }

    [Fact]
    public void Changing_The_Typeface_Stores_It() {
        var viewModel = CreateViewModel();
        viewModel.FontFamily = "Verdana";

        var stored = Assert.Single(_settings.Saves);
        Assert.Equal("Verdana", stored.FontFamily);
        // One row, always: the save is an update, not a second entry.
        Assert.Equal(1, stored.Id);
    }

    [Fact]
    public void Changing_The_Size_Stores_It() {
        var viewModel = CreateViewModel();
        viewModel.FontSize = 24;

        var stored = Assert.Single(_settings.Saves);
        Assert.Equal(24, stored.FontSize);
        Assert.Equal(EditorViewModel.DefaultFontFamily, stored.FontFamily);
    }

    [Fact]
    public void Changing_The_Theme_Stores_It() {
        var viewModel = CreateViewModel();
        viewModel.ThemeMode = ThemeMode.Dark;

        var stored = Assert.Single(_settings.Saves);
        Assert.Equal(ThemeMode.Dark, stored.ThemeMode);
    }

    [Fact]
    public void Picking_A_Typeface_Records_It_In_The_History() {
        var viewModel = CreateViewModel();
        viewModel.FontFamily = "Verdana";

        Assert.Equal(new[] { "Verdana" }, _fontHistory.Uses.Select(use => use.Family).ToArray());
    }

    [Fact]
    public void The_Font_Picker_Leads_With_The_Typefaces_Used_Lately() {
        // Remembered oldest first: the store hands back most recent first, so
        // Verdana — picked last — should lead the picker.
        _fontHistory.Remember("Georgia", 17);
        _fontHistory.Remember("Verdana", 24);

        var viewModel = CreateViewModel();

        var fonts = viewModel.AvailableFonts.ToList();
        Assert.Equal("Verdana", fonts[0]);
        Assert.Equal("Georgia", fonts[1]);
    }

    [Fact]
    public void Re_Picking_A_Typeface_Moves_It_To_The_Front() {
        _fontHistory.Remember("Georgia", 17);
        _fontHistory.Remember("Verdana", 24);

        var viewModel = CreateViewModel();

        // Consolas, not the document's own Georgia: setting the picker to the
        // typeface it already shows is no change at all, and would record
        // nothing.
        viewModel.FontFamily = "Consolas";

        Assert.Equal("Consolas", viewModel.AvailableFonts.First());
    }

    [Fact]
    public void The_Size_Picker_Offers_The_Sizes_Used_Lately() {
        _fontHistory.Remember("Georgia", 30);
        _fontHistory.Remember("Georgia", 15.5);

        var viewModel = CreateViewModel();

        Assert.Contains(30d, viewModel.FontSizes);
        Assert.Contains(15.5, viewModel.FontSizes);
    }

    [Fact]
    public void Recent_Files_Are_Restored_Newest_First() {
        _recentFiles.Stored.AddRange(new[] { "a.txt", "b.txt", "c.txt" });

        var viewModel = CreateViewModel();

        Assert.Equal(new[] { "a.txt", "b.txt", "c.txt" }, viewModel.RecentFiles);
        Assert.True(viewModel.HasRecentFiles);
    }

    [Fact]
    public void Opening_A_File_Remembers_It_In_The_Store() {
        var viewModel = CreateViewModel();
        _files.Files[ExistingPath] = ExistingText;

        viewModel.OpenFromCommandLine(ExistingPath);

        Assert.Equal(new[] { ExistingPath }, _recentFiles.Remembered);
    }

    [Fact]
    public void HasRecentFiles_Tracks_Whether_The_List_Is_Empty() {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.HasRecentFiles);

        viewModel.RecentFiles.Add("a.txt");

        Assert.True(viewModel.HasRecentFiles);

        viewModel.RecentFiles.RemoveAt(0);

        Assert.False(viewModel.HasRecentFiles);
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
            var vm = new EditorViewModel(_files, _dialogs, _confirm, _fonts, _settings, _recentFiles, _fontHistory);
            vm.OpenFromCommandLine(ExistingPath);
            return vm;
        }

        return new EditorViewModel(_files, _dialogs, _confirm, _fonts, _settings, _recentFiles, _fontHistory);
    }
}
