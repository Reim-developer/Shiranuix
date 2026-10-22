using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Shiranuix.Graphs;

/// <summary>
/// The glass the relationship map is drawn on: nodes as circles with their
/// names under them, ties as arrows with their words along them, and the
/// whole paper moved and looked at larger by the writer's own hand.
/// </summary>
/// <remarks>
/// <para>
/// A control rather than a scene graph: the drawing is one pass over the
/// state's own collections every time something twitches, which a writer's
/// graph — tens of nodes, not thousands — survives without a care. What
/// the canvas does beyond drawing is the handful of moves every writer
/// already knows: the wheel looks closer or further, empty paper drags the
/// paper itself, a node taken in its middle goes where the hand takes it,
/// a tie taken along its line bows wherever the hand holds it, and a tie
/// dragged out of a node's rim arrives at another node and asks what it
/// should say.
/// </para>
/// <para>
/// The canvas itself keeps only what is the glass's own business — the
/// pointer, the cursor, the drawing — and asks the rest of the map: the
/// camera where the world stands, the geometry where a tie's line runs, the
/// hit test what is under the hand, the gestures what a press means, and
/// the renderer what the frame looks like.
/// </para>
/// <para>
/// Everything above the glass — the toolbar, the picker, the dialogs —
/// belongs to the window; the canvas asks the state only for what it draws
/// and tells it only what the writer did to it: a node clicked, a tie
/// clicked, a tie's words pointed at for rewriting where they stand, a
/// node let go of, a tie's curve let go of, a rim let go of on another
/// node, and a right-click asking the small vocabulary a node or a tie has.
/// </para>
/// </remarks>
public sealed class GraphCanvas : Control {
    /// <summary>The state this canvas draws, set once by the window that owns it.</summary>
    public static readonly StyledProperty<GraphState?> StateProperty =
        AvaloniaProperty.Register<GraphCanvas, GraphState?>(nameof(State));

    private readonly GraphViewport _viewport = new();
    private readonly GraphTextCache _text = new();
    private readonly GraphEdgeGeometry _geometry;
    private readonly GraphHitTester _hits;
    private readonly GraphGestures _gestures;
    private readonly GraphRenderer _renderer;

    // The two cursors the paper wears, made once each: the pointer crosses
    // the canvas far too often to be handed a fresh cursor every time.
    private readonly Cursor _overNode = new(StandardCursorType.Hand);
    private readonly Cursor _overPaper = new(StandardCursorType.SizeAll);

    static GraphCanvas() {
        AffectsRender<GraphCanvas>(StateProperty);
    }

    /// <summary>Creates the canvas ready for the writer's hand.</summary>
    public GraphCanvas() {
        _geometry = new GraphEdgeGeometry(_viewport);
        _hits = new GraphHitTester(_viewport, _geometry, _text);
        _gestures = new GraphGestures(_hits, _geometry, _viewport);
        _renderer = new GraphRenderer(_viewport, _geometry, _text);

        Focusable = true;

        // The palette switches with the theme, and the canvas wears it the
        // moment it does — words and all, so nothing of the old theme's
        // ink is left standing on the paper.
        ActualThemeVariantChanged += (_, _) => {
            _text.Clear();
            InvalidateVisual();
        };
    }

    /// <summary>The state this canvas draws and pushes back at.</summary>
    public GraphState? State {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>
    /// The camera the map is seen through: where the world stands on the
    /// glass, and how much of it the writer has magnified.
    /// </summary>
    public IGraphViewport Viewport => _viewport;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
        base.OnPropertyChanged(change);

        if (change.Property == StateProperty) {
            if (change.OldValue is GraphState old) {
                old.Changed -= OnStateChanged;
                old.PropertyChanged -= OnStateChangedProperty;
                old.Arranged -= OnArranged;
            }

            if (change.NewValue is GraphState state) {
                state.Changed += OnStateChanged;
                state.PropertyChanged += OnStateChangedProperty;
                state.Arranged += OnArranged;
            }

            // A new graph stands where the last one did until the writer
            // says otherwise: the paper remembers its own place.
            InvalidateVisual();
        }
    }

