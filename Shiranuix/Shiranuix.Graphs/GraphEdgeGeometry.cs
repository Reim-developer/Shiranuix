using Avalonia;

namespace Shiranuix.Graphs;

/// <summary>
/// Where a tie's line runs, worked out of the camera and the two nodes it
/// joins.
/// </summary>
/// <remarks>
/// The geometry is asked of the camera rather than of the world, because
/// the drawing, the words and the dot are all inked on the glass: a curve
/// that the drawing put at one place and the hit test looked for at
/// another would be a curve the writer could never take hold of.
/// </remarks>
public sealed class GraphEdgeGeometry : IGraphEdgeGeometry {
    private readonly IGraphViewport _viewport;

    /// <summary>
    /// Creates the geometry over the camera the map is seen through.
    /// </summary>
    /// <param name="viewport">Where the world stands on the glass.</param>
    public GraphEdgeGeometry(IGraphViewport viewport) {
        _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
    }

    /// <inheritdoc />
    public bool TryLine(
        IReadOnlyList<GraphEdgeViewModel> edges,
        GraphEdgeViewModel edge,
        out Point start,
        out Point end,
        out Point? control,
        out Vector unit) {
        start = default;
        end = default;
        control = null;
        unit = default;

        ArgumentNullException.ThrowIfNull(edges);
        ArgumentNullException.ThrowIfNull(edge);

        var from = _viewport.ToScreen(new Point(edge.From.X, edge.From.Y));
        var to = _viewport.ToScreen(new Point(edge.To.X, edge.To.Y));

        // Worked out of the components rather than through the types: the
        // geometry only needs the length to know there is a line at all, and
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
        var radius = GraphMetrics.NodeRadius * _viewport.Zoom;
        start = from + unit * radius;
        end = to - unit * (radius + GraphMetrics.ArrowGap);

        var offset = edge.Bend + ParallelOffset(edges, edge);
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

    /// <inheritdoc />
    public double ParallelOffset(IReadOnlyList<GraphEdgeViewModel> edges, GraphEdgeViewModel edge) {
        var index = 0;
        var count = 0;

        foreach (var other in edges) {
            if (!edge.IsParallelTo(other)) {
                continue;
            }

            if (ReferenceEquals(other, edge)) {
                index = count;
            }

            count++;
        }

        return count > 1 ? (index - (count - 1) / 2.0) * GraphMetrics.ParallelSpread : 0;
    }

    /// <inheritdoc />
    public Point CurveMiddle(Point start, Point end, Point? control) =>
        control is { } through
            ? new Point(
                (start.X + 2 * through.X + end.X) / 4,
                (start.Y + 2 * through.Y + end.Y) / 4)
            : new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);

    /// <inheritdoc />
    public Point WordsAt(Point middle, Vector unit, GraphEdgeViewModel edge) =>
        middle + new Vector(-unit.Y, unit.X) * (edge.From.Id > edge.To.Id ? -GraphMetrics.LabelOffset : GraphMetrics.LabelOffset);

    /// <inheritdoc />
    public Point DotAt(Point middle, Vector unit, GraphEdgeViewModel edge) =>
        middle + new Vector(-unit.Y, unit.X) * (edge.From.Id > edge.To.Id ? GraphMetrics.DotOffset : -GraphMetrics.DotOffset);

    /// <inheritdoc />
    public Rect Pill(Point at, double textWidth, double textHeight) =>
        new(at.X - textWidth / 2 - 4, at.Y - textHeight / 2 - 2.5, textWidth + 8, textHeight + 5);

    /// <inheritdoc />
    public double BendToward(IReadOnlyList<GraphEdgeViewModel> edges, GraphEdgeViewModel edge, Point world) {
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
        return 2 * distance - ParallelOffset(edges, edge);
    }
}
