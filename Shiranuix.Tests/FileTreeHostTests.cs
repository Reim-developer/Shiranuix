using Shiranuix.FileTree.State;
using Shiranuix.Models;
using Shiranuix.Services;
using Shiranuix.Tests.Fakes;
using Shiranuix.Utilities;
using Shiranuix.ViewModels;

namespace Shiranuix.Tests;

/// <summary>
/// Tests for the tree's host: how a command called by its id reaches its own
/// work, making the thing a draft row promised, and standing somewhere else
/// with the page's answers in hand.
/// </summary>
/// <remarks>
/// The tree is a reader of folders, and a reader of folders cannot be faked
/// into lying usefully: the disk is a real one, a folder made for the test and
/// taken away after. The page, as ever, reads its files from a fake and lies
/// beautifully.
/// </remarks>
public sealed class FileTreeHostTests {
    private readonly FakeFileService _files = new();
    private readonly FakeFileDialogService _dialogs = new();
    private readonly FakeConfirmDialogService _confirm = new();

    [Fact]
    public async Task A_Command_By_Its_Id_Reaches_Its_Own_Work() {
        using var home = A_Settings_Folder();
        var viewModel = CreateViewModel();
        var host = viewModel.Tree!;
        try {
            using var disk = new Disk();
            await host.StandOnAsync(disk.Root, CancellationToken.None);

            await host.RunCommandAsync("file-tree.new-file");

            Assert.True(host.Tree.Draft?.IsDraft ?? false);

            // The wiring from the tree's panel down is the compiler's job now:
            // the panel has no way to build a view without the host in it.
            // What used to be a step someone could forget is a step nothing
            // can skip.
        } finally {
            TreeSettings.TestHome = string.Empty;
            viewModel.Tree?.Stop();
        }
    }

    [Fact]
    public async Task Closing_Every_Folder_By_Its_Id_Leaves_The_Root_Standing() {
        // The header's button asks by id like its neighbours do, so it is the
        // host that answers, and the rows close from a place that never had
        // to know the tree's name for its own rows.
        using var home = A_Settings_Folder();
        var viewModel = CreateViewModel();
        var host = viewModel.Tree!;
        try {
            using var disk = new Disk();
            Directory.CreateDirectory(disk.PathTo("chapters"));
            await host.StandOnAsync(disk.Root, CancellationToken.None);

            var row = host.Tree.All().First(item => item.Name == "chapters");
            row.IsExpanded = true;
            Assert.True(row.IsExpanded);

            await host.RunCommandAsync("file-tree.collapse-all");

            Assert.False(row.IsExpanded);
            Assert.True(host.Tree.RootRow!.IsExpanded);
            Assert.Equal("Closed every folder.", host.Tree.Status);
        } finally {
            TreeSettings.TestHome = string.Empty;
            viewModel.Tree?.Stop();
        }
    }

    [Fact]
    public async Task A_Name_Given_To_A_Draft_Makes_The_File() {
        using var home = A_Settings_Folder();
        var viewModel = CreateViewModel();
        var host = viewModel.Tree!;
        try {
            using var disk = new Disk();
            disk.WriteFile("notes.txt");
            await host.StandOnAsync(disk.Root, CancellationToken.None);

            // The page reads its files from a fake, so the file the tree is
            // about to make is primed there first: the tree makes it on the
            // real disk, then the page opens it from the fake with the words
            // below and shows them.
            _files.Files[disk.PathTo("chapter.txt")] = "hello.";
            var draft = host.Tree.BeginNewFile();

            var outcome = await host.FinishCreateAsync(draft.Node, "chapter.txt", CancellationToken.None);

            Assert.True(outcome.IsSuccess, outcome.Error);
            Assert.True(File.Exists(disk.PathTo("chapter.txt")));
            Assert.Null(host.Tree.Draft);
            Assert.Equal("chapter.txt", host.Tree.Selected?.Name);
            Assert.Equal("hello.", viewModel.Text);
        } finally {
            TreeSettings.TestHome = string.Empty;
            viewModel.Tree?.Stop();
        }
    }

    [Fact]
    public async Task A_Name_Given_To_A_Draft_Makes_The_Folder() {
        using var home = A_Settings_Folder();
        var viewModel = CreateViewModel();
        var host = viewModel.Tree!;
        try {
            using var disk = new Disk();
            await host.StandOnAsync(disk.Root, CancellationToken.None);
            var draft = host.Tree.BeginNewFolder();

            var outcome = await host.FinishCreateAsync(draft.Node, "chapters", CancellationToken.None);

            Assert.True(outcome.IsSuccess, outcome.Error);
            Assert.True(Directory.Exists(disk.PathTo("chapters")));
            Assert.Null(host.Tree.Draft);
            Assert.Equal("chapters", host.Tree.Selected?.Name);
            Assert.Equal("Made chapters.", host.Tree.Status);
        } finally {
            TreeSettings.TestHome = string.Empty;
            viewModel.Tree?.Stop();
        }
    }

