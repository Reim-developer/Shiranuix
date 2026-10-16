using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
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
        CommandKeys.Watch(this, () => DataContext as EditorViewModel, _bindings);

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
    /// The palette opened or closed: give it the keyboard while it is up, and
    /// the page's caret back when it is gone.
    /// </summary>
    /// <remarks>
    /// Both halves are here because neither is a binding. A box that is drawn
    /// on top does not take the keyboard - the page underneath would keep
    /// typing into itself while the palette waited for a name - and focus is
    /// not something a bound property hands over.
    /// </remarks>
    private void OnPaletteChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        if (e.PropertyName is not nameof(PaletteState.IsOpen)) {
            return;
        }

        if (DataContext is not ViewModels.EditorViewModel model) {
            return;
        }

        if (model.Palette.IsOpen) {
            Palette.QueryBox.Focus();
            Palette.QueryBox.SelectAll();
        } else {
            Editor.Focus();
        }
    }

    /// <summary>
    /// Puts the side column at the width the registry says it should be.
    /// </summary>
    /// <remarks>
    /// The column's width is a <c>GridLength</c>, which a bound number would
    /// have to become on every load, so it is set here instead - where the
    /// meaning is obvious and the layout type stays in the view. Zero when no
    /// panel is on show, which is what leaves an editor with no plugins a full
    /// width page.
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
        if (e.PropertyName is nameof(PluginHost.PanelRegistry.SidebarWidth)
            or nameof(PluginHost.PanelRegistry.IsVisible)
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
            // it: whatever put it there — the find bar, a plugin — the box
            // shows it.
            vm.PropertyChanged += OnSelectionChanged;

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

            // The palette is bound through the view model, but its keyboard is
            // not: a box drawn over the page does not take the caret, and the
            // page would keep typing into itself while a name was being typed.
            vm.Palette.PropertyChanged += OnPaletteChanged;
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
