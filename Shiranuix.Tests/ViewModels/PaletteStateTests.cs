using Shiranuix.Models;
using Shiranuix.PluginHost;
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
    private readonly FakeFileService _files = new();
    private readonly FakeFileDialogService _dialogs = new();
    private readonly FakeConfirmDialogService _confirm = new();
    private readonly FakeFontCatalog _fonts = new();
    private readonly FakeUserSettings _settings = new();
    private readonly FakeRecentFilesStore _recentFiles = new();
    private readonly FakeFontHistoryStore _fontHistory = new();
    private EditorViewModel _editor = null!;

    private PaletteState Create() => new(() => _editor);

    [Fact]
    public void The_Palette_Offers_The_Editor_s_Own_Commands() {
        var palette = Create();
        palette.Open();

        // Every command the editor answers to by name is in the box, and each
        // of them says so: they came from the editor, not from anywhere else.
        Assert.NotEmpty(palette.Entries);
        Assert.All(palette.Entries, row => Assert.Equal("editor", row.Origin));
    }

    [Fact]
    public void What_Is_Typed_Finds_The_Command_Whose_Id_Is_Exactly_It() {
        var palette = Create();
        palette.Open();

        palette.Query = "undo";

        // "Redo" contains "undo" and would otherwise rank with it; the id that
        // is exactly what was typed comes first.
        Assert.Equal("Undo", palette.Entries[0].Label);
    }

    [Fact]
    public void A_Leading_Colon_Is_How_A_Command_Is_Written_Not_What_Is_Searched_For() {
        var palette = Create();
        palette.Open();
        palette.Query = ":save";

        Assert.Contains(palette.Entries, row => row.Id == "Save");
        Assert.Contains(palette.Entries, row => row.Id == "SaveAs");
    }

    [Fact]
    public void What_Is_Typed_And_Answers_Nothing_Says_So_And_Enter_Does_Nothing() {
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
        var palette = Create();
        palette.Open();
        palette.Down();

        Assert.Single(palette.Entries, row => row.IsHighlighted);
        Assert.True(palette.Entries[1].IsHighlighted);
    }

    [Fact]
    public void Running_A_Row_Puts_The_Box_Away_And_Runs_The_Command() {
        _editor = CreateEditor();

        var palette = Create();
        palette.Open();
        palette.Query = "wrap";

        // The box is gone before the work starts: a command that opens a
        // dialog should not open it behind a list that is still showing. The
        // command ran too — word wrap answers for it.
        palette.Run();

        Assert.False(palette.IsOpen);
        Assert.True(_editor.WordWrap);
    }

    [Fact]
    public void Opening_The_Box_Again_Starts_Over() {
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
        var palette = Create();
        palette.ToggleCommand.Execute(null);
        Assert.True(palette.IsOpen);

        palette.ToggleCommand.Execute(null);
        Assert.False(palette.IsOpen);
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
}
