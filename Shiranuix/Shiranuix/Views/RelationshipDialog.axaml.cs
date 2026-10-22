using Avalonia.Controls;
using Avalonia.Interactivity;
using Shiranuix.Services.Dialogs;
using Shiranuix.ViewModels.Dialogs;
using Shiranuix.WindowBorderless;

namespace Shiranuix.Views;

/// <summary>
/// The window a tie is asked about: its two ends out of the graph's own
/// cast, and the words drawn along it.
/// </summary>
public partial class RelationshipDialog : Window {
    /// <summary>
    /// Builds the dialog over the tie's own state.
    /// </summary>
    /// <param name="viewModel">What the dialog binds: the cast, the ends, the words.</param>
    public RelationshipDialog(RelationshipDialogViewModel viewModel) {
        InitializeComponent();
        WindowChrome.Watch(this, TitleBand);
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    /// <summary>
    /// The constructor the XAML previewer and the runtime loader build the
    /// window with — no tie ever arrives through it.
    /// </summary>
    public RelationshipDialog() {
        InitializeComponent();
        WindowChrome.Watch(this, TitleBand);
    }

    /// <summary>Answers with the tie the dialog holds, if it holds one.</summary>
    private void OnConfirm(object? sender, RoutedEventArgs e) {
        if (DataContext is not RelationshipDialogViewModel viewModel) {
            return;
        }

        // The button stands disabled while the two ends are the same
        // character or not both picked; this is the same rule, kept where
        // the answer is made rather than only where the button is drawn.
        Close(viewModel.Draft);
    }

    /// <summary>Answers nothing: no tie is drawn.</summary>
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
