using Avalonia.Controls;
using Avalonia.Input;
using Narika.ViewModels;
using Shiranuix.WindowBorderless;

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
        WindowChrome.Watch(this, TitleBand);
    }

    /// <summary>
    /// The launcher's window: the view model arrives from the container,
    /// which is what keeps every command the buttons bind to wired to the
    /// services they are meant to reach.
    /// </summary>
    /// <param name="viewModel">The launcher this window draws.</param>
    public MainWindow(LauncherViewModel viewModel) {
        InitializeComponent();
        WindowChrome.Watch(this, TitleBand);

        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    /// <summary>
    /// The whole card is the door: a press anywhere on it is asked of the
    /// view model the card is drawn over, which knows what that press
    /// means — the wizard for a machine with nothing, the panel for one
    /// with something — and whether it may be answered at all right now.
    /// </summary>
    private void OnCardTapped(object? sender, TappedEventArgs e) {
        if (DataContext is LauncherViewModel launcher && launcher.OpenCommand.CanExecute(null)) {
            launcher.OpenCommand.Execute(null);
        }
    }
}
