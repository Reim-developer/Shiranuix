using Shiranuix.PluginHost;
using Shiranuix.Services;
using Shiranuix.Tests.Fakes;
using Shiranuix.Utilities;
using Shiranuix.ViewModels;

namespace Shiranuix.Tests.Plugins;

/// <summary>
/// Tests for <see cref="PluginCatalog"/>: the sample plugin is loaded out of a
/// real folder, and everything else is refused for a real reason.
/// </summary>
/// <remarks>
/// The whole of what a plugin can do to the editor runs through here, so these
/// are the tests that would catch a contract the host does not honour. Nothing
/// is stubbed on the plugin's side — the assembly built from
/// <c>samples/reading-time</c> is copied into a temporary folder and loaded as
/// it would be on a writer's machine.
/// </remarks>
public sealed class PluginCatalogTests {
    private readonly FakeFileService _files = new();
    private readonly FakeFileDialogService _dialogs = new();
    private readonly FakeConfirmDialogService _confirm = new();
    private readonly FakeFontCatalog _fonts = new();
    private readonly FakeUserSettings _settings = new();
    private readonly FakeRecentFilesStore _recentFiles = new();
    private readonly FakeFontHistoryStore _fontHistory = new();
    private readonly RecordingLoggerFactory _log = new();
    private readonly FakeDialogService _questions = new();
    private PluginServices _services = null!;
    private PluginEvents _events = new(new RecordingLoggerFactory());

    [Fact]
    public async Task The_Sample_Plugin_Loads_And_Offers_Its_Command() {
        using var plugins = new PluginFixture();
        plugins.InstallSample();
        var catalog = CreateCatalog();

        await catalog.LoadAsync(plugins.Root, CancellationToken.None);

        var installed = Assert.Single(catalog.Installed);
        Assert.Equal("reading-time", installed.Id);
        Assert.Equal("Reading time", installed.Name);

        var command = Assert.Single(catalog.Commands);
        Assert.Equal("reading-time.estimate", command.Info.Id);
    }

    [Fact]
    public async Task A_Loaded_Plugin_Is_Given_The_Document_And_A_Log() {
        using var plugins = new PluginFixture();
        plugins.InstallSample();
        var catalog = CreateCatalog();

        await catalog.LoadAsync(plugins.Root, CancellationToken.None);

        // Both doors, said by the plugin itself: it counted without a document
        // it would have logged the other sentence.
        Assert.True(_log.Mentions("Ready to count"), "the plugin did not get the document");
    }

    [Fact]
    public async Task A_Command_Runs_And_Says_What_It_Found() {
        using var plugins = new PluginFixture();
        plugins.InstallSample();
        var (catalog, editor) = Create();
        editor.Text = "one two three four five";

        await catalog.LoadAsync(plugins.Root, CancellationToken.None);
        var outcome = await catalog.Commands[0].ExecuteAsync(CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal("5 words — about a minute.", editor.StatusMessage);
    }

    [Fact]
    public async Task Closing_The_Editor_Lets_The_Plugin_Go() {
        using var plugins = new PluginFixture();
        plugins.InstallSample();
        var catalog = CreateCatalog();

        await catalog.LoadAsync(plugins.Root, CancellationToken.None);
        await catalog.ShutdownAsync(CancellationToken.None);

        Assert.True(_log.Mentions("Going quiet"), "the plugin was never asked to shut down");
    }

    [Fact]
    public async Task A_Folder_Without_A_Manifest_Is_Written_About_And_Skipped() {
        using var plugins = new PluginFixture();
        plugins.FolderWithoutManifest("lonely");
        var catalog = CreateCatalog();

        await catalog.LoadAsync(plugins.Root, CancellationToken.None);

        Assert.Empty(catalog.Installed);
        Assert.True(_log.Mentions("manifest.json is missing"));
    }

    [Fact]
    public async Task A_Manifest_Without_An_Assembly_Is_Written_About_And_Skipped() {
        using var plugins = new PluginFixture();
        plugins.WriteManifest("ghost", "ghost");
        var catalog = CreateCatalog();

        await catalog.LoadAsync(plugins.Root, CancellationToken.None);

        Assert.Empty(catalog.Installed);
        Assert.True(_log.Mentions("is not there"), "the missing assembly went unmentioned");
    }

    [Fact]
    public async Task A_Manifest_Naming_A_Type_That_Is_Not_There_Is_Refused() {
        using var plugins = new PluginFixture();
        plugins.InstallSample();
        plugins.WriteManifest(
            "liar", "liar", entryPoint: "ReadingTime.NotAPlugin", withSampleAssembly: true);
        var catalog = CreateCatalog();

        await catalog.LoadAsync(plugins.Root, CancellationToken.None);

        // The liar is gone; the plugin beside it still loaded, which is the
        // whole promise of the catalog.
        Assert.Equal("reading-time", Assert.Single(catalog.Installed).Id);
        Assert.True(_log.Mentions("is not a type"));
    }

    [Fact]
    public async Task A_Plugin_Built_For_Another_Major_Is_Refused() {
        using var plugins = new PluginFixture();
        plugins.InstallSample();
        plugins.WriteManifest("reading-time-2", "reading-time-2", api: "1.0");
        var catalog = CreateCatalog();

        await catalog.LoadAsync(plugins.Root, CancellationToken.None);

        Assert.Equal("reading-time", Assert.Single(catalog.Installed).Id);
        Assert.True(_log.Mentions("speaks contract 1.0+"));
    }

    [Fact]
    public async Task An_Empty_Folder_Holds_Nothing_And_Says_Nothing() {
        using var plugins = new PluginFixture();
        var catalog = CreateCatalog();

        await catalog.LoadAsync(plugins.Root, CancellationToken.None);

        Assert.Empty(catalog.Installed);
        Assert.Empty(catalog.Commands);
    }

    [Fact]
    public async Task The_Editors_Own_Doors_Are_On_The_Register_Too() {
        using var plugins = new PluginFixture();
        plugins.InstallSample();
        var (catalog, _) = Create();

        await catalog.LoadAsync(plugins.Root, CancellationToken.None);

        // Asking for one through the open register and asking for it as a
        // capability find the same instance: one concept, two spellings.
        Assert.NotNull(_services.Resolve<Shiranuix.Plugins.Editor.IEditorHost>());
        Assert.True(_services.IsRegistered<Shiranuix.Plugins.Editor.IEditorHost>());
    }

    private PluginCatalog CreateCatalog() => Create().Catalog;

    private (PluginCatalog Catalog, EditorViewModel Editor) Create() {
        var window = new DocumentWindow(_files);
        var ledger = new EditLedger();
        var document = new DocumentSession(
            new DocumentFile(_files, _recentFiles),
            new DocumentHistory(),
            window,
            ledger,
            new WindowNavigator(window, ledger));

        var panels = new PanelRegistry();

        var editor = new EditorViewModel(
            _dialogs, _confirm, document, new TypographyState(_fonts, _settings, _fontHistory), panels, _events);

        var doors = new Dictionary<Type, object> {
            [typeof(Shiranuix.Plugins.Editor.IEditorHost)] =
                new EditorHostCapability(editor, _dialogs)
        };

        var services = new PluginServices();
        foreach (var door in doors) {
            services.Register(door.Key, door.Value);
        }
        var events = new PluginEvents(_log);

        _services = services;
        _events = events;

        return (new PluginCatalog(editor, _dialogs, _questions, _log, services, events, panels), editor);
    }
}
