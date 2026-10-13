using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Shiranuix.Models;
using Shiranuix.ViewModels;

namespace Shiranuix.Views;

/// <summary>
/// Code-behind for the shell window.
/// </summary>
/// <remarks>
/// Owns two view concerns: applying the view model's requested
/// <see cref="ThemeMode"/> to the running <see cref="Application"/>, and (via
/// the bound ContentControl) displaying whichever screen is current. All
/// navigation state lives in <see cref="MainViewModel"/>.
/// </remarks>
public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;

    /// <summary>
    /// Creates the window and wires theme application to the data context.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>
    /// Reacts to the shell view model being attached, applying the initial
    /// theme and subscribing to later changes.
    /// </summary>
    /// <param name="sender">The window.</param>
    /// <param name="e">Event args (unused).</param>
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Detach from a previously attached view model, if any.
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (DataContext is MainViewModel vm)
        {
            _viewModel = vm;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            ApplyTheme(vm.ThemeMode);
        }
        else
        {
            _viewModel = null;
        }
    }

    /// <summary>
    /// Applies the theme again whenever <see cref="MainViewModel.ThemeMode"/>
    /// changes.
    /// </summary>
    /// <param name="sender">The view model.</param>
    /// <param name="e">Property-changed args.</param>
    private void OnViewModelPropertyChanged(object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.ThemeMode) && _viewModel is not null)
        {
            ApplyTheme(_viewModel.ThemeMode);
        }
    }

    /// <summary>
    /// Maps the requested <see cref="ThemeMode"/> onto the application's
    /// theme variant.
    /// </summary>
    /// <param name="mode">The mode to apply.</param>
    private static void ApplyTheme(ThemeMode mode)
    {
        if (Application.Current is null)
        {
            return;
        }

        Application.Current.RequestedThemeVariant = mode switch
        {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            // "System" follows the OS preference.
            _ => ThemeVariant.Default
        };
    }
}
