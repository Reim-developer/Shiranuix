using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Shiranuix.CoreEditor;
using Shiranuix.CoreFormat;
using Shiranuix.Models;
using Shiranuix.Services;
using Shiranuix.TextRender;
using Shiranuix.ViewModels;
using Splat;

namespace Shiranuix.Views;

/// <summary>
/// Code-behind for the shell window.
/// </summary>
/// <remarks>
/// Owns the things that must live in the view: attaching to the view model it
/// is handed, applying that view model's <see cref="ThemeMode"/> to the running
/// <see cref="Application"/>, carrying over what the input method is
/// composing — a state only the editor's own controls can see —, routing
/// the editor's keys and wheel through <see cref="EditorKeys"/>, answering
/// the window's chords through <see cref="CommandKeys"/> over
/// <see cref="KeyBindingState"/>'s bindings, and
/// following the font picker's highlight through
/// <see cref="FontPreviewWatcher"/>, which reaches into the combo box's
/// own popup tree where no binding can reach. The view model and every
/// service under it are built by the container and injected through the
/// constructor; the window no longer constructs anything itself.
/// Everything else is bound.
/// </remarks>
public partial class MainWindow : Window {
    private TypographyState? _typography;
    private ISettingsWindowService? _settings;
    private IImeCompositionGuard? _imeGuard;
    private KeyBindingState? _bindings;
    private FindState? _find;

    /// <summary>
    /// Creates the window over the view model the container built for it.
    /// </summary>
    /// <param name="viewModel">The application's view model.</param>
    /// <param name="settings">How the title band's button reaches the
    /// settings window.</param>
    /// <param name="imeGuard">How a composition the input method cannot take
    /// any further is committed before it freezes.</param>
    /// <param name="bindings">What the window's chords answer to.</param>
    public MainWindow(EditorViewModel viewModel, ISettingsWindowService settings, IImeCompositionGuard imeGuard, KeyBindingState bindings) {
        InitializeComponent();
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _imeGuard = imeGuard ?? throw new ArgumentNullException(nameof(imeGuard));
        _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        EditorComposition.Watch(Editor, ReportComposition);
        FontPreviewWatcher.Watch(FontCombo, FontPreview, FontPreviewSample);
        EditorKeys.Watch(Editor, () => DataContext as EditorViewModel);
        // The page stands each of its lines at the edge the document gave
        // that line, answered fresh every time the page is laid out.
        LineAlignment.SetProvider(Editor, AlignmentAtLine);
        TypingAssist.Watch(this, Editor, Rules);
        CommandKeys.Watch(this, () => DataContext as EditorViewModel, _bindings);
        WindowChrome.Watch(this, TitleBand);

        // Subscribe before assigning the data context, so the initial theme is
        // applied by the same code path as every later change.
        DataContextChanged += OnDataContextChanged;

        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    /// <summary>
    /// The constructor the XAML previewer builds the window with.
    /// </summary>
    /// <remarks>
    /// A view without a parameterless constructor is one the previewer refuses
    /// to build, so this overload exists for it — and it does the only thing
    /// that can be done with no container to ask: take the view model out of
    /// Splat, where the application put it the moment it created the
    /// container. Where the previewer runs without an application behind it,
    /// that returns null and the preview falls back to empty bindings rather
    /// than failing.
    ///
    /// The running application never takes this path: it goes through the
    /// constructor above.
    /// </remarks>
    public MainWindow() {
        InitializeComponent();
        EditorComposition.Watch(Editor, ReportComposition);
        FontPreviewWatcher.Watch(FontCombo, FontPreview, FontPreviewSample);
        EditorKeys.Watch(Editor, () => DataContext as EditorViewModel);
        LineAlignment.SetProvider(Editor, AlignmentAtLine);
        TypingAssist.Watch(this, Editor, Rules);
        WindowChrome.Watch(this, TitleBand);

        // Same order as the other constructor: subscribe first, assign second.
        DataContextChanged += OnDataContextChanged;

        DataContext = AppLocator.Current.GetService<EditorViewModel>();
        _settings = AppLocator.Current.GetService<ISettingsWindowService>();
        _imeGuard = AppLocator.Current.GetService<IImeCompositionGuard>();
        _bindings = AppLocator.Current.GetService<KeyBindingState>();
        if (_bindings is not null) {
            CommandKeys.Watch(this, () => DataContext as EditorViewModel, _bindings);
        }
    }

    /// <summary>
    /// Opens the settings window from the title band's button.
    /// </summary>
    private void OnOpenSettings(object? sender, RoutedEventArgs e) =>
        _settings?.Show();

    /// <summary>
    /// Puts the side column at the width the registry says it should be.
    /// </summary>
    /// <remarks>
    /// The column's width is a <c>GridLength</c>, which a bound number would
    /// have to become on every load, so it is set here instead - where the
    /// meaning is obvious and the layout type stays in the view. Zero when no
    /// panel is on show, which is what leaves the writer a full width page.
    /// </remarks>
    private void FitPanelColumn() {
        if (DataContext is not ViewModels.EditorViewModel model) {
            return;
        }

        PageRow.ColumnDefinitions[0].Width = new GridLength(
            model.Panel.IsVisible ? model.Panel.SidebarWidth : 0);
    }

    /// <summary>
    /// The panel registry changed: the side column's width follows it.
    /// </summary>
    /// <param name="sender">The registry.</param>
    /// <param name="e">Which thing about it changed.</param>
    private void OnPanelRegistryChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        if (e.PropertyName is nameof(PanelRegistry.SidebarWidth)
            or nameof(PanelRegistry.IsVisible)
            or "") {
            FitPanelColumn();
        }
    }

