using Shiranuix.Models;
using Shiranuix.Services;
using Shiranuix.Tests.Fakes;
using Shiranuix.Utilities;
using Shiranuix.ViewModels;

namespace Shiranuix.Tests.Services;

/// <summary>
/// Tests for <see cref="EditorCommands"/>: the table the window's chords are
/// routed through, and what it promises to answer.
/// </summary>
public sealed class EditorCommandsTests {
    private readonly FakeFileService _files = new();
    private readonly FakeFileDialogService _dialogs = new();
    private readonly FakeConfirmDialogService _confirm = new();
    private readonly FakeFontCatalog _fonts = new();
    private readonly FakeUserSettings _settings = new();
    private readonly FakeRecentFilesStore _recentFiles = new();
    private readonly FakeFontHistoryStore _fontHistory = new();

    [Fact]
    public void Every_Chord_The_Keyboard_Answers_Is_A_Command_The_Table_Can_Run() {
        var editor = CreateEditor();

        // Which chord answers which command is decided elsewhere; what is
        // promised here is the routing: whatever the keyboard names, the
        // table can run. A command one of them misses is a move half of the
        // editor hears and the other half cannot answer.
        foreach (var command in KeyCommands.All) {
            Assert.True(
                EditorCommands.TryRun(command.Id, editor),
                $"The chord {command.DefaultGesture} answers a command the table cannot run.");
        }
    }

    /// <summary>An editor over the shared fakes, with no document open.</summary>
    /// <returns>A view model ready to be asked things.</returns>
    private EditorViewModel CreateEditor() {
        var window = new DocumentWindow(_files);
        var ledger = new EditLedger();
        var document = new DocumentSession(
            new DocumentFile(_files, _recentFiles), new DocumentHistory(), window, ledger,
            new WindowNavigator(window, ledger));

        return new EditorViewModel(
            _dialogs, _confirm, document,
            new TypographyState(_fonts, _settings, _fontHistory),
            new PdfExportState(_settings),
            new FakeExportPdfDialog(),
            new FakePdfViewerService(),
            new PanelRegistry());
    }
}
