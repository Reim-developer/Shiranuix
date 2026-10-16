using Shiranuix.Persistence;
using Shiranuix.PluginHost;
using Shiranuix.Plugins;
using Shiranuix.Plugins.UI;
using Shiranuix.Services;
using Shiranuix.Tests.Fakes;
using Shiranuix.Utilities;
using Shiranuix.ViewModels;

namespace Shiranuix.Tests.Plugins;

/// <summary>
/// Tests for taking a plugin out: turning it off, loading it again, and
/// deleting the folder it lives in.
/// </summary>
/// <remarks>
/// A writer who switches a plugin off expects it off now, not on the next run,
/// which is the whole reason this exists: the panel column, the palette and
/// the keybinding table are all holding on to something, and each of them has
/// to let go.
/// </remarks>
public sealed class PluginRemovalTests {
    private readonly RecordingLoggerFactory _log = new();
    private readonly PluginEvents _events = new(new RecordingLoggerFactory());
    private readonly FakeFileService _files = new();
    private readonly FakeFileDialogService _dialogs = new();
    private readonly FakeConfirmDialogService _confirm = new();
    private readonly FakeFontCatalog _fonts = new();
    private readonly FakeUserSettings _settings = new();
    private readonly FakeRecentFilesStore _recentFiles = new();
    private readonly FakeFontHistoryStore _fontHistory = new();
    private readonly FakeDisabledPluginsStore _disabled = new();

    [Fact]
    public async Task Taking_A_Loaded_Plugin_Out_Empties_Its_Panel_And_Its_Commands() {
        using var plugins = new PluginFixture();
        plugins.InstallSample();
        var (catalog, registry, panels) = await Loaded(plugins);

        Assert.Single(panels.Panels);
        Assert.NotEmpty(registry.Commands);

        var gone = await catalog.UnloadAsync("reading-time", CancellationToken.None);

        Assert.True(gone);
        Assert.Empty(catalog.Installed);
        Assert.Empty(catalog.Commands);

        // The column first: a panel holding a view whose plugin has gone is
        // the one thing a writer would watch break.
        Assert.Empty(panels.Panels);
        Assert.Empty(registry.Commands);
    }

    [Fact]
    public async Task A_Plugin_That_Was_Never_Loaded_Is_Not_Taken_Out_Twice() {
        using var plugins = new PluginFixture();
        plugins.InstallSample();
        var (catalog, _, _) = await Loaded(plugins);

        // Once is enough: the second time there is nothing loaded to take out.
        Assert.True(await catalog.UnloadAsync("reading-time", CancellationToken.None));
        Assert.False(await catalog.UnloadAsync("reading-time", CancellationToken.None));
        Assert.False(await catalog.UnloadAsync("no-such-plugin", CancellationToken.None));
    }

    [Fact]
    public async Task A_Plugin_That_Is_Turned_Off_Is_Not_Loaded_At_All() {
        using var plugins = new PluginFixture();
        plugins.InstallSample();
        _disabled.TurnOff("reading-time");

        var (catalog, registry, panels) = await Loaded(plugins);

        // Said once, where the rest of the loading is written about, rather
        // than refused quietly: a writer who wonders why their plugin is gone
        // should find the answer in the log.
        Assert.Empty(catalog.Installed);
        Assert.Empty(registry.Commands);
        Assert.Empty(panels.Panels);
    }

    [Fact]
    public async Task What_Is_On_Disk_Is_Listed_Whether_It_Loaded_Or_Not() {
        using var plugins = new PluginFixture();
        plugins.InstallSample();
        _disabled.TurnOff("reading-time");
        var (catalog, _, _) = await Loaded(plugins);

        // Read from the folder rather than from what loaded: a plugin that
        // will not start is still a folder taking up space, and a list of the
        // ones working would leave no way to get rid of the one that is not.
        var rows = catalog.OnDisk();

        var row = Assert.Single(rows);
        Assert.Equal("reading-time", row.Id);
        Assert.Equal("Turned off.", row.Problem);

        Assert.Contains("1.0.0", row.Version);
    }

    [Fact]
    public async Task A_Folder_With_Nothing_Readable_In_It_Is_Still_A_Row() {
        using var plugins = new PluginFixture();
        var empty = Directory.CreateDirectory(Path.Combine(plugins.Root, "mystery"));
        var (catalog, _, _) = await Loaded(plugins);

        var row = Assert.Single(catalog.OnDisk());

        Assert.Equal("mystery", row.Id);
        Assert.Equal(empty.FullName, row.Folder);
        Assert.NotEqual(string.Empty, row.Problem);
    }

