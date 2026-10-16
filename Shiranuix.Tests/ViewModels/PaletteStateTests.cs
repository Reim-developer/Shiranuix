using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Shiranuix.Models;
using Shiranuix.PluginHost;
using Shiranuix.Plugins;
using Shiranuix.Plugins.Commands;
using Shiranuix.Services;
using Shiranuix.Tests.Fakes;
using Shiranuix.Utilities;
using Shiranuix.ViewModels;

namespace Shiranuix.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="PaletteState"/>: the one door a command has, and the
/// promises made about what a typed name finds.
/// </summary>
public sealed class PaletteStateTests {
    private readonly RecordingLoggerFactory _log = new();
    private readonly PluginEvents _events = new(new RecordingLoggerFactory());
    private readonly FakeFileService _files = new();
    private readonly FakeFileDialogService _dialogs = new();
    private readonly FakeConfirmDialogService _confirm = new();
    private readonly FakeFontCatalog _fonts = new();
    private readonly FakeUserSettings _settings = new();
    private readonly FakeRecentFilesStore _recentFiles = new();
    private readonly FakeFontHistoryStore _fontHistory = new();
    private readonly PluginCommands _plugins = new();
    private EditorViewModel _editor = null!;

    private PaletteState Create() => new(() => _editor, _plugins);

    [Fact]
    public void A_Palette_Of_Nothing_Still_Offers_The_Editor_s_Own_Commands() {
        _plugins.Source = null;

        var palette = Create();
        palette.Open();

        // The editor's commands are not plugin commands, and a palette built
        // where no plugin is behind it must not be an empty box.
        Assert.NotEmpty(palette.Entries);
        Assert.All(palette.Entries, row => Assert.Equal("editor", row.Origin));
    }

    [Fact]
    public void A_Plugins_Command_Is_Offered_Too_And_Says_Whose_It_Is() {
        var ran = false;
        _plugins.Offering(new PluginCommandStub("file-tree.move-to", "Move to…", "Moves a row somewhere else.", "Ctrl+Shift+M", () => ran = true));

        var palette = Create();
        palette.Open();

        var row = Assert.Single(palette.Entries, row => row.Id == "file-tree.move-to");
        Assert.Equal("file-tree", row.Origin);
        Assert.Equal("Move to…", row.Label);
        Assert.Equal("Ctrl+Shift+M", row.Gesture);
        Assert.True(row.HasGesture);

        // Typed by name, the way a writer reaches a command nobody told them
        // the chord of: the row that answers is the row that runs.
        palette.Query = "move-to";
        Assert.Equal("file-tree.move-to", Assert.Single(palette.Entries).Id);
        palette.Run();

        Assert.True(ran);
    }

    [Fact]
    public void A_Plugin_That_Loads_After_The_Window_Is_Up_Is_Still_In_The_Box() {
        _plugins.Source = null;

        var palette = Create();
        palette.Open();
        Assert.DoesNotContain(palette.Entries, row => row.Id == "reading-time.count");

        // The list is read each time the box opens rather than remembered: a
        // plugin that loads after the window would otherwise never be in it.
        _plugins.Offering(new PluginCommandStub("reading-time.count", "Reading time", null, null, () => { }));

        palette.Open();

        Assert.Contains(palette.Entries, row => row.Id == "reading-time.count");
    }

    [Fact]
    public void A_Plugin_That_Loads_While_The_Box_Is_Open_Turns_Up_In_It() {
        _plugins.Source = null;

        var palette = Create();
        palette.Open();
        palette.Query = "reading";
        Assert.Empty(palette.Entries);

        // The box was already open and was asked for something nothing
        // answered; a plugin arriving a moment later has to fill it in
        // without the writer closing and opening again.
        _plugins.Offering(new PluginCommandStub("reading-time.count", "Reading time", null, null, () => { }));

        Assert.Single(palette.Entries, row => row.Id == "reading-time.count");
    }

    [Fact]
    public void What_Is_Typed_Finds_The_Command_Whose_Id_Is_Exactly_It() {
        _plugins.Source = null;

        var palette = Create();
        palette.Open();

        palette.Query = "undo";

        // "Redo" contains "undo" and would otherwise rank with it; the id that
        // is exactly what was typed comes first.
        Assert.Equal("Undo", palette.Entries[0].Label);
    }

