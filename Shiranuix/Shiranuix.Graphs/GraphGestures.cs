using Avalonia;

namespace Shiranuix.Graphs;

/// <summary>
/// What a press on the glass means: nothing yet, the words of a tie opened
/// where they stand, a node's small vocabulary, a node or a tie asking
/// what it says, a node taken in its middle, a tie taken along its line,
/// a tie dragged out of a rim, or the paper itself.
/// </summary>
public enum GraphGesture {
    /// <summary>Nothing happened, or the hand is resting on the paper.</summary>
    None,

    /// <summary>The words of a tie stand under the hand: the box opens there.</summary>
    OpenWords,

    /// <summary>The node under the hand asks its small vocabulary.</summary>
    NodeMenu,

    /// <summary>The node under the hand is asked what it says.</summary>
    EditNode,

    /// <summary>The tie under the hand is asked what it says.</summary>
    EditEdge,

    /// <summary>A node is taken in its middle and goes where the hand goes.</summary>
    Drag,

    /// <summary>A tie is taken along its line and bows where the hand holds it.</summary>
    Bend,

    /// <summary>A tie is dragged out of a node's rim.</summary>
    Link,

    /// <summary>The paper itself is taken and moves under the hand.</summary>
    Pan
}

/// <summary>
/// What a press, a move or a release of the glass came to: the meaning of
/// the gesture, and the thing it was about.
/// </summary>
/// <param name="Gesture">What the hand meant.</param>
/// <param name="Node">The node it was about, if any.</param>
/// <param name="Edge">The tie it was about, if any.</param>
public readonly record struct GraphGestureResult(
    GraphGesture Gesture,
    GraphNodeViewModel? Node = null,
    GraphEdgeViewModel? Edge = null);

/// <summary>
/// The map's own vocabulary of presses: what a hand pressing, moving and
/// letting go of the glass means, and what it leaves behind.
/// </summary>
/// <remarks>
/// The gestures live apart from the canvas so that the map's own ways of
/// being touched can be asked about without a window being opened to look:
/// a press on a node's rim, a drag along a tie's line, a tie dragged from
/// one rim to another. The canvas keeps only what is the glass's own
/// business — the pointer, the cursor, the drawing.
/// </remarks>
public sealed class GraphGestures {
    private readonly IGraphHitTester _hits;
    private readonly IGraphEdgeGeometry _geometry;
    private readonly IGraphViewport _viewport;

    private GraphNodeViewModel? _dragging;
    private GraphEdgeViewModel? _bending;
    private GraphNodeViewModel? _linking;
    private GraphNodeViewModel? _linkHover;
    private Point _linkCursor;
    private bool _panning;
    private bool _moved;
    private Point _gestureStart;

    /// <summary>
    /// Creates the gestures over the map's own pieces.
    /// </summary>
    /// <param name="hits">What stands under the hand.</param>
    /// <param name="geometry">Where the ties' lines run.</param>
    /// <param name="viewport">Where the world stands on the glass.</param>
    public GraphGestures(IGraphHitTester hits, IGraphEdgeGeometry geometry, IGraphViewport viewport) {
        _hits = hits ?? throw new ArgumentNullException(nameof(hits));
        _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
    }

    /// <summary>
    /// The tie being dragged out of a node's rim, if one is being dragged.
    /// </summary>
    public GraphLinkPreview? Link =>
        _linking is { } source ? new GraphLinkPreview(source, _linkCursor, _linkHover) : null;

    /// <summary>The node the dragged tie would arrive at, if any.</summary>
    public GraphNodeViewModel? LinkHover => _linkHover;