    [Fact]
    public async Task Turning_A_Plugin_Off_Unloads_It_And_Writes_The_Store() {
        using var plugins = new PluginFixture();
        plugins.InstallSample();
        var (catalog, registry, panels) = await Loaded(plugins);
        var state = new PluginSettingsState(
            new CatalogHost(catalog), _disabled, new FakeDialogService(), _log);

        state.Refresh();
        var row = Assert.Single(state.Rows);
        Assert.True(row.IsEnabled);
        Assert.True(row.IsRunning);

        row.IsEnabled = false;

        // The switch is a binding, so the write happens on the way through:
        // not "when the window closes", which is when a writer would find out
        // it had not.
        await Task.Yield();
        await Task.Yield();

        Assert.Equal(["reading-time"], _disabled.Load());
        Assert.Empty(panels.Panels);
        Assert.Empty(registry.Commands);
        Assert.False(Assert.Single(state.Rows).IsRunning);
        Assert.Equal("Turned off.", Assert.Single(state.Rows).StateText);
    }

    [Fact]
    public async Task Removing_A_Plugin_Asks_And_Then_Keeps_It_When_Told_No() {
        using var plugins = new PluginFixture();
        plugins.InstallSample();
        var (catalog, registry, _) = await Loaded(plugins);
        var dialogs = new FakeDialogService { Result = false };
        var state = new PluginSettingsState(new CatalogHost(catalog), _disabled, dialogs, _log);

        state.Refresh();
        var folder = Assert.Single(state.Rows).Folder;

        await state.RemoveAsync(Assert.Single(state.Rows));

        Assert.True(Directory.Exists(folder));
        Assert.NotEmpty(registry.Commands);
        Assert.Single(state.Rows);
    }

    [Fact]
    public async Task Removing_A_Loaded_Plugin_Takes_It_Out_Of_The_Editor_And_Out_Of_The_List() {
        using var plugins = new PluginFixture();
        plugins.InstallSample();
        var (catalog, registry, panels) = await Loaded(plugins);
        var dialogs = new FakeDialogService { Result = true };
        var state = new PluginSettingsState(new CatalogHost(catalog), _disabled, dialogs, _log);

        state.Refresh();
        await state.RemoveAsync(Assert.Single(state.Rows));

        // Out of the editor first: a panel still holding a view whose files
        // are being deleted is worse than a panel that is gone.
        Assert.Empty(panels.Panels);
        Assert.Empty(registry.Commands);

        // And out of the list, which is what the next start reads: whether the
        // folder went then and there or waited in the bin for this editor to
        // close, it can no longer be found under the plugins folder.
        Assert.Empty(state.Rows);
        Assert.True(state.IsEmpty);
    }

    [Fact]
    public async Task Removing_A_Plugin_That_Never_Loaded_Deletes_Its_Folder_Outright() {
        using var plugins = new PluginFixture();
        plugins.InstallSample();

        // A folder with nothing readable in it: it is still a row, and it is
        // still a folder somebody wants to get rid of, so the editor takes it
        // out - and because nothing of it was ever loaded, its files are not
        // held by the runtime and the folder goes where the writer was told.
        Directory.CreateDirectory(Path.Combine(plugins.Root, "mystery"));

        var (catalog, _, _) = await Loaded(plugins);
        var dialogs = new FakeDialogService { Result = true };
        var state = new PluginSettingsState(new CatalogHost(catalog), _disabled, dialogs, _log);

        state.Refresh();
        var mystery = Assert.Single(state.Rows, row => row.Id == "mystery");

        await state.RemoveAsync(mystery);

        Assert.False(Directory.Exists(mystery.Folder));
        Assert.DoesNotContain(state.Rows, row => row.Id == "mystery");

        // The working one is untouched: removing a broken folder is not a
        // reason to disturb the plugins that are fine.
        Assert.Contains(state.Rows, row => row.Id == "reading-time");
    }

    /// <summary>
    /// Builds a catalog over a folder of plugins and loads it.
    /// </summary>
    private async Task<(PluginCatalog Catalog, PluginCommandRegistry Registry, PanelRegistry Panels)> Loaded(
        PluginFixture plugins) {
        var window = new DocumentWindow(_files);
        var ledger = new EditLedger();
        var document = new DocumentSession(
            new DocumentFile(_files, _recentFiles), new DocumentHistory(), window, ledger,
            new WindowNavigator(window, ledger));

        var services = new PluginServices();
        var registry = new PluginCommandRegistry();
        var panels = new PanelRegistry();
        var editor = new EditorViewModel(
            _dialogs, _confirm, document,
            new TypographyState(_fonts, _settings, _fontHistory), panels);

        var catalog = new PluginCatalog(
            editor, _dialogs, new FakeDialogService(), _log, services, _events, panels, registry,
            _disabled);

        await catalog.LoadAsync(plugins.Root, CancellationToken.None);

        return (catalog, registry, panels);
    }

    /// <summary>The catalog behind the seam the settings window talks to.</summary>
    private sealed class CatalogHost(PluginCatalog catalog) : IPluginHost {
        public event EventHandler? PluginsChanged { add { } remove { } }

        public IReadOnlyList<PluginIdentity> Running => catalog.Installed;

        public IReadOnlyList<PluginOnDisk> OnDisk() => catalog.OnDisk();

        public Task<bool> UnloadAsync(string pluginId, CancellationToken cancellationToken) =>
            catalog.UnloadAsync(pluginId, cancellationToken);
    }
}
