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
/// the canvas does beyond drawing is three moves the writer already knows
/// from every other canvas: the wheel looks closer or further, empty paper
/// drags the paper itself, and a node drags wherever it is taken.
/// </para>
/// <para>
/// Everything above the glass — the toolbar, the picker, the dialogs —
/// belongs to the window; the canvas asks the state only for what it draws
/// and tells it only what the writer did to it: a node clicked, a tie
/// clicked, a node let go of, the paper double-clicked where somebody
/// should stand.
/// </para>
/// </remarks>
public sealed class GraphCanvas : Control {
    /// <summary>The radius a node is drawn at, in the world's own units.</summary>
    private const double NodeRadius = 26;

    /// <summary>How near a tie's line a press still counts as the tie.</summary>
    private const double EdgeHitSlop = 7;

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
        var groups = new Dictionary<(int, int), List<GraphEdgeViewModel>>();
        foreach (var edge in state.Edges) {
            var key = Pair(edge);
            if (!groups.TryGetValue(key, out var group)) {
                groups[key] = group = [];
            }

            group.Add(edge);
        }

        var labels = new List<(GraphEdgeViewModel Edge, Point At, bool Selected)>();

        foreach (var edge in state.Edges) {
            var group = groups[Pair(edge)];
            DrawEdge(context, edge, palette,
                ReferenceEquals(edge, state.SelectedEdge),
                group.IndexOf(edge),
                group.Count,
                labels);
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
    /// A press begins one of three things: a node to be taken, the paper to
    /// be dragged, or — on the second press of a double — a question or a
    /// place for somebody new.
    /// </summary>
    /// <param name="e">The press's own event.</param>
    protected override void OnPointerPressed(PointerPressedEventArgs e) {
        base.OnPointerPressed(e);

        var state = State;
        if (state is null) {
            return;
        }

        Focus();
        var position = e.GetCurrentPoint(this).Position;

        if (e.ClickCount == 2) {
            if (NodeAt(position) is { } node) {
                state.SelectNode(node);
                _ = state.EditCommand.ExecuteAsync(null);
            } else {
                var world = ToWorld(position);
                _ = state.AddCharacterAtAsync(world.X, world.Y);
            }

            e.Handled = true;
            return;
        }

        var pressed = e.GetCurrentPoint(this).Properties;
        if (!pressed.IsLeftButtonPressed && !pressed.IsMiddleButtonPressed) {
            return;
        }

        _gestureStart = position;
        _moved = false;

        if (pressed.IsLeftButtonPressed && NodeAt(position) is { } grabbed) {
            _dragging = grabbed;
        } else if (pressed.IsLeftButtonPressed && EdgeAt(position) is { } picked) {
            // A tie clicked: selected where it stands, and nothing dragged —
            // a line has no handle to take, only words to change.
            state.SelectEdge(picked);
            InvalidateVisual();
            e.Handled = true;
            return;
        } else {
            _panning = true;
        }

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <summary>
    /// The node under the hand goes where the hand goes; the paper under
    /// the hand goes the other way; and with neither, the cursor says what
    /// a press would do.
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
    /// and the store hears it; a node only clicked is selected; empty paper
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
            ? "Double-click the paper to add a character."
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
        int parallelIndex,
        int parallelCount,
        List<(GraphEdgeViewModel Edge, Point At, bool Selected)> labels) {
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
            return;
        }

        var unit = new Vector(deltaX / distance, deltaY / distance);

        // Both ends pulled back to the circles' edges, so an arrow touches
        // its nodes rather than disappearing into them.
        var radius = NodeRadius * _zoom;
        var start = from + unit * radius;
        var end = to - unit * (radius + 4);

        // Ties to the same two nodes curve apart, one beside the other —
        // sideways in the same frame whichever way the arrow points, so a
        // tie and its answer bend apart rather than over each other.
        Point? bend = null;
        if (parallelCount > 1) {
            var spread = (parallelIndex - (parallelCount - 1) / 2.0) * 34;
            var middle = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);
            var flip = edge.From.Id > edge.To.Id ? -1 : 1;
            bend = middle + new Vector(-unit.Y * flip, unit.X * flip) * spread;
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
        // the curve's own direction when the tie is one of a parallel pair.
        var approach = bend ?? start;
        var headX = end.X - approach.X;
        var headY = end.Y - approach.Y;
        var headingLength = Math.Sqrt(headX * headX + headY * headY);
        if (headingLength < 0.01) {
            return;
        }

        var arrow = new Vector(headX / headingLength, headY / headingLength);
        var back = end - arrow * 10;
        var side = new Vector(-arrow.Y, arrow.X) * 4.5;

        var shape = new StreamGeometry();
        using (var drawing = shape.Open()) {
            drawing.BeginFigure(end, isFilled: true);
            drawing.LineTo(back + side);
            drawing.LineTo(back - side);
            drawing.EndFigure(isClosed: true);
        }

        context.DrawGeometry(brush, null, shape);

        if (edge.Label.Length > 0) {
            // Beside its own line rather than on it: laid over the middle
            // of the tie the words fight the arrow for the eye, and where
            // a circle may stand they lose outright — so they are pushed
            // off to one side, in the same frame the parallel curves
            // spread in, which keeps a pair's words apart as well.
            var at = (bend is { } through
                ? new Point(
                    (start.X + 2 * through.X + end.X) / 4,
                    (start.Y + 2 * through.Y + end.Y) / 4)
                : new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2))
                + new Vector(-unit.Y, unit.X) * (edge.From.Id > edge.To.Id ? -13 : 13);

            labels.Add((edge, at, selected));
        }
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
        var pill = new Rect(
            at.X - text.Width / 2 - 4,
            at.Y - text.Height / 2 - 2.5,
            text.Width + 8,
            text.Height + 5);

        context.DrawRectangle(palette.Surface, null, pill, 4, 4);
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

    /// <summary>The two nodes a tie joins, either way round, as one key.</summary>
    /// <param name="edge">The tie to key.</param>
    /// <returns>The pair's key — the same for a tie and its mirror.</returns>
    private static (int, int) Pair(GraphEdgeViewModel edge) =>
        edge.From.Id < edge.To.Id ? (edge.From.Id, edge.To.Id) : (edge.To.Id, edge.From.Id);

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
