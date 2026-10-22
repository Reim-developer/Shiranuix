using Avalonia.Controls;
using Shiranuix.Graphs;
using Shiranuix.WindowBorderless;

namespace Shiranuix.Views;

/// <summary>
/// The window the relationship map is drawn in, beside the writing.
/// </summary>
/// <remarks>
/// The window builds nothing of its own: the state it binds is the whole
/// logic, and the canvas is wired to it once — from its first moment, the
/// way the file tree's panel is — so there is no later wiring step that
/// could be forgotten and leave a window that draws but cannot hear.
/// </remarks>
public partial class GraphWindow : Window {
    /// <summary>
    /// Builds the window over the graph's own state.
    /// </summary>
    /// <param name="viewModel">What the window binds and the canvas draws.</param>
    public GraphWindow(GraphState viewModel) {
        InitializeComponent();
        WindowChrome.Watch(this, TitleBand);
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        GraphPane.State = viewModel;
        Opened += (_, _) => GraphPane.Focus();
    }

    /// <summary>
    /// The constructor the XAML previewer and the runtime loader build the
    /// window with — no graph ever arrives through it, because no state
    /// exists for it to draw.
    /// </summary>
    public GraphWindow() {
        InitializeComponent();
        WindowChrome.Watch(this, TitleBand);
    }
}
