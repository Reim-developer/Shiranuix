using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Shiranuix.FileTree;
using Shiranuix.Services;
using Shiranuix.Services.Dialogs;

namespace Shiranuix.ViewModels;

/// <summary>
/// The window's coordinator: the commands the chrome offers, the status line
/// and title they produce, and the two pieces of state it hands between.
/// </summary>
/// <remarks>
/// <para>
/// This knows how to ask, not how to do. Picking a file, confirming a discard
/// and putting words on the status line are all it handles; the document itself
/// and its typography live in <see cref="DocumentSession"/> and
/// <see cref="TypographyState"/>, which the container builds and this
/// coordinates. The view binds those two directly, so nothing here is a store
/// of the document's or the pickers' contents.
/// </para>
/// <para>
/// It holds no UI types either — not one control, not one window. The things
/// that would have forced a UI dependency (picking a file, confirming a
/// discard) and the things that would have forced a database (settings, recent
/// files, font history) are interfaces handed in at construction, so the whole
/// view model runs headlessly in the test project.
/// </para>
/// <para>
/// The class spans three files by concern: the doors the view binds are in
/// <c>EditorViewModel.Doors.cs</c>, the commands the chrome offers are in
/// <c>EditorViewModel.Commands.cs</c>, and this file holds the wiring and
/// the state the coordinator keeps itself.
/// </para>
/// </remarks>
public partial class EditorViewModel : ViewModelBase {
    private readonly IFileDialogService _dialogs;
    private readonly IDialogService? _dialogService;
    private readonly ILoggerFactory _logFactory;
    private readonly IConfirmDialogService _confirm;
    private readonly FindState _find;
    private readonly PdfExportState _pdfOptions;
    private readonly IExportPdfDialogService _exportDialogs;
    private readonly IMissingFontDialogService _missingFonts;
    private readonly IPdfViewerService _pdfViewers;

    /// <summary>The document's find: a query, its options and its current run.</summary>
    public FindState Find => _find;

    /// <summary>
    /// The column down the left of the page, where the panel of the editor's
    /// own file tree is drawn, and whether it is open.
    /// </summary>
    public PanelRegistry Panel { get; }

    /// <summary>The file tree that follows the document.</summary>
    public FileTree.FileTreeHost? Tree { get; }

