using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Shiranuix.Models;
using Shiranuix.Services;
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
/// composing — a state only the editor's own controls can see — and
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

    /// <summary>The editor's own scroll viewer, found once it exists.</summary>
    private ScrollViewer? _editorScroller;

    /// <summary>How many lines one turn of the wheel slides the window.</summary>
    private const int WheelLines = 3;

    /// <summary>
    /// How close to a scroll's edge counts as being at it — the last half
    /// pixel is usually rounding rather than a position.
    /// </summary>
    private const double ScrollEdge = 0.5;

    /// <summary>
    /// Creates the window over the view model the container built for it.
    /// </summary>
    /// <param name="viewModel">The application's view model.</param>
    /// <param name="settings">How the title band's button reaches the
    /// settings window.</param>
    /// <param name="imeGuard">How a composition the input method cannot take
    /// any further is committed before it freezes.</param>
    public MainWindow(EditorViewModel viewModel, ISettingsWindowService settings, IImeCompositionGuard imeGuard) {
        InitializeComponent();
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _imeGuard = imeGuard ?? throw new ArgumentNullException(nameof(imeGuard));
        EditorComposition.Watch(Editor, ReportComposition);
        FontPreviewWatcher.Watch(FontCombo, FontPreview, FontPreviewSample);
        WireEditorKeys();

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
        WireEditorKeys();

        // Same order as the other constructor: subscribe first, assign second.
        DataContextChanged += OnDataContextChanged;

        DataContext = AppLocator.Current.GetService<EditorViewModel>();
        _settings = AppLocator.Current.GetService<ISettingsWindowService>();
        _imeGuard = AppLocator.Current.GetService<IImeCompositionGuard>();
    }

    /// <summary>
    /// Opens the settings window from the title band's button.
    /// </summary>
    private void OnOpenSettings(object? sender, RoutedEventArgs e) =>
        _settings?.Show();

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
            vm.OpenFromCommandLine(path);
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
    /// Routes the keys and the wheel the document answers itself over the
    /// editor: the history's own three, the window's page keys, the
    /// whole-file home and end, and the wheel that turns the window once
    /// the box has no scrolling room left.
    /// </summary>
    /// <remarks>
    /// The text box's undo is switched off in the markup, because its stacks
    /// die the moment the whole text is replaced from outside — which is what
    /// opening a file does and what moving the window over a large document
    /// does. The keys are consumed even when there is nothing to take
    /// back: the boxes must not fall through to a history that no longer
    /// exists, and a key with no move behind it should simply do nothing.
    ///
    /// The handlers run even when the text box got there first
    /// (<c>handledEventsToo</c>) and read the scroll position as they find
    /// it: the window's travel is asked for only when the box has no room
    /// left to move in that direction, so the two never travel at once —
    /// whichever of them the control lets through first.
    /// </remarks>
    private void WireEditorKeys() {
        Editor.AddHandler(
            InputElement.KeyDownEvent,
            OnEditorKeyDown,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        Editor.AddHandler(
            InputElement.PointerWheelChangedEvent,
            OnEditorPointerWheel,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
    }

    /// <summary>
    /// Handles one of the keys the document answers itself over the editor:
    /// the history's three, the window's two page keys, and the whole-file
    /// home and end.
    /// </summary>
    /// <param name="sender">The editor.</param>
    /// <param name="e">The key press; consumed when it was one of those.</param>
    private void OnEditorKeyDown(object? sender, KeyEventArgs e) {
        if (DataContext is not EditorViewModel vm) {
            return;
        }

        // The whole-file jumps: the window goes to the file's first or last
        // stretch and the caret to the end that shows — however far the two
        // of them have travelled before landing. The caret is set after the
        // window, so what lands on screen is where the jump puts it rather
        // than where the box's own key left it.
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key is Key.Home or Key.End) {
            if (e.Key == Key.Home) {
                vm.SlideToStart();
                vm.CaretIndex = 0;
            } else {
                vm.SlideToEnd();
                vm.CaretIndex = vm.Text.Length;
            }

            e.Handled = true;
            return;
        }

        // The page keys turn whole windows — but only once the box has no
        // page of its own left to turn in that direction.
        switch (e.Key) {
            case Key.PageDown when AtScrollLimit(down: true):
                vm.SlideBy(vm.PageLines);
                e.Handled = true;
                return;
            case Key.PageUp when AtScrollLimit(down: false):
                vm.SlideBy(-vm.PageLines);
                e.Handled = true;
                return;
        }

        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) {
            return;
        }

        switch (e.Key) {
            case Key.Z when !e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                vm.Undo();
                e.Handled = true;
                break;
            case Key.Z:
            case Key.Y:
                vm.Redo();
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// Turns one wheel notch into a window slide — once the box has run out
    /// of scrolling room in that direction, so its own scrolling stays the
    /// thing that happens first.
    /// </summary>
    /// <param name="sender">The editor.</param>
    /// <param name="e">The wheel turn; the delta's sign says which way.</param>
    /// <remarks>
    /// A notch is three lines — small enough to read what passed, large
    /// enough to feel like travel. The delta is read the way the box reads
    /// it: positive is up the file. A turn too small to name rounds to
    /// nothing and is left alone, which is the right answer for the last
    /// drift of a trackpad's momentum.
    /// </remarks>
    private void OnEditorPointerWheel(object? sender, PointerWheelEventArgs e) {
        if (DataContext is not EditorViewModel vm) {
            return;
        }

        var lines = (int)Math.Round(e.Delta.Y * WheelLines);

        if (lines == 0 || !AtScrollLimit(down: lines < 0)) {
            return;
        }

        vm.SlideBy(-lines);
        e.Handled = true;
    }

    /// <summary>
    /// Whether the editor's own scroll has nothing left to give in a
    /// direction — the edge past which the window's slide is the only
    /// travel left.
    /// </summary>
    /// <param name="down">True for the file's end, false for its
    /// beginning.</param>
    /// <returns>True when the scroll stands at that edge.</returns>
    /// <remarks>
    /// The scroll viewer lives in the text box's template, so it cannot be
    /// named from the markup and is found through the visuals once. A box
    /// with no room at all — fewer lines than the viewport — counts as
    /// being at both edges, which is right: there is nothing to scroll, so
    /// a turn is asking the window directly. Before the template exists the
    /// same answer stands; the wheel cannot turn itself.
    /// </remarks>
    private bool AtScrollLimit(bool down) {
        var scroller = _editorScroller ??= Editor.GetVisualDescendants()
            .OfType<ScrollViewer>()
            .FirstOrDefault();

        if (scroller is null) {
            return true;
        }

        var max = Math.Max(scroller.Extent.Height - scroller.Viewport.Height, 0);

        return down
            ? scroller.Offset.Y >= max - ScrollEdge
            : scroller.Offset.Y <= ScrollEdge;
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
        }
    }

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
