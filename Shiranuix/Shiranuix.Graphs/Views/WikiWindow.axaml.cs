using Avalonia.Controls;
using Avalonia.Markup.Xaml;
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
/// application, and the window is where it must stand.
/// </remarks>
public partial class WikiWindow : Window {
    /// <summary>
    /// Builds the window over the wiki's own state.
    /// </summary>
    /// <param name="viewModel">What the window binds.</param>
    public WikiWindow(WikiState viewModel) {
        InitializeComponent();
        WindowChrome.Watch(this, TitleBand);
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
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
}
