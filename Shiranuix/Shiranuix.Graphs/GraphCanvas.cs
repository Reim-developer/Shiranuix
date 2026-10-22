using System.Globalization;
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
/// Everything above the glass — the toolbar, the picker, the dialogs —
/// belongs to the window; the canvas asks the state only for what it draws
/// and tells it only what the writer did to it: a node clicked, a tie
/// clicked, a tie's words pointed at for rewriting where they stand, a
/// node let go of, a tie's curve let go of, a rim let go of on another
/// node, and a right-click asking the small vocabulary a node or a tie has.
/// </para>
/// </remarks>
public sealed class GraphCanvas : Control {
    /// <summary>The radius a node is drawn at, in the world's own units.</summary>
    private const double NodeRadius = 26;

    /// <summary>How near a tie's line a press still counts as the tie.</summary>
    private const double EdgeHitSlop = 7;

    /// <summary>How far apart parallel ties are drawn, in the world's own units.</summary>
    private const double ParallelSpread = 34;

    /// <summary>The dot a selected tie wears at its middle, for bending.</summary>
    private const double BendDotRadius = 5;

    /// <summary>How far around the dot a hand may miss it and still take hold.</summary>
    private const double BendDotSlop = 4;

    /// <summary>
    /// How far from a node's centre its rim begins — the band a press drags
    /// a tie out of rather than the node itself, kept thin and at the very
    /// edge so a hand reaching for a node still takes the node.
    /// </summary>
    private const double RimInnerRadius = 20;

    /// <summary>The furthest the wheel may pull back, and the closest it may push in.</summary>
    private const double MinZoom = 0.15;

    /// <summary>The closest the wheel may push in; see <see cref="MinZoom"/>.</summary>
    private const double MaxZoom = 5;

    /// <summary>The state this canvas draws, set once by the window that owns it.</summary>
    public static readonly StyledProperty<GraphState?> StateProperty =
        AvaloniaProperty.Register<GraphCanvas, GraphState?>(nameof(State));

    private readonly Dictionary<string, (IBrush Brush, FormattedText Text)> _labels = new();

    // The two cursors the paper wears, made once each: the pointer crosses
    // the canvas far too often to be handed a fresh cursor every time.
    private readonly Cursor _overNode = new(StandardCursorType.Hand);
    private readonly Cursor _overPaper = new(StandardCursorType.SizeAll);

    private double _zoom = 1;
    private Point _offset;
    private bool _centered;

    private GraphNodeViewModel? _dragging;
    private GraphEdgeViewModel? _bending;
    private GraphNodeViewModel? _linking;
    private GraphNodeViewModel? _linkHover;
    private Point _linkCursor;
    private bool _panning;
    private bool _moved;
    private Point _gestureStart;

    static GraphCanvas() {
        AffectsRender<GraphCanvas>(StateProperty);
    }

    /// <summary>Creates the canvas ready for the writer's hand.</summary>
    public GraphCanvas() {
        Focusable = true;

        // The palette switches with the theme, and the canvas wears it the
        // moment it does — words and all, so nothing of the old theme's
        // ink is left standing on the paper.
        ActualThemeVariantChanged += (_, _) => {
            _labels.Clear();
            InvalidateVisual();
        };
    }