    [Fact]
    public void A_Leading_Colon_Is_How_A_Command_Is_Written_Not_What_Is_Searched_For() {
        _plugins.Source = null;

        var palette = Create();
        palette.Open();
        palette.Query = ":save";

        Assert.Contains(palette.Entries, row => row.Id == "Save");
        Assert.Contains(palette.Entries, row => row.Id == "SaveAs");
    }

    [Fact]
    public void What_Is_Typed_And_Answers_Nothing_Says_So_And_Enter_Does_Nothing() {
        _plugins.Source = null;

        var palette = Create();
        palette.Open();
        palette.Query = "nothing-is-called-this";

        Assert.Empty(palette.Entries);
        Assert.True(palette.IsEmpty);
        Assert.False(palette.HasEntries);
        Assert.Contains("nothing-is-called-this", palette.EmptyMessage);

        // An empty box answers Enter by closing itself, not by running
        // whatever happened to be standing out.
        palette.Run();

        Assert.False(palette.IsOpen);
    }

    [Fact]
    public void The_Highlight_Walks_The_Rows_And_Wraps_Around_At_Both_Ends() {
        _plugins.Source = null;

        var palette = Create();
        palette.Open();
        var count = palette.Entries.Count;
        Assert.True(count > 2);

        Assert.Equal(0, palette.Highlighted);

        palette.Down();
        Assert.Equal(1, palette.Highlighted);

        palette.Up();
        palette.Up();
        Assert.Equal(count - 1, palette.Highlighted);

        palette.Down();
        Assert.Equal(0, palette.Highlighted);
    }

    [Fact]
    public void Exactly_One_Row_Stands_Out_At_A_Time() {
        _plugins.Source = null;

        var palette = Create();
        palette.Open();
        palette.Down();

        Assert.Single(palette.Entries, row => row.IsHighlighted);
        Assert.True(palette.Entries[1].IsHighlighted);
    }

    [Fact]
    public void Running_A_Row_Puts_The_Box_Away_First() {
        var order = new List<string>();
        _plugins.Offering(new PluginCommandStub("plugin.slow", "Slow", null, null, () => order.Add("ran")));

        var palette = Create();
        palette.Query = "slow";
        palette.Run();

        // The box is gone before the work starts: a command that opens a
        // dialog should not open it behind a list that is still showing.
        Assert.False(palette.IsOpen);
        Assert.Equal(["ran"], order);
    }

    [Fact]
    public void Opening_The_Box_Again_Starts_Over() {
        _plugins.Source = null;

        var palette = Create();
        palette.Open();
        palette.Query = "save";
        palette.Down();
        palette.Close();

        palette.Open();

        Assert.Equal(string.Empty, palette.Query);
        Assert.Equal(0, palette.Highlighted);
    }

    [Fact]
    public void The_Chord_Toggles_The_Box_Rather_Than_Only_Opening_It() {
        _plugins.Source = null;

        var palette = Create();
        palette.ToggleCommand.Execute(null);
        Assert.True(palette.IsOpen);

        palette.ToggleCommand.Execute(null);
        Assert.False(palette.IsOpen);
    }

    [Fact]
    public async Task A_Plugin_Command_That_Refuses_Says_Why_In_The_Status_Bar() {
        _editor = CreateEditor();
        var palette = new PaletteState(() => _editor, _plugins);
        _plugins.Offering(new PluginCommandStub(
            "file-tree.move-to", "Move to…", "Nothing happened.", null,
            () => { }, PluginOutcome.Failure("The folder is gone.")));

        palette.Open();
        palette.Query = "move-to";
        palette.Run();
        await Task.Yield();

        Assert.Equal("The folder is gone.", _editor.StatusMessage);
    }

