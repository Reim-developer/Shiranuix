using Avalonia;
using Avalonia.Media;

namespace Shiranuix.Graphs;

/// <summary>
/// The frame: nodes as circles wearing the first letter of their names,
/// ties as arrows with their words along them, and the hand's own handles
/// on the curves over the lot.
/// </summary>
/// <remarks>
/// The drawing is one pass over the state's own collections every time
/// something twitches, which a writer's graph — tens of nodes, not
/// thousands — survives without a care. The order of that pass is the
/// order the eye reads in: the ties first, so no node ever stands on the
/// arrow that points at it; the nodes next; the words of the ties over
/// everything, so no circle swallows them; and the bend dots over even
/// that, so a hand may reach any curve's handle at once.
/// </remarks>
public sealed class GraphRenderer {
    private readonly IGraphViewport _viewport;
    private readonly IGraphEdgeGeometry _geometry;
    private readonly IGraphTextMeasurer _text;

    /// <summary>
    /// Creates the renderer over the camera the map is seen through.
    /// </summary>
    /// <param name="viewport">Where the world stands on the glass.</param>
    /// <param name="geometry">Where the ties' lines run.</param>
    /// <param name="text">How the words of the map measure.</param>
    public GraphRenderer(IGraphViewport viewport, IGraphEdgeGeometry geometry, IGraphTextMeasurer text) {
        _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        _text = text ?? throw new ArgumentNullException(nameof(text));
    }

    /// <summary>Draws everything the state holds, in one pass.</summary>
    /// <param name="context">What to draw with.</param>
    /// <param name="state">What the state holds.</param>
    /// <param name="palette">The ink this frame wears.</param>
    /// <param name="linking">The tie being dragged out of a rim, or nothing.</param>
    public void Draw(
        DrawingContext context,
        GraphState state,
        GraphPalette palette,
        GraphLinkPreview? linking) {
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
            DrawEdge(context, state, edge, palette, ReferenceEquals(edge, state.SelectedEdge), labels);
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

        // Every tie's dot, over everything: the hand's own handle on the
        // curve — grey for whoever has not been chosen, blue the moment it
        // is, so a hand may reach any curve's handle at once and always
        // see which handle it has hold of.
        foreach (var edge in state.Edges) {
            DrawBendDot(context, state, edge, palette, ReferenceEquals(edge, state.SelectedEdge));
        }

        // The tie being dragged out of a node's rim, over everything: it is
        // not yet a thing on the map but a question about to be one, and
        // the hand needs to see where it is pointing.
        if (linking is { } preview) {
            DrawLinking(context, palette, preview);
        }
    }

    /// <summary>
    /// Where one tie's words stand on the glass — the very pill the drawing
    /// inks, asked by the window when it is about to stand its box over
    /// them and let the writer rewrite them in place.
    /// </summary>
    /// <param name="state">The state the tie belongs to.</param>
    /// <param name="edge">The tie whose words to find.</param>
    /// <param name="box">Where they stand, in the canvas's own coordinates.</param>
    /// <returns>False when the tie carries no words, or has no line to stand beside.</returns>
    public bool TryGetLabelBox(GraphState state, GraphEdgeViewModel edge, out Rect box) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(edge);

        if (edge.Label.Length == 0 ||
            !_geometry.TryLine(state.Edges, edge, out var start, out var end, out var control, out var unit)) {
            box = default;
            return false;
        }

        // Measured in ink nobody sees: what words measure never depends on
        // the colour they are drawn in, only on what they say.
        var text = _text.Measure(edge.Label, GraphMetrics.EdgeLabelSize, Brushes.Transparent);
        box = _geometry.Pill(
            _geometry.WordsAt(_geometry.CurveMiddle(start, end, control), unit, edge),
            text.Width,
            text.Height);