    /// <summary>The state this canvas draws and pushes back at.</summary>
    public GraphState? State {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
        base.OnPropertyChanged(change);

        if (change.Property == StateProperty) {
            if (change.OldValue is GraphState old) {
                old.Changed -= OnStateChanged;
                old.PropertyChanged -= OnStateChangedProperty;
            }

            if (change.NewValue is GraphState state) {
                state.Changed += OnStateChanged;
                state.PropertyChanged += OnStateChangedProperty;
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

    /// <summary>Draws everything the state holds, in one pass.</summary>
    /// <param name="context">What to draw with.</param>
    public override void Render(DrawingContext context) {
        base.Render(context);

        CenterOnce();

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

        var palette = ResolvePalette();

        if (state.Nodes.Count == 0) {
            DrawEmpty(context, palette, state);
            return;
        }

        // The ties first, so no node ever stands on the arrow that points
        // at it. Ties between the same two nodes are spread into parallel
        // curves, one beside the other, rather than drawn over themselves.
        // Their words are gathered on the way past, for drawing over the
        // nodes at the end.
        var labels = new List<(GraphEdgeViewModel Edge, Point At, bool Selected)>();

        foreach (var edge in state.Edges) {
            DrawEdge(context, edge, palette, ReferenceEquals(edge, state.SelectedEdge), labels);
        }

        foreach (var node in state.Nodes) {
            DrawNode(context, node, palette, ReferenceEquals(node, state.SelectedNode));
        }

        // The words last of all, over the nodes: a tie's name drawn along
        // with its tie is a name the circles can stand on and swallow
        // whole. Beside its own line and above everything, it stays read.
        foreach (var (edge, at, selected) in labels) {
            DrawEdgeLabel(context, edge, at, selected, palette);
        }

        // The dot a selected tie wears at its middle, over everything: it
        // is the hand's own handle on the curve, and nothing may stand on
        // what the hand is about to take.
        if (state.SelectedEdge is { } chosen &&
            TryTieLine(chosen, out var start, out var end, out var control, out _)) {
            context.DrawEllipse(
                palette.EdgeSelected,
                new Pen(palette.Surface, 1.5),
                CurveMiddle(start, end, control),
                BendDotRadius,
                BendDotRadius);
        }

        // The tie being dragged out of a node's rim, over everything: it is
        // not yet a thing on the map but a question about to be one, and
        // the hand needs to see where it is pointing.
        if (_linking is { } source) {
            DrawLinking(context, palette, source, _linkCursor, _linkHover);
        }
    }

    /// <summary>The wheel looks closer or further, about where the pointer stands.</summary>
    /// <param name="e">The wheel's own event.</param>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e) {
        base.OnPointerWheelChanged(e);

        if (State is null) {
            return;
        }

        var position = e.GetPosition(this);
        var world = (Point)((Vector)(position - _offset) / _zoom);

        _zoom = Math.Clamp(_zoom * (e.Delta.Y > 0 ? 1.12 : 1 / 1.12), MinZoom, MaxZoom);
        _offset = position - (Vector)world * _zoom;

        InvalidateVisual();
        e.Handled = true;
    }

    /// <summary>
    /// A press begins one of several things, asked in the order they stand
    /// on the glass — words over node over line, topmost first: the right
    /// button asks rather than moves — a tie's own words opened where they
    /// stand, the small vocabulary of a node, the tie's words through the
    /// dialog; the dot of a selected tie belongs to the hand before any
    /// count of presses; on the second press of a double, the thing
    /// pressed — a node's middle asking what it says, its rim still
    /// starting a tie, a tie asking its own words, its words opened where
    /// they stand, and empty paper asking nothing at all any more.
    /// Otherwise the words under the hand are opened in place, a node's
    /// middle is taken, its rim begins a tie, a tie taken along its line
    /// bows, and what is left is the paper itself.
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

        if (pressed.IsRightButtonPressed) {
            // The right hand asks rather than moves: a tie's own words,
            // opened at the pill for rewriting where they stand; a node
            // with a small vocabulary — to join it to somebody, to change
            // it, or to take it away; a tie taken along its line, whose
            // words are asked through the dialog at once.
            if (TryOpenWords(state, position)) {
                e.Handled = true;
                return;
            }

            if (NodeAt(position) is { } picked) {
                ShowNodeMenu(state, picked);
            } else if (EdgeAt(position) is { } tie) {
                state.SelectEdge(tie);
                InvalidateVisual();
                _ = state.EditCommand.ExecuteAsync(null);
            }

            e.Handled = true;
            return;
        }

        // The dot at a selected tie's middle belongs to the hand before it
        // belongs to any count of presses: a tie is picked where it runs,
        // and the dot standing at that same middle picked again a moment
        // later — close enough together to be counted as one double — and
        // that second press must be a hand taking hold of the curve, not
        // the dialog asking what the tie says anew.
        if (pressed.IsLeftButtonPressed && HandleAt(position) is { } dot) {
            _gestureStart = position;
            _moved = false;
            _bending = dot;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (e.ClickCount == 2) {
            e.Handled = true;

            if (TryOpenWords(state, position)) {
                return;
            }

            if (NodeAt(position) is { } node) {
                if (IsRim(node, position)) {
                    // Pressed over a node's rim again: still the rim, still
                    // the start of a tie — hands make ties in quick
                    // succession from the same place, and the count of
                    // presses must not turn the second into a question
                    // about the node instead.
                    StartLinking(state, node, position);
                    e.Pointer.Capture(this);
                } else {
                    state.SelectNode(node);
                    _ = state.EditCommand.ExecuteAsync(null);
                }
            } else if (EdgeAt(position) is { } tie) {
                state.SelectEdge(tie);
                InvalidateVisual();
                _ = state.EditCommand.ExecuteAsync(null);
            }

            // A double press on empty paper asks nothing: the toolbar adds
            // characters now, and the paper has said all it has to say.
            return;
        }

        if (!pressed.IsLeftButtonPressed && !pressed.IsMiddleButtonPressed) {
            return;
        }

        _gestureStart = position;
        _moved = false;

        if (pressed.IsLeftButtonPressed && TryOpenWords(state, position)) {
            // One press on the words themselves, and the box stands over
            // them: a pill is too small a thing to drag and too fine a
            // thing to ask about twice.
            e.Handled = true;
            return;
        } else if (pressed.IsLeftButtonPressed && NodeAt(position) is { } grabbed) {
            if (IsRim(grabbed, position)) {
                StartLinking(state, grabbed, position);
            } else {
                _dragging = grabbed;
            }
        } else if (pressed.IsLeftButtonPressed && EdgeAt(position) is { } picked) {
            // A tie taken where it runs: selected under the eye in the
            // same press, and the hand already holding the curve — one
            // gesture both chooses the tie and bows it, so the dot is
            // never something to fetch first.
            state.SelectEdge(picked);
            _bending = picked;
            InvalidateVisual();
        } else {
            _panning = true;
        }

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <summary>
    /// The node under the hand goes where the hand goes, the tie taken
    /// along its line bows wherever the hand holds it, the tie dragged out
    /// of a rim follows the hand as a line to somewhere, the paper under
    /// the hand goes the other way — and with none of them, the cursor
    /// says what a press would do.
    /// </summary>
    /// <param name="e">The movement's own event.</param>
    protected override void OnPointerMoved(PointerEventArgs e) {
        base.OnPointerMoved(e);

        var state = State;
        if (state is null) {
            return;
        }

        var position = e.GetPosition(this);

        if (_dragging is not null) {
            var world = ToWorld(position);
            _dragging.X = world.X;
            _dragging.Y = world.Y;
            _moved = true;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_bending is not null) {
            var world = ToWorld(position);
            _bending.Bend = BendToward(_bending, world);
            _moved = true;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_linking is not null) {
            // The line follows the hand, and the node that would answer is
            // the one under it — never the one it was dragged out of.
            _linkCursor = position;
            var over = NodeAt(position);
            _linkHover = over is not null && !ReferenceEquals(over, _linking) ? over : null;
            if (_linkHover is not null) {
                Cursor = _overNode;
            }

            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_panning) {
            _offset += position - _gestureStart;
            _gestureStart = position;
            _moved = true;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        Cursor = NodeAt(position) is not null ? _overNode : _overPaper;
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

        if (_dragging is { } node) {
            _dragging = null;

            if (_moved) {
                state?.PersistPosition(node);
            } else {
                state?.SelectNode(node);
                InvalidateVisual();
            }
        } else if (_bending is { } bent) {
            // The curve let go of: the store hears the bend, so the tie
            // stands in the shape it was left in the next time the map is
            // opened as well as this one.
            _bending = null;

            if (_moved) {
                state?.PersistBend(bent);
            }
        } else if (_linking is { } source) {
            // The rim let go of: on another node the tie is asked for with
            // both ends already standing; anywhere else the gesture is
            // simply dropped, and the paper is exactly as it was.
            var target = _linkHover;
            _linking = null;
            _linkHover = null;

            if (target is not null && state is not null) {
                _ = state.ConnectNodesAsync(source, target);
            }

            InvalidateVisual();
        } else if (_panning) {
            _panning = false;

            if (!_moved) {
                state?.ClearSelection();
                InvalidateVisual();
            }
        }
    }

    /// <summary>Delete strikes out whatever stands selected — a node whole or one tie.</summary>
    /// <param name="e">The key's own event.</param>
    protected override void OnKeyDown(KeyEventArgs e) {
        base.OnKeyDown(e);

        if (e.Key is Key.Delete or Key.Back && State is { HasSelection: true } state) {
            _ = state.DeleteSelectionCommand.ExecuteAsync(null);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Fills in the words the empty paper has to say — no graph yet, or a
    /// graph with nobody standing in it yet.
    /// </summary>
    private void DrawEmpty(DrawingContext context, Palette palette, GraphState state) {
        var words = state.HasCurrentGraph
            ? "Add a character from the toolbar above."
            : "Make a graph from the toolbar above to begin.";

        var text = Label(words, 13, palette.Hint);
        var center = ToScreen(new Point(GraphState.WorldWidth / 2, GraphState.WorldHeight / 2));
        context.DrawText(text, center - new Vector(text.Width / 2, text.Height / 2));
    }

    /// <summary>Draws one tie: its curve and its arrow, leaving its words to the labels.</summary>
    /// <param name="labels">Where a tie's words are left for drawing over the nodes.</param>
    private void DrawEdge(
        DrawingContext context,
        GraphEdgeViewModel edge,
        Palette palette,
        bool selected,
        List<(GraphEdgeViewModel Edge, Point At, bool Selected)> labels) {
        if (!TryTieLine(edge, out var start, out var end, out var bend, out var unit)) {
            return;
        }

        var brush = selected ? palette.EdgeSelected : palette.Edge;
        var pen = new Pen(brush, selected ? 2 : 1.4);

        var geometry = new StreamGeometry();
        using (var drawing = geometry.Open()) {
            drawing.BeginFigure(start, isFilled: false);
            if (bend is { } control) {
                drawing.QuadraticBezierTo(control, end);
            } else {
                drawing.LineTo(end);
            }

            drawing.EndFigure(isClosed: false);
        }

        context.DrawGeometry(null, pen, geometry);

        // The arrowhead: pointing along the line as it arrives, which is
        // the curve's own direction when the tie bends at all.
        DrawArrowHead(context, brush, end, bend ?? start);

        if (edge.Label.Length > 0) {
            // Beside its own line rather than on it: laid over the middle
            // of the tie the words fight the arrow for the eye, and where
            // a circle may stand they lose outright — so they are pushed
            // off to one side, in the same frame the parallel curves
            // spread in, which keeps a pair's words apart as well.
            labels.Add((edge, WordsAt(CurveMiddle(start, end, bend), unit, edge), selected));
        }
    }

    /// <summary>
    /// Draws a tie's own arrowhead: from the point it makes back along the
    /// line it arrives on, so a straight tie points straight and a bowed
    /// one arrives along its curve.
    /// </summary>
    /// <param name="context">What to draw with.</param>
    /// <param name="brush">The ink to fill the point in.</param>
    /// <param name="tip">Where the point stands.</param>
    /// <param name="from">Where the line comes from — its start, or the curve's control point.</param>
    private static void DrawArrowHead(DrawingContext context, IBrush brush, Point tip, Point from) {
        var headX = tip.X - from.X;
        var headY = tip.Y - from.Y;
        var headingLength = Math.Sqrt(headX * headX + headY * headY);
        if (headingLength < 0.01) {
            return;
        }

        var arrow = new Vector(headX / headingLength, headY / headingLength);
        var back = tip - arrow * 10;
        var side = new Vector(-arrow.Y, arrow.X) * 4.5;

        var shape = new StreamGeometry();
        using (var drawing = shape.Open()) {
            drawing.BeginFigure(tip, isFilled: true);
            drawing.LineTo(back + side);
            drawing.LineTo(back - side);
            drawing.EndFigure(isClosed: true);
        }

        context.DrawGeometry(brush, null, shape);
    }

    /// <summary>
    /// Where one tie's line actually runs: pulled back to the circles'
    /// edges, spread sideways among the ties standing between the same two
    /// nodes, and bent either side of the straight by the writer's own
    /// hand — one place asked, so the drawing, the words and the dot all
    /// agree on where the curve is.
    /// </summary>
    /// <param name="edge">The tie to trace.</param>
    /// <param name="start">Where the line leaves the first circle.</param>
    /// <param name="end">Where it arrives at the second.</param>
    /// <param name="control">The point the curve bows through, or nothing for a straight line.</param>
    /// <param name="unit">The direction the line runs in.</param>
    /// <returns>False when the two nodes stand on each other and there is no line at all.</returns>
    private bool TryTieLine(
        GraphEdgeViewModel edge,
        out Point start,
        out Point end,
        out Point? control,
        out Vector unit) {
        start = default;
        end = default;
        control = null;
        unit = default;

        if (State is null) {
            return false;
        }

        var from = ToScreen(new Point(edge.From.X, edge.From.Y));
        var to = ToScreen(new Point(edge.To.X, edge.To.Y));

        // Worked out of the components rather than through the types: the
        // canvas only needs the length to know there is a line at all, and
        // the direction to point it along.
        var deltaX = to.X - from.X;
        var deltaY = to.Y - from.Y;
        var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        if (distance < 1) {
            // The two nodes stand on each other — there is no line to draw
            // between them, and nothing to point along.
            return false;
        }

        unit = new Vector(deltaX / distance, deltaY / distance);

        // Both ends pulled back to the circles' edges, so an arrow touches
        // its nodes rather than disappearing into them.
        var radius = NodeRadius * _zoom;
        start = from + unit * radius;
        end = to - unit * (radius + 4);

        var offset = edge.Bend + ParallelOffset(edge);
        if (Math.Abs(offset) > 0.01) {
            // Sideways in the same frame whichever way the arrow points, so
            // a tie and its answer bend apart rather than over each other,
            // and a bend the hand dragged lands on the same side as the
            // spread it was dragged out of.
            var flip = edge.From.Id > edge.To.Id ? -1 : 1;
            var middle = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);
            control = middle + new Vector(-unit.Y * flip, unit.X * flip) * offset;
        }

        return true;
    }

    /// <summary>
    /// How far sideways one tie is drawn among the ties standing between
    /// the same two nodes — and zero when it stands alone.
    /// </summary>
    /// <param name="edge">The tie to ask about.</param>
    /// <returns>The spread's own share, in world units.</returns>
    private double ParallelOffset(GraphEdgeViewModel edge) {
        if (State is not { } state) {
            return 0;
        }

        var index = 0;
        var count = 0;
        foreach (var other in state.Edges) {
            if (!edge.IsParallelTo(other)) {
                continue;
            }

            if (ReferenceEquals(other, edge)) {
                index = count;
            }

            count++;
        }

        return count > 1 ? (index - (count - 1) / 2.0) * ParallelSpread : 0;
    }

    /// <summary>Where the middle of a tie's curve stands on the glass.</summary>
    /// <param name="start">Where the line leaves the first circle.</param>
    /// <param name="end">Where it arrives at the second.</param>
    /// <param name="control">The point the curve bows through, or nothing for a straight line.</param>
    /// <returns>The middle — halfway along a straight tie, halfway between the bows of a curved one.</returns>
    private static Point CurveMiddle(Point start, Point end, Point? control) =>
        control is { } through
            ? new Point(
                (start.X + 2 * through.X + end.X) / 4,
                (start.Y + 2 * through.Y + end.Y) / 4)
            : new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);

    /// <summary>
    /// Where one tie's words stand: beside the middle of its curve, off to
    /// one side in the same frame the parallel curves spread in — asked of
    /// the line itself, so the drawing, the hit test and the box the window
    /// opens can never disagree about the pill's place.
    /// </summary>
    /// <param name="middle">Where the curve's middle stands on the glass.</param>
    /// <param name="unit">The direction the line runs in.</param>
    /// <param name="edge">The tie whose words these are.</param>
    /// <returns>The middle of the words.</returns>
    private static Point WordsAt(Point middle, Vector unit, GraphEdgeViewModel edge) =>
        middle + new Vector(-unit.Y, unit.X) * (edge.From.Id > edge.To.Id ? -13 : 13);

    /// <summary>The pill one tie's words are inked on, around their own middle.</summary>
    /// <param name="at">Where the words' middle stands.</param>
    /// <param name="textWidth">How wide the words measure.</param>
    /// <param name="textHeight">How tall they measure.</param>
    /// <returns>The pill's own bounds.</returns>
    private static Rect Pill(Point at, double textWidth, double textHeight) =>
        new(at.X - textWidth / 2 - 4, at.Y - textHeight / 2 - 2.5, textWidth + 8, textHeight + 5);

    /// <summary>
    /// How far a tie must bend for its curve's middle to stand where the
    /// hand is holding it — asked in the frame the line runs in, so the
    /// dot goes wherever the hand takes it, and the tie stands afterwards
    /// exactly where it was let go of.
    /// </summary>
    /// <param name="edge">The tie being bent.</param>
    /// <param name="world">Where the hand holds the middle, in the world's own units.</param>
    /// <returns>The bend to draw from now on.</returns>
    private double BendToward(GraphEdgeViewModel edge, Point world) {
        var a = new Vector(edge.From.X, edge.From.Y);
        var b = new Vector(edge.To.X, edge.To.Y);
        var along = b - a;
        var length = Math.Sqrt(along.X * along.X + along.Y * along.Y);
        if (length < 1) {
            return edge.Bend;
        }

        var flip = edge.From.Id > edge.To.Id ? -1 : 1;
        var side = new Vector(-along.Y / length * flip, along.X / length * flip);

        // How far off the straight line the hand is holding the middle at.
        var half = (a + b) / 2;
        var stray = new Vector(world.X - half.X, world.Y - half.Y);
        var distance = stray.X * side.X + stray.Y * side.Y;

        // A quadratic bows halfway to its control point, so the bend is
        // twice the hand's height — and the parallel spread is already
        // pulling this tie part of the way there.
        return 2 * distance - ParallelOffset(edge);
    }

    /// <summary>
    /// Draws one tie's words: on a pill of their own, beside the line they
    /// name — after every node, so no circle may stand on them.
    /// </summary>
    private void DrawEdgeLabel(
        DrawingContext context,
        GraphEdgeViewModel edge,
        Point at,
        bool selected,
        Palette palette) {
        var text = Label(edge.Label, 11, selected ? palette.EdgeSelected : palette.Text);
        context.DrawRectangle(palette.Surface, null, Pill(at, text.Width, text.Height), 4, 4);
        context.DrawText(text, at - new Vector(text.Width / 2, text.Height / 2));
    }

    /// <summary>Draws one node: the circle, and the name underneath.</summary>
    private void DrawNode(
        DrawingContext context,
        GraphNodeViewModel node,
        Palette palette,
        bool selected) {
        var center = ToScreen(new Point(node.X, node.Y));
        var radius = NodeRadius * _zoom;

        context.DrawEllipse(
            palette.NodeFill,
            selected ? palette.NodeSelected : palette.NodeStroke,
            center,
            radius,
            radius);

        var name = Label(node.Name, 12, palette.Name);
        context.DrawText(name, center + new Vector(-name.Width / 2, radius + 4));
    }

    /// <summary>The node standing under a point on the glass, topmost first.</summary>
    /// <param name="screen">The point on the glass.</param>
    /// <returns>The node there, or nothing.</returns>
    private GraphNodeViewModel? NodeAt(Point screen) {
        if (State is not { } state) {
            return null;
        }

        var world = ToWorld(screen);
        for (var i = state.Nodes.Count - 1; i >= 0; i--) {
            var node = state.Nodes[i];
            var dx = world.X - node.X;
            var dy = world.Y - node.Y;

            if (dx * dx + dy * dy <= (NodeRadius + 2) * (NodeRadius + 2)) {
                return node;
            }
        }

        return null;
    }

    /// <summary>
    /// Whether a press stands on a node's rim — the band a tie is dragged
    /// out of — rather than in its middle, where the node itself is taken
    /// and moved.
    /// </summary>
    /// <param name="node">The node the press landed on.</param>
    /// <param name="screen">The point on the glass.</param>
    /// <returns>True on the rim; false in the middle.</returns>
    private bool IsRim(GraphNodeViewModel node, Point screen) {
        var world = ToWorld(screen);
        var dx = world.X - node.X;
        var dy = world.Y - node.Y;

        return dx * dx + dy * dy >= RimInnerRadius * RimInnerRadius;
    }

    /// <summary>The tie whose line stands under a point on the glass.</summary>
    /// <param name="screen">The point on the glass.</param>
    /// <returns>The tie there, or nothing.</returns>
    private GraphEdgeViewModel? EdgeAt(Point screen) {
        if (State is not { } state) {
            return null;
        }

        var world = ToWorld(screen);
        // Squared, like everything that only asks whether it is near: the
        // square root is the one thing a hit test never needs.
        var slopSquared = (EdgeHitSlop / _zoom) * (EdgeHitSlop / _zoom);

        foreach (var edge in state.Edges) {
            var a = new Vector(edge.From.X, edge.From.Y);
            var b = new Vector(edge.To.X, edge.To.Y);
            var along = b - a;
            var lengthSquared = along.X * along.X + along.Y * along.Y;
            if (lengthSquared < 0.01) {
                continue;
            }

            var point = new Vector(world.X, world.Y);
            var offset = point - a;
            var t = Math.Clamp(
                (offset.X * along.X + offset.Y * along.Y) / lengthSquared, 0, 1);
            var nearest = a + along * t;
            var stray = point - nearest;
            if (stray.X * stray.X + stray.Y * stray.Y <= slopSquared) {
                return edge;
            }
        }

        return null;
    }

    /// <summary>
    /// The dot of the tie whose curve stands under a point on the glass —
    /// only the selected tie wears one, so only the selected tie bends.
    /// </summary>
    /// <param name="screen">The point on the glass.</param>
    /// <returns>The tie whose dot is there, or nothing.</returns>
    private GraphEdgeViewModel? HandleAt(Point screen) {
        if (State is not { SelectedEdge: { } edge } ||
            !TryTieLine(edge, out var start, out var end, out var control, out _)) {
            return null;
        }

        var reach = BendDotRadius + BendDotSlop;
        var at = CurveMiddle(start, end, control);
        var dx = screen.X - at.X;
        var dy = screen.Y - at.Y;

        return dx * dx + dy * dy <= reach * reach ? edge : null;
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

        if (edge.Label.Length == 0 ||
            !TryTieLine(edge, out var start, out var end, out var control, out var unit)) {
            box = default;
            return false;
        }

        // Measured in ink nobody sees: what words measure never depends on
        // the colour they are drawn in, only on what they say.
        var text = Label(edge.Label, 11, Brushes.Transparent);
        box = Pill(WordsAt(CurveMiddle(start, end, control), unit, edge), text.Width, text.Height);
        return true;
    }

    /// <summary>The tie whose words stand under a point on the glass.</summary>
    /// <param name="screen">The point on the glass.</param>
    /// <returns>The tie there, or nothing.</returns>
    private GraphEdgeViewModel? LabelAt(Point screen) {
        if (State is not { } state) {
            return null;
        }

        // Topmost first: the words drawn last are the words a hand means.
        for (var i = state.Edges.Count - 1; i >= 0; i--) {
            var edge = state.Edges[i];
            if (edge.Label.Length > 0 && TryGetLabelBox(edge, out var box) && box.Contains(screen)) {
                return edge;
            }
        }

        return null;
    }

    /// <summary>
    /// The small vocabulary a node has, asked at the place the hand
    /// pointed: to join it to somebody — one end already standing — to
    /// change what it says about itself, or to take it away whole. The
    /// menu itself opens wherever the pointer stood, which is where the
    /// question was asked.
    /// </summary>
    /// <param name="state">The state the menu's answers go through.</param>
    /// <param name="node">The node the hand asked about.</param>
    private void ShowNodeMenu(GraphState state, GraphNodeViewModel node) {
        var connect = new MenuItem {
            Header = "Connect from here\u2026",
            IsEnabled = state.Nodes.Count >= 2
        };
        connect.Click += (_, _) => _ = state.ConnectFromAsync(node);

        var edit = new MenuItem { Header = "Edit\u2026" };
        edit.Click += (_, _) => {
            state.SelectNode(node);
            _ = state.EditCommand.ExecuteAsync(null);
        };

        var erase = new MenuItem { Header = "Delete" };
        erase.Click += (_, _) => {
            state.SelectNode(node);
            _ = state.DeleteSelectionCommand.ExecuteAsync(null);
        };

        var menu = new ContextMenu { ItemsSource = new object[] { connect, edit, erase } };
        menu.Open(this);
    }

    /// <summary>
    /// Opens the words of whichever tie's words stand under a point — the
    /// box over the pill itself, no dialog across the paper — and says
    /// whether there were words there at all, for the presses that go
    /// looking before they decide what else to do.
    /// </summary>
    /// <param name="state">The state the pointing goes through.</param>
    /// <param name="position">Where on the glass the hand stands.</param>
    /// <returns>True when the words were found and opened.</returns>
    private bool TryOpenWords(GraphState state, Point position) {
        if (LabelAt(position) is { } words) {
            state.BeginLabelEdit(words);
            return true;
        }

        return false;
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
        InvalidateVisual();
    }

    /// <summary>
    /// The tie the hand is dragging out of a node's rim: a line over
    /// everything, an arrow where it would arrive, and the node that would
    /// answer rung where it is touched — the tie that is about to be,
    /// shown before it is asked for.
    /// </summary>
    /// <param name="context">What to draw with.</param>
    /// <param name="palette">The ink this frame wears.</param>
    /// <param name="from">The node the tie is dragged out of.</param>
    /// <param name="cursor">Where the hand stands.</param>
    /// <param name="hover">The node under the hand, or nothing over empty paper.</param>
    private void DrawLinking(
        DrawingContext context,
        Palette palette,
        GraphNodeViewModel from,
        Point cursor,
        GraphNodeViewModel? hover) {
        var source = ToScreen(new Point(from.X, from.Y));
        var toward = hover is { } over ? ToScreen(new Point(over.X, over.Y)) : cursor;

        var deltaX = toward.X - source.X;
        var deltaY = toward.Y - source.Y;
        var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        var radius = NodeRadius * _zoom;
        if (distance <= radius + 1) {
            // The hand is still inside the circle it came out of — the
            // line has nowhere to point yet.
            return;
        }

        var unit = new Vector(deltaX / distance, deltaY / distance);
        var start = source + unit * radius;
        var end = hover is null ? cursor : toward - unit * (radius + 4);

        context.DrawLine(new Pen(palette.EdgeSelected, 2), start, end);
        DrawArrowHead(context, palette.EdgeSelected, end, start);

        if (hover is not null) {
            // The node that would answer, rung where it is touched.
            var ring = radius + 5;
            context.DrawEllipse(null, new Pen(palette.EdgeSelected, 2.5), toward, ring, ring);
        }
    }

    /// <summary>Where a point on the glass stands in the world.</summary>
    /// <param name="screen">The point on the glass.</param>
    /// <returns>The same place, in the world's own units.</returns>
    private Point ToWorld(Point screen) =>
        (Point)((Vector)(screen - _offset) / _zoom);

    /// <summary>Where a place in the world stands on the glass.</summary>
    /// <param name="world">The place in the world.</param>
    /// <returns>The same place, on the glass.</returns>
    private Point ToScreen(Point world) =>
        (Point)((Vector)world * _zoom) + _offset;

    /// <summary>
    /// Stands the middle of the world in the middle of the glass the first
    /// time there is a glass to stand it in — once, because the writer's
    /// own pans and zooms are theirs to keep.
    /// </summary>
    private void CenterOnce() {
        if (_centered || Bounds.Width <= 0 || Bounds.Height <= 0) {
            return;
        }

        _centered = true;
        _offset = new Point(
            Bounds.Width / 2 - GraphState.WorldWidth / 2 * _zoom,
            Bounds.Height / 2 - GraphState.WorldHeight / 2 * _zoom);
    }

    /// <summary>
    /// The words, drawn at a size, in a brush — cached by what they say
    /// and the ink they say it in, because a brush the theme has replaced
    /// would draw the old colour's words however fresh the frame is.
    /// </summary>
    /// <param name="words">What to draw.</param>
    /// <param name="size">How big to draw them.</param>
    /// <param name="brush">The brush to draw them in.</param>
    /// <returns>The words, ready for the drawing.</returns>
    private FormattedText Label(string words, double size, IBrush brush) {
        var key = $"{size}:{brush.GetHashCode()}:{words}";

        if (!_labels.TryGetValue(key, out var entry) || !ReferenceEquals(entry.Brush, brush)) {
            entry = (brush, new FormattedText(
                words,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default),
                size,
                brush));
            _labels[key] = entry;
        }

        return entry.Text;
    }

    /// <summary>The brushes and pens of one drawing pass, off the theme the canvas stands in.</summary>
    /// <returns>The palette to draw this frame with.</returns>
    private Palette ResolvePalette() => new(
        Brush("Sx.AccentSoftBrush", Color.Parse("#FFEBF3FC")),
        new Pen(Brush("Sx.BorderSoftBrush", Color.Parse("#FFE1DFDD")), 1.5),
        new Pen(Brush("Sx.AccentBrush", Color.Parse("#FF0F6CBD")), 2.5),
        Brush("Sx.TextTertiaryBrush", Color.Parse("#FFA19F9D")),
        Brush("Sx.AccentBrush", Color.Parse("#FF0F6CBD")),
        Brush("Sx.TextSecondaryBrush", Color.Parse("#FF605E5C")),
        Brush("Sx.TextPrimaryBrush", Color.Parse("#FF201F1E")),
        Brush("Sx.TextTertiaryBrush", Color.Parse("#FFA19F9D")),
        Brush("Sx.SurfaceBrush", Color.Parse("#FFFFFFFF")));

    /// <summary>A brush off the theme, falling back to the palette's own word for it.</summary>
    /// <param name="key">The theme's name for the brush.</param>
    /// <param name="fallback">The colour to wear if the theme has no such brush.</param>
    /// <returns>The brush to draw in.</returns>
    private IBrush Brush(string key, Color fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush
            ? brush
            : new SolidColorBrush(fallback);

    /// <summary>What one frame is drawn in.</summary>
    /// <param name="NodeFill">The circle's inside.</param>
    /// <param name="NodeStroke">The circle's own edge.</param>
    /// <param name="NodeSelected">A selected circle's edge, drawn heavier.</param>
    /// <param name="Edge">A tie's line and words.</param>
    /// <param name="EdgeSelected">A selected tie's line and words.</param>
    /// <param name="Name">A node's name under it.</param>
    /// <param name="Text">A tie's words, inked on their own pill.</param>
    /// <param name="Hint">The empty paper's own words.</param>
    /// <param name="Surface">The pill a tie's words stand on, so no circle may swallow them.</param>
    private sealed record Palette(
        IBrush NodeFill,
        IPen NodeStroke,
        IPen NodeSelected,
        IBrush Edge,
        IBrush EdgeSelected,
        IBrush Name,
        IBrush Text,
        IBrush Hint,
        IBrush Surface);
}
