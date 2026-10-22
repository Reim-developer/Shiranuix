using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Shiranuix.Graphs.Dialogs;
using Shiranuix.WindowBorderless;

namespace Shiranuix.Graphs.Views;

/// <summary>
/// The picker: which character of the wiki stands in the map.
/// </summary>
/// <remarks>
/// The window asks two questions of the state it binds — which character
/// was chosen, and whether the writer wants the wiki itself — and answers
/// with the one that was chosen. The wiki is opened by the window rather
/// than by the state, because opening a window is the window's own work.
/// </remarks>
public partial class WikiPickerDialog : Window {
    private readonly IWikiWindowService? _wikiWindows;

    /// <summary>
    /// Builds the picker over the wiki it offers.
    /// </summary>
    /// <param name="viewModel">What the window binds.</param>
    /// <param name="wikiWindows">Where the wiki is opened when it is asked for.</param>
    public WikiPickerDialog(WikiPickerViewModel viewModel, IWikiWindowService wikiWindows) {
        InitializeComponent();
        WindowChrome.Watch(this, TitleBand);
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _wikiWindows = wikiWindows ?? throw new ArgumentNullException(nameof(wikiWindows));

        viewModel.WikiAsked += () => _wikiWindows?.Show();
    }

    /// <summary>
    /// The constructor the XAML previewer and the runtime loader build the
    /// window with — no wiki ever arrives through it, because no state
    /// exists for it to offer.
    /// </summary>
    public WikiPickerDialog() {
        InitializeComponent();
        WindowChrome.Watch(this, TitleBand);
    }

    /// <summary>The writer walked away: nobody stands in the map.</summary>
    /// <param name="sender">The button that was pressed.</param>
    /// <param name="e">The press's own event.</param>
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);

    /// <summary>The chosen character stands in the map.</summary>
    /// <param name="sender">The button that was pressed.</param>
    /// <param name="e">The press's own event.</param>
    private void OnConfirm(object? sender, RoutedEventArgs e) {
        if (DataContext is not WikiPickerViewModel viewModel || viewModel.Selected is not { } chosen) {
            return;
        }

        Close(chosen);
    }
}
