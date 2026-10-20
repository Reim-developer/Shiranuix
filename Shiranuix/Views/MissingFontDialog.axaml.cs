using Avalonia.Controls;
using Avalonia.Interactivity;
using Shiranuix.ViewModels.Dialogs;

namespace Shiranuix.Views;

/// <summary>
/// The window the missing typeface question is asked in: this file was set
/// in a face this machine does not carry, so which face should it be opened
/// in instead.
/// </summary>
/// <remarks>
/// The window holds no judgement of its own — the face chosen arrives
/// through the button and the window closes with it. Cancelled, the window
/// answers nothing, and nothing opens.
/// </remarks>
public partial class MissingFontDialog : Window {
    /// <summary>
    /// Builds the dialog over its own state.
    /// </summary>
    /// <param name="viewModel">What the dialog binds: the face missing, the words, and the faces offered.</param>
    public MissingFontDialog(MissingFontDialogViewModel viewModel) {
        InitializeComponent();
        WindowChrome.Watch(this, TitleBand);
        DataContext = viewModel;
    }

    /// <summary>
    /// The constructor the XAML previewer and the runtime loader build the
    /// window with.
    /// </summary>
    /// <remarks>
    /// Shows nothing worth answering — no question ever arrives. The running
    /// application builds the window through the constructor above, with the
    /// dialog's state already in hand.
    /// </remarks>
    public MissingFontDialog() {
        InitializeComponent();
        WindowChrome.Watch(this, TitleBand);
    }

    /// <summary>
    /// Answers with the face chosen, and the window goes.
    /// </summary>
    private void OnOpen(object? sender, RoutedEventArgs e) {
        if (DataContext is not MissingFontDialogViewModel viewModel) {
            return;
        }

        Close(viewModel.FontFamily);
    }

    /// <summary>
    /// Answers nothing: the opening is called off, and nothing lands.
    /// </summary>
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
