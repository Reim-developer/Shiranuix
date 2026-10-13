using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Shiranuix.Models;
using Shiranuix.Services;
using Shiranuix.ViewModels;

namespace Shiranuix.Views;

/// <summary>
/// Code-behind for the shell window.
/// </summary>
/// <remarks>
/// Owns the three things that must live in the view: constructing the real
/// file and dialog services (they need the window), handing them to the view
/// model, and applying the view model's <see cref="ThemeMode"/> to the running
/// <see cref="Application"/>. Everything else is bound.
/// </remarks>
public partial class MainWindow : Window {
    private EditorViewModel? _viewModel;

    /// <summary>
    /// Creates the window and wires services and theme to the view model.
    /// </summary>
    public MainWindow() {
        InitializeComponent();

        // Subscribe before assigning the data context, so the initial theme is
        // applied by the same code path as every later change.
        DataContextChanged += OnDataContextChanged;

        DataContext = new EditorViewModel(
            new FileService(),
            new WindowFileDialogService(this),
            new WindowConfirmService(this));
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