    /// <summary>The whole canvas is heard again whenever the state says so.</summary>
    private void OnStateChanged() => InvalidateVisual();

    /// <summary>
    /// The selection moves without the state's wholesale word — a node
    /// picked after a dialog, the picker turning to another graph's cast —
    /// and the canvas wears it the moment it does.
    /// </summary>
    private void OnStateChangedProperty(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        if (e.PropertyName is nameof(GraphState.SelectedNode) or nameof(GraphState.SelectedEdge)) {
            InvalidateVisual();
        }
    }

    /// <summary>
    /// The graph has been arranged afresh, and the paper brings the whole
    /// of it into the glass: a graph that stands half off the paper is a
    /// graph the writer has to go looking for.
    /// </summary>
    private void OnArranged() {
        if (State is not { } state || Bounds.Width <= 0 || Bounds.Height <= 0) {
            return;
        }

        _viewport.Fit(BoundsOf(state.Nodes), Bounds.Size, GraphMetrics.NodeMargin);
        InvalidateVisual();
    }

    /// <summary>Draws everything the state holds, in one pass.</summary>
    /// <param name="context">What to draw with.</param>
    public override void Render(DrawingContext context) {
        base.Render(context);

        // The sheet of paper itself, laid down under everything that will
        // follow: the glass only takes a press where its own ink falls, so
        // without this whole sheet a press on bare paper would slip through
        // to the window beneath and nothing — not the hand panning the
        // world, not a writer walking away from the words box — would ever
        // land here. The window's own colour shows through the ink, which
        // is to say nothing of it shows at all.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));

        var state = State;
        if (state is null) {
            return;
        }

        CenterOnce();

        _renderer.Draw(context, state, GraphPalette.Resolve(this), _gestures.Link);
    }

    /// <summary>The wheel looks closer or further, about where the pointer stands.</summary>
    /// <param name="e">The wheel's own event.</param>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e) {
        base.OnPointerWheelChanged(e);

        if (State is null) {
            return;
        }

        _viewport.ZoomAt(e.GetPosition(this), e.Delta.Y > 0 ? 1.12 : 1 / 1.12);

