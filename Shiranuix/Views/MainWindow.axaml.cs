using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Shiranuix.Models;
using Shiranuix.ViewModels;
using Splat;

namespace Shiranuix.Views;

/// <summary>
/// Code-behind for the shell window.
/// </summary>
/// <remarks>
/// Owns the two things that must live in the view: attaching to the view
/// model it is handed, and applying that view model's
/// <see cref="ThemeMode"/> to the running <see cref="Application"/>. The view
/// model and every service under it are built by the container and injected
/// through the constructor; the window no longer constructs anything itself.
/// Everything else is bound.
/// </remarks>
public partial class MainWindow : Window {
    private EditorViewModel? _viewModel;

    /// <summary>
    /// Creates the window over the view model the container built for it.
    /// </summary>
    /// <param name="viewModel">The application's view model.</param>
    public MainWindow(EditorViewModel viewModel) {
        InitializeComponent();

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
    /// Reacts to a view model being attached, applying the initial theme and
    /// subscribing to later changes.
    /// </summary>
    /// <param name="sender">The window.</param>
    /// <param name="e">Event args (unused).</param>
    private void OnDataContextChanged(object? sender, EventArgs e) {
        // Detach from a previously attached view model, if any.
        if (_viewModel is not null) {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (DataContext is EditorViewModel vm) {
            _viewModel = vm;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            ApplyTheme(vm.ThemeMode);
        } else {
            _viewModel = null;
        }
    }

    /// <summary>
    /// Applies the theme again whenever <see cref="EditorViewModel.ThemeMode"/>
    /// changes.
    /// </summary>
    /// <param name="sender">The view model.</param>
    /// <param name="e">Property-changed args.</param>
    private void OnViewModelPropertyChanged(object? sender,
        System.ComponentModel.PropertyChangedEventArgs e) {
        if (e.PropertyName == nameof(EditorViewModel.ThemeMode) && _viewModel is not null) {
            ApplyTheme(_viewModel.ThemeMode);
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