        return true;
    }

    /// <summary>
    /// Fills in the words the empty paper has to say — no graph yet, or a
    /// graph with nobody standing in it yet.
    /// </summary>
    private void DrawEmpty(DrawingContext context, GraphPalette palette, GraphState state) {
        var words = state.HasCurrentGraph
            ? "Add a character from the toolbar above."
            : "Make a graph from the toolbar above to begin.";

        var text = _text.Measure(words, GraphMetrics.HintSize, palette.Hint);
        var center = _viewport.ToScreen(new Point(GraphState.WorldWidth / 2, GraphState.WorldHeight / 2));
        context.DrawText(text, center - new Vector(text.Width / 2, text.Height / 2));
    }

    /// <summary>Draws one tie: its curve and its arrow, leaving its words to the labels.</summary>
    /// <param name="labels">Where a tie's words are left for drawing over the nodes.</param>
    private void DrawEdge(
        DrawingContext context,
        GraphState state,
        GraphEdgeViewModel edge,
        GraphPalette palette,
        bool selected,
        List<(GraphEdgeViewModel Edge, Point At, bool Selected)> labels) {
        if (!_geometry.TryLine(state.Edges, edge, out var start, out var end, out var bend, out var unit)) {
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
            labels.Add((edge, _geometry.WordsAt(_geometry.CurveMiddle(start, end, bend), unit, edge), selected));
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
    /// Draws one tie's words: on a pill of their own, beside the line they
    /// name — after every node, so no circle may stand on them.
    /// </summary>
    private void DrawEdgeLabel(
        DrawingContext context,
        GraphEdgeViewModel edge,
        Point at,
        bool selected,
        GraphPalette palette) {
        var text = _text.Measure(edge.Label, GraphMetrics.EdgeLabelSize, selected ? palette.EdgeSelected : palette.Text);
        context.DrawRectangle(palette.Surface, null, _geometry.Pill(at, text.Width, text.Height), 4, 4);
        context.DrawText(text, at - new Vector(text.Width / 2, text.Height / 2));
    }

    /// <summary>Draws one node: the circle, the letter inside it, and the name underneath.</summary>
    private void DrawNode(
        DrawingContext context,
        GraphNodeViewModel node,
        GraphPalette palette,
        bool selected) {
        var center = _viewport.ToScreen(new Point(node.X, node.Y));
        var radius = GraphMetrics.NodeRadius * _viewport.Zoom;

        context.DrawEllipse(
            palette.NodeFill,
            selected ? palette.NodeSelected : palette.NodeStroke,
            center,
            radius,
            radius);

        var initial = node.Initial;
        if (initial.Length > 0) {
            // The letter is drawn through a transform of the camera's own
            // scale, so that it grows with the circle it stands in while
            // the words behind it stay one size whatever the writer has
            // zoomed to — and so that the words the map has measured are
            // measured once, at the one size they are measured at.
            var zoom = _viewport.Zoom;
            using (context.PushTransform(Matrix.CreateScale(zoom, zoom))) {
                var text = _text.Measure(initial, GraphMetrics.InitialSize, palette.Initial);
                context.DrawText(text, center / zoom - new Vector(text.Width / 2, text.Height / 2));
            }
        }

        var name = _text.Measure(node.Name, GraphMetrics.NodeNameSize, palette.Name);
        context.DrawText(name, center + new Vector(-name.Width / 2, radius + 4));
    }

    /// <summary>Draws the dot a tie is bent by, standing clear of its own line.</summary>
    private void DrawBendDot(
        DrawingContext context,
        GraphState state,
        GraphEdgeViewModel edge,
        GraphPalette palette,
        bool selected) {
        if (!_geometry.TryLine(state.Edges, edge, out var start, out var end, out var control, out var unit)) {
            return;
        }

        context.DrawEllipse(
            selected ? palette.EdgeSelected : palette.Edge,
            new Pen(palette.Surface, 1.5),
            _geometry.DotAt(_geometry.CurveMiddle(start, end, control), unit, edge),
            GraphMetrics.BendDotRadius,
            GraphMetrics.BendDotRadius);
    }

    /// <summary>
    /// The tie the hand is dragging out of a node's rim: a line over
    /// everything, an arrow where it would arrive, and the node that would
    /// answer rung where it is touched — the tie that is about to be,
    /// shown before it is asked for.
    /// </summary>
    private void DrawLinking(DrawingContext context, GraphPalette palette, GraphLinkPreview preview) {
        var source = _viewport.ToScreen(new Point(preview.From.X, preview.From.Y));
        var toward = preview.Hover is { } over
            ? _viewport.ToScreen(new Point(over.X, over.Y))
            : preview.Cursor;

        var deltaX = toward.X - source.X;
        var deltaY = toward.Y - source.Y;
        var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        var radius = GraphMetrics.NodeRadius * _viewport.Zoom;
        if (distance <= radius + 1) {
            // The hand is still inside the circle it came out of — the
            // line has nowhere to point yet.
            return;
        }

        var unit = new Vector(deltaX / distance, deltaY / distance);
        var start = source + unit * radius;
        var end = preview.Hover is null ? preview.Cursor : toward - unit * (radius + GraphMetrics.ArrowGap);

        context.DrawLine(new Pen(palette.EdgeSelected, 2), start, end);
        DrawArrowHead(context, palette.EdgeSelected, end, start);

        if (preview.Hover is not null) {
            // The node that would answer, rung where it is touched.
            var ring = radius + 5;
            context.DrawEllipse(null, new Pen(palette.EdgeSelected, 2.5), toward, ring, ring);
        }
    }
}