        InvalidateVisual();
        e.Handled = true;
    }

    /// <summary>
    /// A press is put to the map's own hands, and the glass answers with
    /// what it can: the small vocabulary of a node opens where the hand
    /// pointed, the pointer is taken hold of for the gestures that carry
    /// the hand across the paper, and the words of a tie open their box
    /// over the very pill they stand on.
    /// </summary>
    /// <param name="e">The press's own event.</param>
    protected override void OnPointerPressed(PointerPressedEventArgs e) {
        base.OnPointerPressed(e);

        var state = State;
        if (state is null) {
            return;
        }

        Focus();

        var point = e.GetCurrentPoint(this);
        var position = point.Position;
        var pressed = point.Properties;

        var result = _gestures.Press(
            state,
            position,
            pressed.IsLeftButtonPressed,
            pressed.IsMiddleButtonPressed,
            pressed.IsRightButtonPressed,
            e.ClickCount);

        if (result.Gesture == GraphGesture.NodeMenu) {
            GraphNodeMenu.Show(this, state, result.Node!);
        }

        if (pressed.IsRightButtonPressed) {
            // The right hand asks rather than moves: the glass answers and
            // takes nothing hold of.
            e.Handled = true;
            return;
        }

        if (e.ClickCount == 2) {
            // A double press asks its question and leaves the paper alone,
            // unless it asked to begin a tie, which the hand carries on.
            if (result.Gesture == GraphGesture.Link) {
                e.Pointer.Capture(this);
            }

            e.Handled = true;
            return;
        }

        if (result.Gesture == GraphGesture.None) {
            return;
        }

        if (result.Gesture != GraphGesture.OpenWords) {
            // A press on the words themselves opens the box over them and
            // takes nothing hold of: a pill is too small a thing to drag.
            e.Pointer.Capture(this);
        }

        e.Handled = true;
    }

    /// <summary>
    /// The hand moves: the node under it goes where the hand goes, the tie
    /// taken along its line bows wherever the hand holds it, the tie
    /// dragged out of a rim follows the hand as a line to somewhere, the
    /// paper under the hand goes the other way — and with none of them,
    /// the cursor says what a press would do.
    /// </summary>
    /// <param name="e">The movement's own event.</param>
    protected override void OnPointerMoved(PointerEventArgs e) {
        base.OnPointerMoved(e);

        var state = State;
        if (state is null) {
            return;
        }

        var position = e.GetPosition(this);
        var result = _gestures.Move(state, position);

        if (result.Gesture == GraphGesture.None) {
            Cursor = _hits.NodeAt(state.Nodes, position) is not null ? _overNode : _overPaper;
            return;
        }

        if (result.Gesture == GraphGesture.Link) {
            // The tie being dragged out of a rim: the cursor says whether
            // the hand is over a node that would answer it.
            Cursor = _gestures.LinkHover is not null ? _overNode : _overPaper;
        }

        InvalidateVisual();
        e.Handled = true;
    }

    /// <summary>
    /// The hand lets go: a node taken and moved stands where it was left
    /// and the store hears it; a node only clicked is selected; a tie's
    /// curve let go of is heard by the store likewise; a rim let go of on
    /// another node is asked for as a tie between them; empty paper
    /// released without a drag sweeps the selection away.
    /// </summary>
    /// <param name="e">The release's own event.</param>
    protected override void OnPointerReleased(PointerReleasedEventArgs e) {
        base.OnPointerReleased(e);

        var state = State;
        e.Pointer.Capture(null);

        if (state is null) {
            return;
        }

        _gestures.Release(state);
        InvalidateVisual();
    }

    /// <summary>Delete strikes out whatever stands selected — a node whole or one tie.</summary>
    /// <param name="e">The key's own event.</param>
    protected override void OnKeyDown(KeyEventArgs e) {
        base.OnKeyDown(e);

        if (e.Key == Key.Delete && State is { HasSelection: true } state) {
            _ = state.DeleteSelectionCommand.ExecuteAsync(null);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Where one tie's words stand on the glass — the very pill the
    /// drawing inks, asked by the window when it is about to stand its box
    /// over them and let the writer rewrite them in place.
    /// </summary>
    /// <param name="edge">The tie whose words to find.</param>
    /// <param name="box">Where they stand, in the canvas's own coordinates.</param>
    /// <returns>False when the tie carries no words, or has no line to stand beside.</returns>
    public bool TryGetLabelBox(GraphEdgeViewModel edge, out Rect box) {
        ArgumentNullException.ThrowIfNull(edge);

        if (State is not { } state) {
            box = default;
            return false;
        }

        return _renderer.TryGetLabelBox(state, edge, out box);
    }

    /// <summary>
    /// Stands the middle of the world in the middle of the glass the first
    /// time there is a glass to stand it in — once, because the writer's
    /// own pans and zooms are theirs to keep.
    /// </summary>
    private void CenterOnce() {
        if (_viewport.HasCentered || Bounds.Width <= 0 || Bounds.Height <= 0) {
            return;
        }

        _viewport.CenterOn(
            new Rect(0, 0, GraphState.WorldWidth, GraphState.WorldHeight),
            Bounds.Size);
    }

    /// <summary>
    /// The box every node of the graph stands in, in the world's own units.
    /// </summary>
    /// <param name="nodes">Every node in the open graph.</param>
    /// <returns>The smallest box they all stand in.</returns>
    private static Rect BoundsOf(IReadOnlyList<GraphNodeViewModel> nodes) {
        if (nodes.Count == 0) {
            return default;
        }

        var left = double.MaxValue;
        var top = double.MaxValue;
        var right = double.MinValue;
        var bottom = double.MinValue;

        foreach (var node in nodes) {
            left = Math.Min(left, node.X);
            top = Math.Min(top, node.Y);
            right = Math.Max(right, node.X);
            bottom = Math.Max(bottom, node.Y);
        }

        return new Rect(left, top, right - left, bottom - top);
    }
}
