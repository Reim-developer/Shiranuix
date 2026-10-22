using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Shiranuix.WindowBorderless;

namespace Shiranuix.Graphs.Views;

/// <summary>
/// The window the relationship map is drawn in, beside the writing.
/// </summary>
/// <remarks>
/// The window builds almost nothing of its own: the state it binds is the
/// whole logic, and the canvas is wired to it once — from its first moment,
/// the way the file tree's panel is — so there is no later wiring step that
/// could be forgotten and leave a window that draws but cannot hear. The
/// one thing it stands up itself is the box that rewrites a tie's words
/// where they are: the state says which tie, the canvas says where its
/// words stand, and this puts the box there and takes it away again when
/// the words are answered or the asking is dropped.
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

        // The words box follows the state's own word: a tie pointed at on
        // the paper opens it over that tie's pill, and every way the
        // asking can end takes it away again.
        viewModel.PropertyChanged += (_, e) => {
            if (e.PropertyName == nameof(GraphState.EditingLabel)) {
                ShowWordsBox(viewModel);
            }
        };

        // Walking away takes the words typed — the ordinary way an editor
        // agrees with the writer, with Enter as its hurry and Escape as
        // its refusal.
        WordsEdit.LostFocus += (_, _) => {
            if (viewModel.EditingLabel is not null) {
                viewModel.CommitLabelEdit(WordsEdit.Text);
            }
        };

        WordsEdit.AddHandler(
            KeyDownEvent,
            (_, e) => {
                if (viewModel.EditingLabel is null) {
                    return;
                }

                if (e.Key is Key.Enter) {
                    viewModel.CommitLabelEdit(WordsEdit.Text);
                    e.Handled = true;
                } else if (e.Key is Key.Escape) {
                    viewModel.CancelLabelEdit();
                    e.Handled = true;
                }
            },
            RoutingStrategies.Bubble,
            handledEventsToo: true);
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

    /// <summary>
    /// Stands the words box over the pill whose tie is being rewritten, or
    /// takes it away again when the writing is done — either way handing
    /// the hand back to the paper, which is where it was before.
    /// </summary>
    /// <param name="viewModel">The state whose words box this is.</param>
    private void ShowWordsBox(GraphState viewModel) {
        if (viewModel.EditingLabel is not { } edge) {
            if (WordsEdit.IsVisible) {
                WordsEdit.IsVisible = false;
                GraphPane.Focus();
            }

            return;
        }

        if (!GraphPane.TryGetLabelBox(edge, out var box)) {
            // No pill to stand over — the tie has lost its words or its
            // line somewhere, and there is nothing here to rewrite.
            viewModel.CancelLabelEdit();
            return;
        }

        WordsEdit.Margin = new Thickness(box.X, box.Y, 0, 0);
        WordsEdit.Width = Math.Max(box.Width, 120);
        WordsEdit.Text = edge.Label;
        WordsEdit.IsVisible = true;
        WordsEdit.Focus();
        WordsEdit.SelectAll();
    }
}