    [Fact]
    public void A_Command_That_Cannot_Run_Is_There_And_Enter_Leaves_It_Alone() {
        var ran = false;
        var stub = new PluginCommandStub("file-tree.delete", "Delete", null, null, () => ran = true);

        _plugins.Offering(stub.RefusesUntil(() => false));
        var palette = Create();
        palette.Open();
        palette.Query = "delete";

        var row = Assert.Single(palette.Entries);
        Assert.False(row.CanRun);

        // The row is still listed: a writer looking for it should find it. It
        // is drawn flat, and Enter on it does nothing rather than failing
        // after the fact with a sentence nobody asked for.
        palette.Run();

        Assert.False(ran);
        Assert.True(palette.IsOpen);
    }

    [Fact]
    public void A_Command_That_Can_Run_Is_Run() {
        var ran = false;
        _plugins.Offering(new PluginCommandStub(
            "file-tree.refresh", "Refresh tree", null, "F5", () => ran = true));

        var palette = Create();
        palette.Open();
        palette.Query = "refresh";

        Assert.True(Assert.Single(palette.Entries).CanRun);
        palette.Run();

        Assert.True(ran);
    }

    [Fact]
    public void Every_Chord_The_Keyboard_Answers_Is_A_Command_The_Palette_Can_Also_Run() {
        _editor = CreateEditor();

        // The two tables are read by different doors - the window by chord, the
        // box by name - so a command in one and missing from the other is a
        // command half a writer can reach. This is what pins the palette's own
        // chord to something that opens it.
        foreach (var command in KeyCommands.All) {
            Assert.True(
                EditorCommands.TryRun(command.Id, _editor),
                $"The chord {command.DefaultGesture} answers a command the palette cannot type.");
        }

        // The loop has already run the palette's own command by now, and running it
        // again would close the box it just opened.
        Assert.True(_editor.Palette.IsOpen);
    }

    /// <summary>An editor over the shared fakes, for the palette to talk to.</summary>
    /// <returns>A view model with nothing open.</returns>
    private EditorViewModel CreateEditor() {
        var window = new DocumentWindow(_files);
        var ledger = new EditLedger();
        var document = new DocumentSession(
            new DocumentFile(_files, _recentFiles), new DocumentHistory(), window, ledger,
            new WindowNavigator(window, ledger));

        return new EditorViewModel(
            _dialogs, _confirm, document,
            new TypographyState(_fonts, _settings, _fontHistory),
            new PluginHost.PanelRegistry());
    }

    /// <summary>A command source a test can put things into and take out of.</summary>
    private sealed class PluginCommands : IPluginCommandSource {
        public IReadOnlyList<IPluginCommand>? Source { get; set; }

        public IReadOnlyList<IPluginCommand> Commands => Source ?? [];

        public event EventHandler? CommandsChanged;

        /// <summary>Puts commands on offer, as a loaded plugin would.</summary>
        /// <param name="commands">What the plugins are offering.</param>
        public void Offering(params IPluginCommand[] commands) {
            Source = commands;
            CommandsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>A plugin command that does what the test told it to.</summary>
    private sealed class PluginCommandStub : IPluginCommand {
        public PluginCommandStub(
            string id,
            string label,
            string? description,
            string? gesture,
            Action onRun,
            PluginOutcome outcome = default) {
            Info = new PluginCommandInfo(id, label, gesture, description);
            _onRun = onRun;
            _outcome = outcome;
        }

        private readonly Action? _onRun;
        private readonly PluginOutcome _outcome;

        public PluginCommandInfo Info { get; }

        /// <summary>
        /// The binding a menu or a palette asks, which is the same shape as
        /// the plugin's own: it says whether the command can run.
        /// </summary>
        public ICommand Binding { get; private set; } = new RelayCommand(() => { });

        /// <summary>Makes this command refuse until what it is asked changes.</summary>
        /// <param name="canRun">What has to be true before it would do anything.</param>
        /// <returns>Itself, so a test can read it in one line.</returns>
        public PluginCommandStub RefusesUntil(Func<bool> canRun) {
            Binding = new RelayCommand(() => { }, canRun);

            return this;
        }

        public Task<PluginOutcome> ExecuteAsync(CancellationToken cancellationToken) {
            _onRun?.Invoke();

            return Task.FromResult(_outcome.IsSuccess ? PluginOutcome.Ok() : _outcome);
        }
    }
}