    /// <summary>
    /// Creates a view model over the dialogs it needs and the state it
    /// coordinates.
    /// </summary>
    /// <param name="dialogs">File pickers.</param>
    /// <param name="confirm">Discard confirmation prompt.</param>
    /// <param name="document">The open document.</param>
    /// <param name="typography">The document's typography and theme.</param>
    /// <param name="pdfOptions">The shape the last export to PDF was drawn in, and what the next dialog opens on.</param>
    /// <param name="exportDialogs">The chooser the export asks its typeface and size in.</param>
    /// <param name="missingFonts">The dialog a file's own typeface opens when this machine does not carry it.</param>
    /// <param name="pdfViewers">The reader a PDF is shown beside the writing in.</param>
    /// <param name="panels">The column the tree's panel is drawn in.</param>
    /// <param name="logFactory">Where the tree writes about itself.</param>
    /// <param name="dialogService">Asked before the tree deletes or moves; left out when the editor is built headless, whereupon the tree does not stand at all.</param>
    public EditorViewModel(
        IFileDialogService dialogs,
        IConfirmDialogService confirm,
        DocumentSession document,
        TypographyState typography,
        PdfExportState pdfOptions,
        IExportPdfDialogService exportDialogs,
        IMissingFontDialogService missingFonts,
        IPdfViewerService pdfViewers,
        PanelRegistry panels,
        ILoggerFactory? logFactory = null,
        IDialogService? dialogService = null) {
        _logFactory = logFactory ?? new Microsoft.Extensions.Logging.LoggerFactory();
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _confirm = confirm ?? throw new ArgumentNullException(nameof(confirm));
        _dialogService = dialogService;
        _pdfOptions = pdfOptions ?? throw new ArgumentNullException(nameof(pdfOptions));
        _exportDialogs = exportDialogs ?? throw new ArgumentNullException(nameof(exportDialogs));
        _missingFonts = missingFonts ?? throw new ArgumentNullException(nameof(missingFonts));
        _pdfViewers = pdfViewers ?? throw new ArgumentNullException(nameof(pdfViewers));
        Document = document ?? throw new ArgumentNullException(nameof(document));
        Typography = typography ?? throw new ArgumentNullException(nameof(typography));
        Panel = panels ?? throw new ArgumentNullException(nameof(panels));
        Tree = _dialogService is null ? null! : new FileTree.FileTreeHost(dialogs, _dialogService, this, _logFactory);

        // The search finishes on a thread of its own — a long document is
        // not a thing to look through while the caret waits — so its answer
        // is handed back to the thread that owns the window. The find itself
        // never learns there is a window.
        _find = new FindState(
            () => Text,
            () => CaretIndex,
            action => Dispatcher.UIThread.Post(action));
        _find.MatchRevealed += (start, length) => {
            SelectionStart = start;
            SelectionEnd = start + length;
            CaretIndex = start;
        };

        // The states own the rules. The view still binds this class, and it
        // hears them only if the changes are raised here under the names its
        // doors carry, so each state is exposed in turn.
        Expose(document, name => {
            // The title reads the document's name and dirty state, so it moves
            // with any of them.
            OnPropertyChanged(nameof(Title));

            // Two flags are computed from the document: whether the status bar
            // has a document to count, and whether the start screen is up. The
            // composition moves the first one — an input method's keystroke is
            // the document being written before the text arrives, and the
            // Vietnamese writer's first "â" should bring the line and column
            // up with it rather than after it.
            if (name is nameof(DocumentSession.IsEmpty)
                    or nameof(DocumentSession.Path)
                    or nameof(DocumentSession.Composition)) {
                OnPropertyChanged(nameof(HasDocument));
                OnPropertyChanged(nameof(IsStartVisible));
            }

            // Whether the bar may set a line answers from the file itself,
            // so the chips' availability walks with every door the
            // document changes files through — a plain file takes them
            // down, and a file of the app's own brings them back up.
            if (name == nameof(DocumentSession.Path)) {
                OnPropertyChanged(nameof(CanAlign));
                OnPropertyChanged(nameof(AlignHint));
                AlignLeftCommand.NotifyCanExecuteChanged();
                AlignCenterCommand.NotifyCanExecuteChanged();
                AlignRightCommand.NotifyCanExecuteChanged();
            }

            // The status bar's line-sequence segment ticks the convention
            // that is current, so the choice and the display cannot drift.
            if (name == nameof(DocumentSession.Newline)) {
                OnPropertyChanged(nameof(NewlineIsLf));
                OnPropertyChanged(nameof(NewlineIsCrLf));
            }

            // Ctrl+Z and Ctrl+Y reach the document's own history, and the
            // keys' availability moves with every move it records.
            if (name is nameof(DocumentSession.CanUndo)
                    or nameof(DocumentSession.CanRedo)) {
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
            }

            // The window's edges draw a cue when more of the file waits
            // beyond them, so the two flags move with every slide.
            if (name is nameof(DocumentSession.HasLinesAbove)
                    or nameof(DocumentSession.HasLinesBelow)) {
                OnPropertyChanged(name);
            }
        });

        Expose(typography, name => {
            // The wrap chip lights by itself, and the page's own binding
            // follows the door.
            if (name == nameof(TypographyState.WordWrap)) {
                OnPropertyChanged(nameof(WordWrap));
            }

            // The bar's three alignment chips light by the one at work, and
            // the page's own binding follows the door.
            if (name == nameof(TypographyState.TextAlign)) {
                OnPropertyChanged(nameof(TextAlign));
                OnPropertyChanged(nameof(IsLeftAligned));
                OnPropertyChanged(nameof(IsCenterAligned));
                OnPropertyChanged(nameof(IsRightAligned));
            }
            // The start screen waits on the welcome's setting too: the
            // writer who has sent the invitation away opens on the page
            // alone, and the change reaches the view the screen is bound to.
            if (name == nameof(TypographyState.ShowWelcomeMessage)) {
                OnPropertyChanged(nameof(IsStartVisible));
            }
        });
    }

