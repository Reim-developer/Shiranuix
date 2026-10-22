using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Shiranuix.WindowBorderless;

namespace Shiranuix.Graphs.Views;

/// <summary>
/// The window the relationship map is drawn in, beside the writing.
/// </summary>
/// <remarks>
/// <para>
/// The window builds almost nothing of its own: the state it binds is the
/// whole logic, and the canvas is wired to it once — from its first moment,
/// the way the file tree's panel is — so there is no later wiring step that
/// could be forgotten and leave a window that draws but cannot hear.
/// </para>
/// <para>
/// The one thing it stands up itself is the box that rewrites a tie's words
/// where they are: the state says which tie, the canvas says where its
/// words stand, and this puts the box there and takes it away again when
/// the words are answered or the asking is dropped.
/// </para>
/// <para>
/// And the card: what the writer knows about the character the hand is
/// resting on, said beside the node rather than in a window of its own —
/// a hand that has come to rest is asking a quiet question, and a quiet
/// question deserves a quiet answer.
/// </para>
/// </remarks>
public partial class GraphWindow : Window {
    /// <summary>
    /// How long a hand must rest on a character before the paper says
    /// anything about them: long enough that a hand passing over the
    /// paper is not a hand asking.
    /// </summary>
    private static readonly TimeSpan CardDelay = TimeSpan.FromMilliseconds(400);

    private CancellationTokenSource? _cardWait;

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
            } else if (e.PropertyName == nameof(GraphState.HoveredNode)) {
                WatchHoveredNode(viewModel);
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
    /// Watches what the hand is resting on: a character under the hand
    /// starts the card's waiting, and a hand that has left takes the
    /// card away with it.
    /// </summary>
    /// <param name="viewModel">The state the canvas reports the hand to.</param>
    private void WatchHoveredNode(GraphState viewModel) {
        // Whatever was waiting is not waiting any more: a hand that has
        // moved on has not settled anywhere.
        _cardWait?.Cancel();
        _cardWait = null;

        if (viewModel.HoveredNode is null) {
            CharacterCard.IsVisible = false;
            return;
        }

        // The card waits for the hand to settle: a hand passing over the
        // paper is not a hand asking about anybody.
        _cardWait = new CancellationTokenSource();
        _ = ShowCardWhenSettledAsync(viewModel, _cardWait.Token);
    }

    /// <summary>
    /// Stands the card beside the character the hand has come to rest on —
    /// if the hand is still there when the waiting is over.
    /// </summary>
    /// <param name="viewModel">The state the card reads.</param>
    /// <param name="token">The waiting's own ending, should the hand go.</param>
    private async Task ShowCardWhenSettledAsync(GraphState viewModel, CancellationToken token) {
        try {
            await Task.Delay(CardDelay, token);
        } catch (TaskCanceledException) {
            return;
        }

        // The card is drawn on the paper's own thread, whatever thread the
        // waiting was on.
        await Dispatcher.UIThread.InvokeAsync(() => {
            if (!token.IsCancellationRequested) {
                ShowCard(viewModel);
            }
        });
    }

    /// <summary>
    /// Stands the card beside the character the hand is resting on, saying
    /// the four things the wiki knows about them — and "None" for the ones
    /// nobody has filled in, which is not the same as nothing at all.
    /// </summary>
    /// <param name="viewModel">The state the card reads.</param>
    private void ShowCard(GraphState viewModel) {
        if (viewModel.HoveredNode is not { } node) {
            CharacterCard.IsVisible = false;
            return;
        }

        // The face the character wears: the picture the wiki holds, or the
        // first letter of their name where there is no picture.
        var face = GraphPane.Images.Face(node.Id, node.Avatar);
        if (face is not null) {
            CardFace.Source = face;
            CardFace.IsVisible = true;
            CardInitial.IsVisible = false;
        } else {
            CardFace.Source = null;
            CardFace.IsVisible = false;
            CardInitial.IsVisible = true;
            CardInitial.Text = node.Initial;
        }

        CardName.Text = node.Name;
        CardBirthDate.Text = OrNone(node.BirthDate);
        CardDescription.Text = OrNone(node.Description);
        CardNote.Text = OrNone(node.Note);

        // The card stands beside the node, and never off the paper: the
        // glass's own edge is no place to lose half a card — so the card
        // turns to the other side of the node rather than run off it, and
        // is held inside the glass even then.
        var at = GraphPane.Viewport.ToScreen(new Point(node.X, node.Y));
        var gap = GraphMetrics.NodeRadius * GraphPane.Viewport.Zoom + 12;

        var left = at.X + gap;
        if (left + CardWidth > GraphPane.Bounds.Width - CardMargin) {
            left = at.X - gap - CardWidth;
        }

        left = Math.Clamp(left, CardMargin, Math.Max(CardMargin, GraphPane.Bounds.Width - CardWidth - CardMargin));
        var top = Math.Clamp(at.Y - 40, CardMargin, Math.Max(CardMargin, GraphPane.Bounds.Height - CardHeight - CardMargin));

        CharacterCard.Margin = new Thickness(left, top, 0, 0);
        CharacterCard.IsVisible = true;
    }

    /// <summary>How wide the card stands.</summary>
    private const double CardWidth = 300;

    /// <summary>
    /// How tall the card stands, as far as the paper can tell: the rows
    /// are ruled and the writing is trimmed, so the card is about this
    /// tall whatever the writer wrote.
    /// </summary>
    private const double CardHeight = 170;

    /// <summary>The room the card keeps from the glass's own edge.</summary>
    private const double CardMargin = 8;

    /// <summary>
    /// What a field nobody has filled in says: not nothing, which would
    /// read as a blank line, but the word the writer expects.
    /// </summary>
    /// <param name="value">What the field holds.</param>
    /// <returns>The field's own words, or "None".</returns>
    private static string OrNone(string value) =>
        value.Length == 0 ? "None" : value;

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
