using Avalonia.Controls;
using Narika.ViewModels;

namespace Narika;

public partial class SetupWindow : Window {
    /// <summary>
    /// For the XAML runtime loader and the design surface: they can only
    /// ever find a constructor that asks for nothing, and this is that
    /// one. The window that actually runs is the constructor beside it —
    /// the one the setup service uses, which is why this one leaves the
    /// view model where it finds it: nowhere.
    /// </summary>
    public SetupWindow() {
        InitializeComponent();
    }

    /// <summary>
    /// The wizard's window: the view model arrives from the container —
    /// it builds this window, so the window asks for it rather than
    /// building it — and the window listens for the one event that ends
    /// the walk, which is the only thing a view model here is not allowed
    /// to do for itself: touch the window it does not know exists.
    /// </summary>
    /// <param name="viewModel">The wizard this window draws.</param>
    public SetupWindow(InstallerViewModel viewModel) {
        InitializeComponent();

        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        viewModel.ShutdownRequested += (_, _) => Close();
    }
}
