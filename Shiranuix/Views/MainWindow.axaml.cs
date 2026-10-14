using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Shiranuix.Models;
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
/// following the font picker's highlight, which lives in the combo box's
/// own popup tree where no binding can reach. The view model and every
/// service under it are built by the container and injected through the
/// constructor; the window no longer constructs anything itself.
/// Everything else is bound.
/// </remarks>
public partial class MainWindow : Window {
    private TypographyState? _typography;
    private bool _fontListWired;

    /// <summary>
    /// Creates the window over the view model the container built for it.
    /// </summary>
    /// <param name="viewModel">The application's view model.</param>
    public MainWindow(EditorViewModel viewModel) {
        InitializeComponent();
        EditorComposition.Watch(Editor, ReportComposition);
        WatchFontPreview();

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
        WatchFontPreview();

        // Same order as the other constructor: subscribe first, assign second.
        DataContextChanged += OnDataContextChanged;

        DataContext = AppLocator.Current.GetService<EditorViewModel>();
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
            vm.OpenFromCommandLine(path);
        }
    }

    /// <summary>
    /// Hands the input method's composition to the view model, which knows the
    /// document the caret is in.
    /// </summary>
    /// <param name="composition">The composition as it stands.</param>
    private void ReportComposition(Composition composition) {
        if (DataContext is EditorViewModel vm) {
            vm.Composition = composition;
        }
    }

    /// <summary>
    /// Follows the font picker's highlight into the preview card: the card's
    /// place and open state first, then the list's own events.
    /// </summary>
    /// <remarks>
    /// The picker's list lives in the combo box's popup — a separate visual
    /// root — so pointer and focus over it never reach this window. The card
    /// therefore listens to the list itself, wired the first time the picker
    /// opens. Nothing here writes a font to the view model: the highlight is
    /// browsing, not a choice, and the click remains the only thing that
    /// chooses.
    /// </remarks>
    private void WatchFontPreview() {
        // The card is a sample, never a target: focus must stay with the
        // list so the keyboard can still walk it.
        Popup.SetTakesFocusFromNativeControl(FontPreview, false);
        FontPreview.PlacementTarget = FontCombo;

        FontCombo.DropDownOpened += (_, _) => {
            WireFontList();
            FontPreview.IsOpen = true;
            ShowFontPreview(FontCombo.SelectedItem as string);
        };

        FontCombo.DropDownClosed += (_, _) => FontPreview.IsOpen = false;
    }

    /// <summary>
    /// Attaches the preview's listeners to the picker's list — the popup's
    /// child, the one element through which every item under the pointer or
    /// the keyboard announces itself.
    /// </summary>
    /// <remarks>
    /// Called on every open until the template has been found. Handlers go
    /// on the child rather than on the items because items are virtualized
    /// and recycled beneath it; the child survives, so the handlers are
    /// attached exactly once and bubbling does the rest.
    /// </remarks>
    private void WireFontList() {
        if (_fontListWired) {
            return;
        }

        var popup = FontCombo.GetVisualDescendants().OfType<Popup>().FirstOrDefault();
        if (popup?.Child is not { } list) {
            return;
        }

        _fontListWired = true;
        list.AddHandler(InputElement.PointerMovedEvent, OnFontListPointerMoved, RoutingStrategies.Bubble);
        list.AddHandler(InputElement.GotFocusEvent, OnFontListGotFocus, RoutingStrategies.Bubble);
    }

    /// <summary>
    /// Draws the sample in the typeface under the mouse — hovering never
    /// selects, so the preview is the only thing that follows.
    /// </summary>
    /// <param name="sender">The picker's list.</param>
    /// <param name="e">The movement; its source is the element crossed.</param>
    private void OnFontListPointerMoved(object? sender, PointerEventArgs e) {
        if (e.Source is StyledElement { DataContext: string family }) {
            ShowFontPreview(family);
        }
    }

    /// <summary>
    /// Draws the sample in the typeface under the keyboard cursor: the
    /// arrows move focus through the list without committing a selection, so
    /// focus is the news that matters.
    /// </summary>
    /// <param name="sender">The picker's list.</param>
    /// <param name="e">The focus change; its source is the element focused.</param>
    private void OnFontListGotFocus(object? sender, FocusChangedEventArgs e) {
        if (e.Source is StyledElement { DataContext: string family }) {
            ShowFontPreview(family);
        }
    }

    /// <summary>
    /// Sets the sample's typeface — the pangram's own family, which is the
    /// one thing the card is there to show.
    /// </summary>
    /// <param name="family">The family to show, e.g. "Georgia".</param>
    /// <remarks>
    /// A family that is not installed draws in the same fallback the
    /// document would use — this is the plain constructor the converter
    /// calls — so the preview never lies about what the pick would do.
    /// </remarks>
    private void ShowFontPreview(string? family) {
        if (!string.IsNullOrWhiteSpace(family)) {
            FontPreviewSample.FontFamily = new FontFamily(family);
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