    /// <summary>
    /// A press begins one of several things, asked in the order they stand
    /// on the glass — words over node over line, topmost first: the right
    /// button asks rather than moves — a tie's own words opened where they
    /// stand, the small vocabulary of a node, the tie's words through the
    /// dialog; a tie's dot belongs to the hand before any count of
    /// presses; on the second press of a double, the thing pressed — a
    /// node's middle asking what it says, its rim still starting a tie, a
    /// tie asking its own words, its words opened where they stand, and
    /// empty paper asking nothing at all any more.
    /// Otherwise the words under the hand are opened in place, a node's
    /// middle is taken, its rim begins a tie, a tie taken along its line
    /// bows, and what is left is the paper itself.
    /// </summary>
    /// <param name="state">The state the press goes through.</param>
    /// <param name="position">Where on the glass the hand stands.</param>
    /// <param name="left">Whether the left button is down.</param>
    /// <param name="middle">Whether the middle button is down.</param>
    /// <param name="right">Whether the right button is down.</param>
    /// <param name="clickCount">How many presses in a row this is.</param>
    /// <returns>What the press came to.</returns>
    public GraphGestureResult Press(
        GraphState state,
        Point position,
        bool left,
        bool middle,
        bool right,
        int clickCount) {
        ArgumentNullException.ThrowIfNull(state);

        if (right) {
            // The right hand asks rather than moves: a tie's own words,
            // opened at the pill for rewriting where they stand; a node
            // with a small vocabulary — to join it to somebody, to change
            // it, or to take it away; a tie taken along its line, whose
            // words are asked through the dialog at once.
            if (_hits.LabelAt(state.Edges, position) is { } spoken) {
                state.BeginLabelEdit(spoken);
                return new GraphGestureResult(GraphGesture.OpenWords, Edge: spoken);
            }

            if (_hits.NodeAt(state.Nodes, position) is { } asked) {
                return new GraphGestureResult(GraphGesture.NodeMenu, Node: asked);
            }

            if ((_hits.HandleAt(state.Edges, position) ?? _hits.EdgeAt(state.Edges, position)) is { } tie) {
                // A dot stands off its line now, so a right-hand question
                // at the dot asks the same as one at the line itself.
                state.SelectEdge(tie);
                _ = state.EditCommand.ExecuteAsync(null);
                return new GraphGestureResult(GraphGesture.EditEdge, Edge: tie);
            }

            return new GraphGestureResult(GraphGesture.None);
        }

        // A tie's dot belongs to the hand before it belongs to any count
        // of presses: standing clear of the line beside its own words, it
        // is a handle made to be taken — a press on it puts the tie under
        // the eye in blue and takes hold of the curve in the same touch.
        // A second press a moment later is the double press's own question
        // about the tie, and the dot is not asked.
        if (left && clickCount != 2 && _hits.HandleAt(state.Edges, position) is { } dot) {
            state.SelectEdge(dot);
            _gestureStart = position;
            _moved = false;
            _bending = dot;
            return new GraphGestureResult(GraphGesture.Bend, Edge: dot);
        }

        if (clickCount == 2) {
            if (_hits.LabelAt(state.Edges, position) is { } told) {
                state.BeginLabelEdit(told);
                return new GraphGestureResult(GraphGesture.OpenWords, Edge: told);
            }

            if (_hits.NodeAt(state.Nodes, position) is { } node) {
                if (_hits.IsRim(node, position)) {
                    // Pressed over a node's rim again: still the rim, still
                    // the start of a tie — hands make ties in quick
                    // succession from the same place, and the count of
                    // presses must not turn the second into a question
                    // about the node instead.
                    StartLinking(state, node, position);
                    return new GraphGestureResult(GraphGesture.Link, Node: node);
                }

                state.SelectNode(node);
                _ = state.EditCommand.ExecuteAsync(null);
                return new GraphGestureResult(GraphGesture.EditNode, Node: node);
            }

            if (_hits.EdgeAt(state.Edges, position) is { } tie) {
                state.SelectEdge(tie);
                _ = state.EditCommand.ExecuteAsync(null);
                return new GraphGestureResult(GraphGesture.EditEdge, Edge: tie);
            }

            // A double press on empty paper asks nothing: the toolbar adds
            // characters now, and the paper has said all it has to say.
            return new GraphGestureResult(GraphGesture.None);
        }

        if (!left && !middle) {
            return new GraphGestureResult(GraphGesture.None);
        }

        _gestureStart = position;
        _moved = false;

        if (left && _hits.LabelAt(state.Edges, position) is { } written) {
            // One press on the words themselves, and the box stands over
            // them: a pill is too small a thing to drag and too fine a
            // thing to ask about twice.
            state.BeginLabelEdit(written);
            return new GraphGestureResult(GraphGesture.OpenWords, Edge: written);
        }

        if (left && _hits.NodeAt(state.Nodes, position) is { } grabbed) {
            if (_hits.IsRim(grabbed, position)) {
                StartLinking(state, grabbed, position);
                return new GraphGestureResult(GraphGesture.Link, Node: grabbed);
            }

            _dragging = grabbed;
            return new GraphGestureResult(GraphGesture.Drag, Node: grabbed);
        }

        if (left && _hits.EdgeAt(state.Edges, position) is { } picked) {
            // A tie taken where it runs: selected under the eye in the
            // same press, and the hand already holding the curve — one
            // gesture both chooses the tie and bows it, so the dot is
            // never something to fetch first.
            state.SelectEdge(picked);
            _bending = picked;
            return new GraphGestureResult(GraphGesture.Bend, Edge: picked);
        }

        _panning = true;
        return new GraphGestureResult(GraphGesture.Pan);
    }

