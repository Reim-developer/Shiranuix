using Avalonia;
using Avalonia.Controls;
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
/// the editor's keys and wheel through <see cref="EditorKeys"/>, and
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
        EditorKeys.Watch(Editor, () => DataContext as EditorViewModel);

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
