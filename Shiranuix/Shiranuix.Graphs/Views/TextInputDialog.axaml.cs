using Avalonia.Controls;
using Avalonia.Interactivity;
using Shiranuix.Graphs.Dialogs;
using Shiranuix.WindowBorderless;

namespace Shiranuix.Graphs.Views;

/// <summary>
/// The window the one-word question is asked in.
/// </summary>
/// <remarks>
/// The window keeps no judgement of its own — the answer is whatever stood
/// in the box when OK was pressed, and nothing when the writer walked away.
/// </remarks>
public partial class TextInputDialog : Window {
    /// <summary>
    /// Builds the dialog over the question's own state.
    /// </summary>
    /// <param name="viewModel">What the dialog binds: the word and the refusal.</param>
    public TextInputDialog(TextInputDialogViewModel viewModel) {
        InitializeComponent();
        WindowChrome.Watch(this, TitleBand);

        // What an input method is still composing belongs to this box, not
        // to the void the focus is about to leave behind.
        ImeCommit.Guard(this);

        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        Opened += (_, _) => ValueBox.Focus();
    }

    /// <summary>
    /// The constructor the XAML previewer and the runtime loader build the
    /// window with — showing nothing worth answering, because no question
    /// ever arrives through it.
    /// </summary>
    public TextInputDialog() {
        InitializeComponent();
        WindowChrome.Watch(this, TitleBand);
    }

    /// <summary>Answers with the word in the box, and the window goes.</summary>
    private void OnConfirm(object? sender, RoutedEventArgs e) {
        if (DataContext is not TextInputDialogViewModel viewModel) {
            return;
        }

        Close(viewModel.Text);
    }

    /// <summary>Answers nothing: the writer is not answering after all.</summary>
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
