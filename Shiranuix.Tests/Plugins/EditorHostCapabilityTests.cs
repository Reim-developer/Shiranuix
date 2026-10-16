using Shiranuix.PluginHost;
using Shiranuix.Plugins;
using Shiranuix.Plugins.Editor;
using Shiranuix.Plugins.Text;
using Shiranuix.Services;
using Shiranuix.Tests.Fakes;
using Shiranuix.Utilities;
using Shiranuix.ViewModels;

namespace Shiranuix.Tests.Plugins;

/// <summary>
/// Tests for <see cref="EditorHostCapability"/>: the door plugins reach the
/// document through, and what a plugin's changes do to the writer's own undo.
/// </summary>
/// <remarks>
/// The promise being pinned down is that a plugin's edit is an edit: one move,
/// in the document's own history, with the caret where the plugin said. A
/// plugin that bypassed this would leave a writer pressing Ctrl+Z and nothing
/// happening, which is the worst thing an extension can do to someone.
/// </remarks>
public sealed class EditorHostCapabilityTests {
    private readonly RecordingLoggerFactory _log = new();
    private readonly PluginEvents _events = new(new RecordingLoggerFactory());
    private readonly FakeFileService _files = new();
    private readonly FakeFileDialogService _dialogs = new();
    private readonly FakeConfirmDialogService _confirm = new();
    private readonly FakeFontCatalog _fonts = new();
    private readonly FakeUserSettings _settings = new();
    private readonly FakeRecentFilesStore _recentFiles = new();
    private readonly FakeFontHistoryStore _fontHistory = new();

    [Fact]
    public void The_Snapshot_Describes_The_Document_As_It_Stands() {
        var (editor, host) = Create();

        var before = host.Snapshot;
        editor.Text = "one two";
        var after = host.Snapshot;

        Assert.Equal(string.Empty, before.Text);
        Assert.False(before.IsDirty);
        Assert.Equal("one two", after.Text);
        Assert.True(after.IsDirty);
        Assert.Equal(new TextPoint(0, 1, 1), after.Caret);
        Assert.True(after.Selection.IsEmpty);
    }

    [Fact]
    public void A_Replacement_Replaces_Only_What_It_Was_Given() {
        var (editor, host) = Create();
        editor.Text = "one two three";

        var outcome = host.Replace(new TextRange(4, 3), "2");

        Assert.True(outcome.IsSuccess);
        Assert.Equal("one 2 three", editor.Text);
    }

    [Fact]
    public void A_Replacement_Leaves_The_Caret_Where_The_Text_Ended() {
        var (editor, host) = Create();
        editor.Text = "one two three";

        host.Replace(new TextRange(0, 3), "ONE");

        Assert.Equal(3, editor.CaretIndex);
    }

    [Fact]
    public void A_Replacement_Is_One_Move_The_Writer_Can_Take_Back() {
        var (editor, host) = Create();
        editor.Text = "one two three";

        host.Replace(new TextRange(4, 3), "2");
        editor.Undo();

        // The whole replacement, in one press: not the characters of it, and
        // not the writer's own typing before it.
        Assert.Equal("one two three", editor.Text);
    }

    [Fact]
    public void A_Range_That_Is_Not_In_The_Document_Is_Refused_Not_Attempted() {
        var (editor, host) = Create();
        editor.Text = "short";

        var outcome = host.Replace(new TextRange(4, 40), "x");

        Assert.False(outcome.IsSuccess);
        Assert.Contains("past the end", outcome.Error);
        Assert.Equal("short", editor.Text);
    }

    [Fact]
    public void Inserting_Puts_Text_Where_The_Caret_Stands() {
        var (editor, host) = Create();
        editor.Text = "one  two";
        editor.CaretIndex = 4;

        host.Insert("-");

        Assert.Equal("one - two", editor.Text);
    }

    [Fact]
    public void Selecting_Says_What_Is_Selected_And_Where_The_Caret_Stands() {
        var (editor, host) = Create();
        editor.Text = "one two three";

        var outcome = host.Select(new TextRange(4, 3));

        Assert.True(outcome.IsSuccess);
        Assert.Equal(4, editor.SelectionStart);
        Assert.Equal(7, editor.SelectionEnd);
        Assert.Equal(7, editor.CaretIndex);
    }

    [Fact]
    public void Taking_A_Move_Back_With_Nothing_To_Take_Back_Says_So() {
        var (_, host) = Create();

        Assert.False(host.Undo().IsSuccess);
        Assert.False(host.Redo().IsSuccess);
    }