    /// <summary>
    /// The document: its text, path, counts and recent files.
    /// </summary>
    public DocumentSession Document { get; }

    /// <summary>
    /// The document's typography and the colour theme.
    /// </summary>
    public TypographyState Typography { get; }

    /// <summary>
    /// Whether the editor has the caret's attention, as the view reports it.
    /// </summary>
    /// <remarks>
    /// A view-side fact, reported in rather than asked for: the TextBox's own
    /// focus is pushed here OneWayToSource, so the window tells the view model
    /// where the user is instead of the view model guessing at it. It matters
    /// because an input method composes a character before it is text — a
    /// Vietnamese writer's first "â" lives in the control and not in the
    /// document — and because clicking into a page to write on it should clear
    /// the invitation before a single character exists.
    /// </remarks>
    [ObservableProperty]
    private bool _isEditing;

    /// <summary>
    /// True while the document is empty, the editor is not being worked in,
    /// and the welcome has not been sent away — which is when the start
    /// screen is drawn over the sheet.
    /// </summary>
    /// <remarks>
    /// All three halves matter. An empty document alone is not enough: a
    /// writer composing a character through an input method has an empty
    /// buffer and is most certainly not starting. Only <c>IsNullOrEmpty</c>
    /// counts as empty — a document containing a single space is something
    /// the user typed. And the invitation has a setting of its own: the
    /// writer who has sent it away opens on the sheet alone, whatever the
    /// buffer holds, so the page answers to that as much as to the two.
    /// </remarks>
    public bool IsStartVisible => Document.IsEmpty && !IsEditing
        && Typography.ShowWelcomeMessage;

    /// <summary>
    /// A short status line for the user ("Opened x.txt", "Saved", ...).
    /// </summary>
    /// <remarks>
    /// Empty until something has happened: a fresh window has no status to
    /// report, and "Ready" was a word that took up space to say nothing.
    /// </remarks>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// True while a file is being read or written off the drawing thread and
    /// the window is veiled against being touched.
    /// </summary>
    /// <remarks>
    /// The veil is not decoration. A save takes its moves as of one instant
    /// and rebases the ledger against the text afterwards, so a keystroke
    /// landing mid-write would leave the ledger balancing on a baseline that
    /// had already moved — the file would miss the key and the clean point
    /// would stand in the wrong place. Holding the input out is what makes
    /// the two halves agree.
    /// </remarks>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// What the veil says is happening ("Saving chapter1.shiranux…").
    /// </summary>
    [ObservableProperty]
    private string _busyMessage = string.Empty;

    /// <summary>The highlight region the find leaves on the editor.</summary>
    [ObservableProperty]
    private int _selectionStart;

    [ObservableProperty]
    private int _selectionEnd;
    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

    partial void OnStatusMessageChanged(string value) =>
        OnPropertyChanged(nameof(HasStatusMessage));

    partial void OnIsEditingChanged(bool value) =>
        OnPropertyChanged(nameof(IsStartVisible));

    /// <summary>
    /// The window title: "Shiranuix — filename", prefixed with a bullet when
    /// there are unsaved changes.
    /// </summary>
    public string Title => Document.IsDirty
        ? $"Shiranuix — • {Document.Name}"
        : $"Shiranuix — {Document.Name}";

    /// <summary>
    /// True while there is a document to speak of: a file has been opened, the
    /// buffer holds something typed, or the input method is composing a
    /// character at the caret.
    /// </summary>
    /// <remarks>
    /// What the status bar's counts are shown against. A fresh window has no
    /// counts worth reading — "0 words, 0 characters, 0 lines" describes a
    /// blank sheet, not a document — so the bar stays empty until there is one.
    /// An opened but empty file counts, because the file is open. A composing
    /// keystroke counts too: with an input method on, the first character of a
    /// session lives in the composition and not yet in the text, and waiting
    /// for it to commit left the bar blank until the writer pressed space.
    /// </remarks>
    public bool HasDocument => !Document.IsEmpty
        || Document.Path is not null
        || !Document.Composition.IsEmpty;
}