    /// <summary>
    /// The side bar's bar was pressed twice: put the column back to the width
    /// it started at.
    /// </summary>
    /// <param name="sender">The bar between the side column and the page.</param>
    /// <param name="e">The second press.</param>
    private void OnSidebarDoubleTapped(object? sender, TappedEventArgs e) {
        (DataContext as ViewModels.EditorViewModel)?.Panel.ResetSidebarWidth();
        FitPanelColumn();
    }

    private void FocusFindBox() {
        FindBox.Focus();
        FindBox.SelectAll();
    }

    private void OnFindBoxKeyDown(object? sender, Avalonia.Input.KeyEventArgs e) {
        if (_find is null) {
            return;
        }

        if (e.Key == Avalonia.Input.Key.Enter) {
            if (e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift)) {
                _find.Previous();
            } else {
                _find.Next();
            }

            e.Handled = true;
        } else if (e.Key == Avalonia.Input.Key.Escape) {
            _find.Close();
            Editor.Focus();
            e.Handled = true;
        }
    }

    private void CaretToView(int caret) {
        var editor = Editor;
        editor.CaretIndex = caret;
        var text = editor.Text ?? string.Empty;
        var line = text.Substring(0, Math.Min(caret, text.Length)).Count(c => c == '\n');
        editor.ScrollToLine(line);
    }

    /// <summary>
    /// Opens a file named on the command line.
    /// </summary>
    /// <param name="path">The file to open.</param>
    /// <remarks>
    /// Prompt-free by construction: <c>Shiranuix file.txt</c> is the same path
    /// Explorer takes when a document is double-clicked, and the blank
    /// document it would discard was created moments ago by this same launch
    /// and cannot have been edited.
    /// </remarks>
    public void OpenStartupFile(string path) {
        if (DataContext is EditorViewModel vm) {
            // Not awaited here: the window is being brought up, and the read
            // it starts is the one that must not hold that up — the same
            // fire-and-forget the tree's walk uses above.
            _ = vm.OpenFromCommandLineAsync(path);
        }
    }

    /// <summary>
    /// Hands the input method's composition to the view model, which knows the
    /// document the caret is in.
    /// </summary>
    /// <param name="composition">The composition as it stands.</param>
    private void ReportComposition(Composition composition) {
        // The input method stops taking a composition that outgrows it: ask it
        // to commit just under that ceiling instead, while the keys still
        // arrive, and let its own commit path land the text at the caret.
        if (_imeGuard?.ShouldComplete(composition.Text) == true &&
            TryGetPlatformHandle() is { } handle) {
            _imeGuard.Complete(handle.Handle);
        }

        if (DataContext is EditorViewModel vm) {
            vm.Composition = composition;
        }
    }

    /// <summary>
    /// Reacts to a view model being attached, applying the initial theme and
    /// subscribing to later changes.
    /// </summary>
    /// <param name="sender">The window.</param>
    /// <param name="e">Event args (unused).</param>
    private void OnDataContextChanged(object? sender, EventArgs e) {
        // Detach from a previously attached typography state, if any.
        if (_typography is not null) {
            _typography.PropertyChanged -= OnTypographyPropertyChanged;
            _typography = null;
        }

        if (DataContext is EditorViewModel vm) {
            // The theme lives on the typography state the view binds, so that is
            // where a change of it is announced.
            _typography = vm.Typography;
            _typography.PropertyChanged += OnTypographyPropertyChanged;
            ApplyTheme(_typography.ThemeMode);

            // The selection is the document's, and so is the view that draws
            // it: whatever put it there — the find bar, the tree — the box
            // shows it.
            vm.PropertyChanged += OnSelectionChanged;

            // The veil holds the whole surface still while a file is off being
            // read or written — and holding it still takes the caret's place
            // away with everything else's. The window is the one that knows
            // when to hand it back.
            vm.PropertyChanged += OnBusyChanged;

            // A document sets its lines some other way — a button pressed,
            // a file read in — and the page stands its lines up again when
            // it does, against what it would answer now.
            vm.Document.PropertyChanged += OnDocumentAlignmentsChanged;

            // The side column's width is the registry's to decide, and it says
            // so again whenever a panel arrives or the column is opened or
            // closed - which is when its width changes from nothing at all.
            vm.Panel.PropertyChanged += OnPanelRegistryChanged;
            FitPanelColumn();

            // The find bar hangs off the state behind the view model, not off
            // the typography — so its data context is said separately.
            _find = vm.Find;
            FindBarHost.DataContext = _find;
            FindBarHost.IsVisible = _find.IsOpen;
            _find.PropertyChanged += (_, e) => {
                if (e.PropertyName == nameof(FindState.IsOpen)) {
                    FindBarHost.IsVisible = _find.IsOpen;
                    if (_find.IsOpen) {
                        Avalonia.Threading.Dispatcher.UIThread.Post(FocusFindBox);
                    }
                }
            };
            _find.MatchRevealed += (s, _) =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                    CaretToView(s);
                }, Avalonia.Threading.DispatcherPriority.Input);
        }
    }

    /// <summary>
    /// What the writing turns while it is typed, read fresh from the state
    /// the page keeps: the settings may move between two strokes.
    /// </summary>
    private TypingRules? Rules() =>
        _typography is not null
            ? new TypingRules(_typography.SmartQuotes, _typography.DashMode)
            : null;

    /// <summary>
    /// Applies the theme again whenever the requested <see cref="ThemeMode"/>
    /// changes.
    /// </summary>
    /// <param name="sender">The typography state.</param>
    /// <param name="e">Property-changed args.</param>
    private void OnTypographyPropertyChanged(object? sender,
        System.ComponentModel.PropertyChangedEventArgs e) {
        if (e.PropertyName == nameof(TypographyState.ThemeMode) && _typography is not null) {
            ApplyTheme(_typography.ThemeMode);
        }
    }

    /// <summary>
    /// Shows the selection the view model says the document has.
    /// </summary>
    /// <param name="sender">The view model.</param>
    /// <param name="e">Property-changed args.</param>
    /// <remarks>
    /// Only when the box disagrees with it: the caret the writer moves is
    /// pushed back into the view model by the binding, and echoing that back
    /// would be two controls arguing over one caret.
    /// </remarks>
    private void OnSelectionChanged(object? sender,
        System.ComponentModel.PropertyChangedEventArgs e) {
        if (e.PropertyName is not (nameof(EditorViewModel.SelectionStart)
            or nameof(EditorViewModel.SelectionEnd))) {
            return;
        }

        if (sender is not EditorViewModel vm) {
            return;
        }

        if (Editor.SelectionStart != vm.SelectionStart || Editor.SelectionEnd != vm.SelectionEnd) {
            Editor.SelectionStart = vm.SelectionStart;
            Editor.SelectionEnd = vm.SelectionEnd;
        }
    }

    /// <summary>
    /// Hands the keys back to the writing surface once the veil is down.
    /// </summary>
    /// <param name="sender">The view model.</param>
    /// <param name="e">Property-changed args.</param>
    /// <remarks>
    /// The veil answers for the whole surface, so whatever was holding the
    /// caret when it came down has been put out of play and the window
    /// itself is left standing there with nothing in it. Sending the keys
    /// back to the page means the writer's next keystroke lands where they
    /// left off instead of nowhere.
    ///
    /// Only when the veil actually lifted: a file written in a breath never
    /// raises the flag at all, so a quick save leaves the caret untouched.
    /// </remarks>
    private void OnBusyChanged(object? sender,
        System.ComponentModel.PropertyChangedEventArgs e) {
        if (e.PropertyName is not nameof(EditorViewModel.IsBusy)) {
            return;
        }

        if (sender is EditorViewModel vm && !vm.IsBusy) {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Editor.Focus());
        }
    }

    /// <summary>
    /// How the page's line with a given number should stand — the edge the
    /// document gives the file line that window line stands on, answered
    /// fresh every time the page lays its lines out.
    /// </summary>
    /// <param name="line">The line's number in the window's text, from
    /// zero — the number the page counts by.</param>
    /// <returns>The edge to lay that line out at.</returns>
    private TextAlignment AlignmentAtLine(int line) =>
        (DataContext as EditorViewModel)?.Document.AlignmentAtWindowLine(line) switch {
            ParagraphAlignment.Center => TextAlignment.Center,
            ParagraphAlignment.Right => TextAlignment.Right,
            _ => TextAlignment.Left
        };

    /// <summary>
    /// The document set its lines a different way: the page stands them up
    /// again, since the layout it holds was built against the answer it
    /// gave before.
    /// </summary>
    /// <param name="sender">The document.</param>
    /// <param name="e">Property-changed args.</param>
    private void OnDocumentAlignmentsChanged(object? sender,
        System.ComponentModel.PropertyChangedEventArgs e) {
        if (e.PropertyName is nameof(DocumentSession.Alignments) or "") {
            LineAlignment.Refresh(Editor);
        }
    }

    /// <summary>
    /// Maps the requested <see cref="ThemeMode"/> onto the application's
    /// theme variant.
    /// </summary>
    /// <param name="mode">The mode to apply.</param>
    private static void ApplyTheme(ThemeMode mode) {
        if (Application.Current is null) {
            return;
        }

        Application.Current.RequestedThemeVariant = mode switch {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            // "System" follows the OS preference.
            _ => ThemeVariant.Default
        };
    }
}