    [Fact]
    public void Saying_Something_In_The_Status_Line_Is_The_Writers_Own_Line_Again() {
        var (editor, host) = Create();

        Assert.True(host.SetStatus("412 words.").IsSuccess);
        Assert.Equal("412 words.", editor.StatusMessage);
    }

    [Fact]
    public async Task Saving_A_Document_That_Was_Never_Saved_Asks_Where_To_Put_It() {
        var (editor, host) = Create();
        editor.Text = "something worth keeping";
        _dialogs.SaveResult = null;

        // The writer closed the picker: there is nowhere to write to, and the
        // answer is a sentence rather than a file half-written.
        var outcome = await host.SaveAsync(CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Contains("did not say where", outcome.Error);
    }

    [Fact]
    public async Task Saving_A_Document_Writes_It_And_Says_So() {
        var (editor, host) = Create();
        editor.Text = "something worth keeping";

        var outcome = await host.SaveAsync(CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(
            "something worth keeping", _files.Files[SessionFactory.ExistingPath]);
        Assert.Equal(SessionFactory.ExistingPath, editor.Document.Path);
    }

    [Fact]
    public void A_Plugin_Is_Told_When_The_Document_Moves() {
        var (editor, host) = Create();
        var heard = new List<PluginDocumentEvent>();
        host.DocumentChanged += news => heard.Add(news.Kind);

        editor.Text = "a";
        editor.Text = "ab";

        Assert.Contains(PluginDocumentEvent.Changed, heard);
    }

    [Fact]
    public async Task A_Plugin_Is_Told_When_The_Document_Is_Written() {
        var (editor, host) = Create();
        editor.Text = "written";
        var heard = new List<PluginDocumentEvent>();
        host.DocumentChanged += news => heard.Add(news.Kind);

        await host.SaveAsync(CancellationToken.None);

        Assert.Contains(PluginDocumentEvent.Saved, heard);
    }

    [Fact]
    public async Task Opening_A_File_That_Is_Not_There_Says_So() {
        var (_, host) = Create();

        var outcome = await host.OpenFileAsync("nowhere.txt", CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Contains("no file at", outcome.Error);
    }

    [Fact]
    public async Task Opening_A_Folder_As_Though_It_Were_A_File_Says_So() {
        var (_, host) = Create();

        var outcome = await host.OpenFileAsync(AppContext.BaseDirectory, CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Contains("is a folder", outcome.Error);
    }

    [Fact]
    public async Task Opening_Nothing_Says_Which_File_Was_Meant() {
        var (_, host) = Create();

        var outcome = await host.OpenFileAsync("  ", CancellationToken.None);

        Assert.False(outcome.IsSuccess);
    }

    [Fact]
    public async Task Opening_A_File_The_Writer_Has_Changed_Asks_About_The_Change_First() {
        var (editor, host) = Create();
        editor.Text = "unsaved work";
        _confirm.Reply = false;

        var path = Path.Combine(Path.GetTempPath(), $"shiranuix-{Guid.NewGuid():n}.txt");
        File.WriteAllText(path, "something else entirely");

        try {
            var outcome = await host.OpenFileAsync(path, CancellationToken.None);

            Assert.False(outcome.IsSuccess);
            Assert.NotEmpty(_confirm.AskedAbout);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Opening_A_File_Opens_It_And_Says_So() {
        var (editor, host) = Create();

        // The door checks the real disk before it asks the document to read,
        // and the document reads through its own file service: so the file has
        // to exist in both, which is what a real one would do.
        var path = Path.Combine(Path.GetTempPath(), $"shiranuix-{Guid.NewGuid():n}.txt");
        File.WriteAllText(path, "a page of something");
        _files.Files[path] = "a page of something";

        try {
            var outcome = await host.OpenFileAsync(path, CancellationToken.None);

            Assert.True(outcome.IsSuccess);
            Assert.Equal("a page of something", editor.Text);
            Assert.Equal(path, editor.Document.Path);
        } finally {
            File.Delete(path);
        }
    }

    private (EditorViewModel Editor, IEditorHost Host) Create() {
        var window = new DocumentWindow(_files);
        var ledger = new EditLedger();
        var document = new DocumentSession(
            new DocumentFile(_files, _recentFiles),
            new DocumentHistory(),
            window,
            ledger,
            new WindowNavigator(window, ledger));

        _dialogs.SaveResult = SessionFactory.ExistingPath;

        var editor = new EditorViewModel(
            _dialogs, _confirm, document, new TypographyState(_fonts, _settings, _fontHistory), new PanelRegistry());

        return (editor, new EditorHostCapability(editor, _dialogs));
    }
}
