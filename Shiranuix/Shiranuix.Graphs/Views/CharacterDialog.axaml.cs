using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Shiranuix.Graphs.Dialogs;
using Shiranuix.WindowBorderless;

namespace Shiranuix.Graphs.Views;

/// <summary>
/// The window a character is asked about whole: their face, their name,
/// the day they were born, what the writer says about them, and anything
/// else worth keeping of them — kept beside the canvas.
/// </summary>
public partial class CharacterDialog : Window {
    private readonly IImagePicker? _picker;

    /// <summary>
    /// Builds the dialog over the character's own state.
    /// </summary>
    /// <param name="viewModel">What the dialog binds: the account, the face, the refusal.</param>
    /// <param name="picker">Asks the writer for a picture, or nothing when there is no picker to ask.</param>
    public CharacterDialog(CharacterDialogViewModel viewModel, IImagePicker? picker = null) {
        InitializeComponent();
        WindowChrome.Watch(this, TitleBand);

        // What an input method is still composing belongs to this box, not
        // to the void the focus is about to leave behind.
        ImeCommit.Guard(this);

        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _picker = picker;

        Opened += (_, _) => {
            NameBox.Focus();
            ShowFace();
        };
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

    /// <summary>
    /// Asks the writer for a picture, and shows the face it is: the
    /// picture is read at the size a face is drawn, and the letter stands
    /// in for a face that is not there.
    /// </summary>
    private async void OnChooseFace(object? sender, RoutedEventArgs e) {
        if (DataContext is not CharacterDialogViewModel viewModel || _picker is null) {
            return;
        }

        var path = await _picker.PickAsync("Choose a picture");
        if (path is null) {
            return;
        }

        var face = GraphAvatar.Read(path);
        if (face is null) {
            return;
        }

        viewModel.Avatar = face;
        ShowFace();
    }

    /// <summary>
    /// Takes the face away: the character goes back to standing for the
    /// first letter of their name.
    /// </summary>
    private void OnClearFace(object? sender, RoutedEventArgs e) {
        if (DataContext is not CharacterDialogViewModel viewModel) {
            return;
        }

        viewModel.Avatar = null;
        ShowFace();
    }

    /// <summary>
    /// Shows the face the character wears: the picture the writer chose,
    /// or the first letter of their name where there is no picture.
    /// </summary>
    private void ShowFace() {
        if (DataContext is not CharacterDialogViewModel viewModel) {
            return;
        }

        if (viewModel.Avatar is { } bytes) {
            FaceImage.Source = new Bitmap(new MemoryStream(bytes));
            FaceLetter.IsVisible = false;
            FaceImage.IsVisible = true;
            return;
        }

        FaceImage.Source = null;
        FaceImage.IsVisible = false;
        FaceLetter.IsVisible = true;
        FaceLetter.Text = GraphInitial.Of(viewModel.Name);
    }
}
