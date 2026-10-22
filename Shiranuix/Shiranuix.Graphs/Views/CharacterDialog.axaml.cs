using Avalonia.Controls;
using Avalonia.Interactivity;
using Shiranuix.Graphs.Dialogs;
using Shiranuix.WindowBorderless;

namespace Shiranuix.Graphs.Views;

/// <summary>
/// The window a character is asked about whole: their name and the writer's
/// note, kept beside the canvas.
/// </summary>
public partial class CharacterDialog : Window {
    /// <summary>
    /// Builds the dialog over the character's own state.
    /// </summary>
    /// <param name="viewModel">What the dialog binds: the name, the note, the refusal.</param>
    public CharacterDialog(CharacterDialogViewModel viewModel) {
        InitializeComponent();
        WindowChrome.Watch(this, TitleBand);
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        Opened += (_, _) => NameBox.Focus();
    }

    /// <summary>
    /// The constructor the XAML previewer and the runtime loader build the
    /// window with — no character ever arrives through it.
    /// </summary>
    public CharacterDialog() {
        InitializeComponent();
        WindowChrome.Watch(this, TitleBand);
    }

    /// <summary>Answers with the character as the dialog holds them.</summary>
    private void OnConfirm(object? sender, RoutedEventArgs e) {
        if (DataContext is not CharacterDialogViewModel viewModel) {
            return;
        }

        Close(viewModel.Draft);
    }

    /// <summary>Answers nothing: the character stands as they were.</summary>
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