    [Fact]
    public async Task A_Name_The_Disk_Refuses_Leaves_The_Waiting_Where_It_Was() {
        using var home = A_Settings_Folder();
        var viewModel = CreateViewModel();
        var host = viewModel.Tree!;
        try {
            using var disk = new Disk();
            disk.WriteFile("taken.txt");
            await host.StandOnAsync(disk.Root, CancellationToken.None);
            var draft = host.Tree.BeginNewFile();

            var outcome = await host.FinishCreateAsync(draft.Node, "taken.txt", CancellationToken.None);

            Assert.False(outcome.IsSuccess);
            Assert.Same(draft, host.Tree.Draft);
            Assert.True(draft.IsEditing);
            Assert.Equal(1, host.Tree.All().Count(item => item.Name == "taken.txt"));
        } finally {
            TreeSettings.TestHome = string.Empty;
            viewModel.Tree?.Stop();
        }
    }

    [Fact]
    public async Task Standing_Somewhere_Else_Empties_The_Page_After_Asking() {
        using var home = A_Settings_Folder();
        var viewModel = CreateViewModel();
        _confirm.Reply = true;
        var host = viewModel.Tree!;
        try {
            using var here = new Disk();
            using var there = new Disk();
            await host.StandOnAsync(here.Root, CancellationToken.None);
            viewModel.Text = "Draft words.";
            _dialogs.FolderResult = there.Root;

            var outcome = await host.ChooseFolderAsync(CancellationToken.None);

            Assert.True(outcome.IsSuccess, outcome.Error);
            Assert.Equal(there.Root, host.Tree.Root?.FullPath);
            Assert.True(viewModel.Document.IsEmpty);
            Assert.False(viewModel.Document.IsDirty);
        } finally {
            TreeSettings.TestHome = string.Empty;
            viewModel.Tree?.Stop();
        }
    }

    [Fact]
    public async Task A_Writer_Who_Keeps_Their_Work_Keeps_Page_And_Tree_Together() {
        using var home = A_Settings_Folder();
        var viewModel = CreateViewModel();
        _confirm.Reply = false;
        var host = viewModel.Tree!;
        try {
            using var here = new Disk();
            using var there = new Disk();
            await host.StandOnAsync(here.Root, CancellationToken.None);
            viewModel.Text = "Draft words.";
            _dialogs.FolderResult = there.Root;

            var outcome = await host.ChooseFolderAsync(CancellationToken.None);

            Assert.False(outcome.IsSuccess);
            Assert.Equal("The page kept its work, so the tree did not move.", outcome.Error);
            Assert.Equal("Draft words.", viewModel.Text);
            Assert.Equal(here.Root, host.Tree.Root?.FullPath);
        } finally {
            TreeSettings.TestHome = string.Empty;
            viewModel.Tree?.Stop();
        }
    }

    [Fact]
    public async Task Moving_On_From_A_Clean_Page_Asked_Nothing() {
        using var home = A_Settings_Folder();
        var viewModel = CreateViewModel();
        var host = viewModel.Tree!;
        try {
            using var here = new Disk();
            using var there = new Disk();
            await host.StandOnAsync(here.Root, CancellationToken.None);
            _dialogs.FolderResult = there.Root;

            var outcome = await host.ChooseFolderAsync(CancellationToken.None);

            Assert.True(outcome.IsSuccess, outcome.Error);
            Assert.Empty(_confirm.AskedAbout);
            Assert.Equal(there.Root, host.Tree.Root?.FullPath);
        } finally {
            TreeSettings.TestHome = string.Empty;
            viewModel.Tree?.Stop();
        }
    }

    /// <summary>
    /// The editor against a page that reads and answers from fakes, with a
    /// tree standing over it.
    /// </summary>
    /// <returns>The editor.</returns>
    private EditorViewModel CreateViewModel() {
        var window = new DocumentWindow(_files);
        var ledger = new EditLedger();
        var document = new DocumentSession(
            new DocumentFile(_files, new FakeRecentFilesStore()),
            new DocumentHistory(),
            window,
            ledger,
            new WindowNavigator(window, ledger));

        return new EditorViewModel(
            _dialogs,
            _confirm,
            document,
            new TypographyState(new FakeFontCatalog(), new FakeUserSettings(), new FakeFontHistoryStore()),
            new PdfExportState(new FakeUserSettings()),
            new FakeExportPdfDialog(),
            new FakePdfViewerService(),
            new PanelRegistry(),
            new RecordingLoggerFactory(),
            new FakeDialogService());
    }

    /// <summary>
    /// Gives the settings a folder of their own, so the tests never write into
    /// the editor's real one, and hands the tree's memory back at the end.
    /// </summary>
    /// <returns>The folder.</returns>
    private static Disk A_Settings_Folder() {
        var home = new Disk();
        TreeSettings.TestHome = home.Root;

        return home;
    }

    /// <summary>
    /// A folder of its own for a test to stand on, taken away with the test.
    /// </summary>
    private sealed class Disk : IDisposable {
        public string Root { get; } = Path.Combine(
            Path.GetTempPath(),
            "shiranuix-tests",
            Guid.NewGuid().ToString("n"));

        public Disk() => Directory.CreateDirectory(Root);

        public string PathTo(string relative) => Path.Combine(Root, relative);

        public void WriteFile(string relative) => File.WriteAllText(PathTo(relative), string.Empty);

        public void Dispose() {
            try {
                if (Directory.Exists(Root)) {
                    Directory.Delete(Root, recursive: true);
                }
            } catch (IOException) {
            } catch (UnauthorizedAccessException) {
            }
        }
    }
}
