using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Shiranuix.Graphs.Dialogs;
using Shiranuix.WindowBorderless;

namespace Shiranuix.Graphs.Views;

/// <summary>
/// The wiki in its own window: the characters on the left, the one the
/// writer has chosen on the right.
/// </summary>
/// <remarks>
/// The window builds almost nothing of its own — the state it binds is the
/// whole logic — and the one thing it stands up itself is the question
/// asked before a character is taken away: the prompt belongs to the
/// application, and the window is where it must stand. And the face: a
/// picture is chosen here, and the letter stands in for a face that is not
/// there.
/// </remarks>
public partial class WikiWindow : Window {
    private readonly IImagePicker? _picker;

    /// <summary>
    /// Builds the window over the wiki's own state.
    /// </summary>
    /// <param name="viewModel">What the window binds.</param>
    /// <param name="picker">Asks the writer for a picture, or nothing when there is no picker to ask.</param>
    /// <param name="imeGuard">The guard that commits a composition the IME can
    /// no longer hold, or nothing when the application has none to give.</param>
    public WikiWindow(WikiState viewModel, IImagePicker? picker = null, IImeCompositionGuard? imeGuard = null) {
        InitializeComponent();
        WindowChrome.Watch(this, TitleBand);
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _picker = picker;

        // What an input method is still composing belongs to this box, not
        // to the void the focus is about to leave behind — and a composition
        // the IME can no longer hold is committed the moment it outgrows it,
        // rather than freezing the text where it stands.
        ImeCommit.Guard(this, imeGuard);

        viewModel.PropertyChanged += (_, e) => {
            if (e.PropertyName is nameof(WikiState.Avatar) or nameof(WikiState.Name)) {
                ShowFace();
            }
        };

        Opened += (_, _) => ShowFace();
    }

    /// <summary>
    /// The constructor the XAML previewer and the runtime loader build the
    /// window with — no wiki ever arrives through it, because no state
    /// exists for it to show.
    /// </summary>
    public WikiWindow() {
        InitializeComponent();
        WindowChrome.Watch(this, TitleBand);
    }

    /// <summary>
    /// Asks the writer for a picture, and shows the face it is: the
    /// picture is read at the size a face is drawn, and the letter stands
    /// in for a face that is not there.
    /// </summary>
    private async void OnChooseFace(object? sender, RoutedEventArgs e) {
        if (DataContext is not WikiState state || _picker is null) {
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

        state.Avatar = face;
        ShowFace();
    }

    /// <summary>
    /// Takes the face away: the character goes back to standing for the
    /// first letter of their name.
    /// </summary>
    private void OnClearFace(object? sender, RoutedEventArgs e) {
        if (DataContext is not WikiState state) {
            return;
        }

        state.Avatar = null;
        ShowFace();
    }

    /// <summary>
    /// Shows the face the character wears: the picture the writer chose,
    /// or the first letter of their name where there is no picture.
    /// </summary>
    private void ShowFace() {
        if (DataContext is not WikiState state) {
            return;
        }

        if (state.Avatar is { } bytes) {
            FaceImage.Source = new Bitmap(new MemoryStream(bytes));
            FaceLetter.IsVisible = false;
            FaceImage.IsVisible = true;
            return;
        }

        FaceImage.Source = null;
        FaceImage.IsVisible = false;
        FaceLetter.IsVisible = true;
        FaceLetter.Text = GraphInitial.Of(state.Name);
    }
}
