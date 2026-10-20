using Avalonia.Controls;
using Narika.ViewModels;

namespace Narika;

public partial class MainWindow : Window {
    /// <summary>
    /// For the XAML runtime loader and the design surface: they can only
    /// ever find a constructor that asks for nothing, and this is that
    /// one. The window that actually runs is the constructor beside it —
    /// the one the application resolves from its container.
    /// </summary>
    public MainWindow() {
        InitializeComponent();
    }

    /// <summary>
    /// The launcher's window: the view model arrives from the container,
    /// which is what keeps every command the buttons bind to wired to the
    /// services they are meant to reach.
    /// </summary>
    /// <param name="viewModel">The launcher this window draws.</param>
    public MainWindow(LauncherViewModel viewModel) {
        InitializeComponent();

        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }
}