    /// <summary>
    /// The node under the hand goes where the hand goes, the tie taken
    /// along its line bows wherever the hand holds it, the tie dragged out
    /// of a rim follows the hand as a line to somewhere, and the paper
    /// under the hand goes the other way.
    /// </summary>
    /// <param name="state">The state the movement goes through.</param>
    /// <param name="position">Where on the glass the hand stands.</param>
    /// <returns>What the movement came to.</returns>
    public GraphGestureResult Move(GraphState state, Point position) {
        ArgumentNullException.ThrowIfNull(state);

        if (_dragging is not null) {
            var world = _viewport.ToWorld(position);
            _dragging.X = world.X;
            _dragging.Y = world.Y;
            _moved = true;
            return new GraphGestureResult(GraphGesture.Drag, Node: _dragging);
        }

        if (_bending is not null) {
            var world = _viewport.ToWorld(position);
            _bending.Bend = _geometry.BendToward(state.Edges, _bending, world);
            _moved = true;
            return new GraphGestureResult(GraphGesture.Bend, Edge: _bending);
        }

        if (_linking is not null) {
            // The line follows the hand, and the node that would answer is
            // the one under it — never the one it was dragged out of.
            _linkCursor = position;
            var over = _hits.NodeAt(state.Nodes, position);
            _linkHover = over is not null && !ReferenceEquals(over, _linking) ? over : null;
            return new GraphGestureResult(GraphGesture.Link, Node: _linking);
        }

        if (_panning) {
            _viewport.PanBy(position - _gestureStart);
            _gestureStart = position;
            _moved = true;
            return new GraphGestureResult(GraphGesture.Pan);
        }

        return new GraphGestureResult(GraphGesture.None);
    }

    /// <summary>
    /// The hand lets go: a node taken and moved stands where it was left
    /// and the store hears it; a node only clicked is selected; a tie's
    /// curve let go of is heard by the store likewise; a rim let go of on
    /// another node is asked for as a tie between them; empty paper
    /// released without a drag sweeps the selection away.
    /// </summary>
    /// <param name="state">The state the release goes through.</param>
    /// <returns>What the release came to.</returns>
    public GraphGestureResult Release(GraphState state) {
        ArgumentNullException.ThrowIfNull(state);

        if (_dragging is { } node) {
            _dragging = null;

            if (_moved) {
                state.PersistPosition(node);
            } else {
                state.SelectNode(node);
            }

            return new GraphGestureResult(GraphGesture.Drag, Node: node);
        }

        if (_bending is { } bent) {
            // The curve let go of: the store hears the bend, so the tie
            // stands in the shape it was left in the next time the map is
            // opened as well as this one.
            _bending = null;

            if (_moved) {
                state.PersistBend(bent);
            }

            return new GraphGestureResult(GraphGesture.Bend, Edge: bent);
        }

        if (_linking is { } source) {
            // The rim let go of: on another node the tie is asked for with
            // both ends already standing; anywhere else the gesture is
            // simply dropped, and the paper is exactly as it was.
            var target = _linkHover;
            _linking = null;
            _linkHover = null;

            if (target is not null) {
                _ = state.ConnectNodesAsync(source, target);
            }

            return new GraphGestureResult(GraphGesture.Link, Node: source);
        }

        if (_panning) {
            _panning = false;

            if (!_moved) {
                state.ClearSelection();
            }

            return new GraphGestureResult(GraphGesture.Pan);
        }

        return new GraphGestureResult(GraphGesture.None);
    }

    /// <summary>
    /// Begins a tie dragged out of a node's rim: the node answers under the
    /// eye, and a line follows the hand from here until it is let go — on
    /// another node, where both ends stand answered before a word is asked
    /// for, or on nothing, where the question is dropped whole.
    /// </summary>
    /// <param name="state">The state the answer goes through.</param>
    /// <param name="from">The node the tie is dragged out of.</param>
    /// <param name="position">Where on the glass the hand stands.</param>
    private void StartLinking(GraphState state, GraphNodeViewModel from, Point position) {
        state.SelectNode(from);
        _linking = from;
        _linkCursor = position;
        _linkHover = null;
    }
}
