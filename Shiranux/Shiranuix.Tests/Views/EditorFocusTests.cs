using System.Reflection;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Shiranuix.Services;
using Shiranuix.Tests.Fakes;
using Shiranuix.Utilities;
using Shiranuix.ViewModels;

namespace Shiranuix.Tests.Views;

/// <summary>
/// Tests for the window's report of where the caret's attention is.
/// </summary>
/// <remarks>
/// The report is the start screen's other half: a document that is empty
/// because nobody has begun is a different thing from one that is empty while
/// a writer composes their first character through an input method. The view
/// side is one binding — the editor's own focus, pushed OneWayToSource — so the
/// test builds that binding the way the markup does and moves focus the way
/// the focus machinery does: through the CLR property, which is the very path
/// the framework raises it on.
/// </remarks>
public sealed class EditorFocusTests {
    private readonly RecordingLoggerFactory _log = new();
    private readonly FakeFileService _files = new();
    private readonly FakeRecentFilesStore _recentFiles = new();
    private readonly FakeFontCatalog _fonts = new();
    private readonly FakeUserSettings _settings = new();
    private readonly FakeFontHistoryStore _fontHistory = new();
    private readonly FakeFileDialogService _dialogs = new();
    private readonly FakeConfirmDialogService _confirm = new();

    [Fact]
    public void Focusing_The_Editor_Reports_It_And_Covers_The_Invitation() {
        // The bug this pins down: the start screen stayed up while the user
        // wrote, because nothing carried the editor's attention across. A
        // composed character may not be in the text yet — but the editor is
        // already theirs, and the invitation has no business being there.
        var editor = new TextBox();
        var viewModel = CreateViewModel();
        Assert.True(viewModel.IsStartVisible);

        BindFocusTo(editor, viewModel);
        FocusOn(editor, true);

        Assert.True(viewModel.IsEditing);
        Assert.False(viewModel.IsStartVisible);
        Assert.True(viewModel.Document.IsEmpty);
    }

    [Fact]
    public void Leaving_An_Untouched_Page_Brings_The_Invitation_Back() {
        var editor = new TextBox();
        var viewModel = CreateViewModel();
        BindFocusTo(editor, viewModel);

        FocusOn(editor, true);
        FocusOn(editor, false);

        Assert.False(viewModel.IsEditing);
        Assert.True(viewModel.IsStartVisible);
    }

    [Fact]
    public void Leaving_A_Page_With_Words_On_It_Keeps_The_Invitation_Away() {
        var editor = new TextBox();
        var viewModel = CreateViewModel();
        BindFocusTo(editor, viewModel);
        FocusOn(editor, true);
        viewModel.Text = "Chapter two";

        FocusOn(editor, false);

        Assert.False(viewModel.IsStartVisible);
    }

    /// <summary>
    /// Builds the markup's binding here: the editor's own focus, pushed into
    /// the view model, never written back the other way.
    /// </summary>
    /// <param name="editor">The editor whose focus is reported.</param>
    /// <param name="viewModel">The view model being told.</param>
    private static void BindFocusTo(TextBox editor, EditorViewModel viewModel) =>
        editor.Bind(
            InputElement.IsFocusedProperty,
            new Binding(nameof(EditorViewModel.IsEditing)) {
                Source = viewModel,
                Mode = BindingMode.OneWayToSource
            });

    /// <summary>
    /// Moves focus the way the framework does when it really changes.
    /// </summary>
    /// <remarks>
    /// Focus itself belongs to the focus manager, which answers only to a
    /// window this test has no need for; the binding listens to the property,
    /// and the property is raised through its private setter by
    /// <c>OnGotFocusCore</c> — the same call this makes.
    /// </remarks>
    /// <param name="editor">The editor to move focus on or off.</param>
    /// <param name="focused">Whether it has focus.</param>
    private static void FocusOn(TextBox editor, bool focused) =>
        typeof(InputElement)
            .GetProperty(nameof(InputElement.IsFocused), BindingFlags.Public | BindingFlags.Instance)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(editor, [focused]);

    private EditorViewModel CreateViewModel() {
        var window = new DocumentWindow(_files);
        var ledger = new EditLedger();
        var typography = new TypographyState(_fonts, _settings, _fontHistory);
        var document = new DocumentSession(
            new DocumentFile(_files, _recentFiles), new DocumentHistory(), window, ledger,
            new WindowNavigator(window, ledger), typography);
        return new EditorViewModel(_dialogs, _confirm, document, typography,
            new PdfExportState(new FakeUserSettings()), new FakeExportPdfDialog(),
            new FakeMissingFontDialog(),
            new FakePdfViewerService(),
            new PanelRegistry(), new RecordingLoggerFactory());
    }
}
