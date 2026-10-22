using Avalonia;
using Avalonia.Media;

namespace Shiranuix.Graphs;

/// <summary>
/// What stands under the hand: asked of the camera, so that the place a
/// writer reaches for is the place the drawing put it.
/// </summary>
public sealed class GraphHitTester : IGraphHitTester {
    private readonly IGraphViewport _viewport;
    private readonly IGraphEdgeGeometry _geometry;
    private readonly IGraphTextMeasurer _text;

    /// <summary>
    /// Creates the tester over the camera the map is seen through.
    /// </summary>
    /// <param name="viewport">Where the world stands on the glass.</param>
    /// <param name="geometry">Where the ties' lines run.</param>
    /// <param name="text">How the words of a tie measure.</param>
    public GraphHitTester(IGraphViewport viewport, IGraphEdgeGeometry geometry, IGraphTextMeasurer text) {
        _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        _text = text ?? throw new ArgumentNullException(nameof(text));
    }

    /// <inheritdoc />
    public GraphNodeViewModel? NodeAt(IReadOnlyList<GraphNodeViewModel> nodes, Point screen) {
        var world = _viewport.ToWorld(screen);
        var reach = GraphMetrics.NodeRadius + 2;
        var reachSquared = reach * reach;

        for (var i = nodes.Count - 1; i >= 0; i--) {
            var node = nodes[i];
            var dx = world.X - node.X;
            var dy = world.Y - node.Y;

            if (dx * dx + dy * dy <= reachSquared) {
                return node;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public bool IsRim(GraphNodeViewModel node, Point screen) {
        var world = _viewport.ToWorld(screen);
        var dx = world.X - node.X;
        var dy = world.Y - node.Y;

        return dx * dx + dy * dy >= GraphMetrics.RimInnerRadius * GraphMetrics.RimInnerRadius;
    }

    /// <inheritdoc />
    public GraphEdgeViewModel? EdgeAt(IReadOnlyList<GraphEdgeViewModel> edges, Point screen) {
        var world = _viewport.ToWorld(screen);

        // Squared, like everything that only asks whether it is near: the
        // square root is the one thing a hit test never needs.
        var slop = GraphMetrics.EdgeHitSlop / _viewport.Zoom;
        var slopSquared = slop * slop;

        foreach (var edge in edges) {
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

    /// <inheritdoc />
    public GraphEdgeViewModel? HandleAt(IReadOnlyList<GraphEdgeViewModel> edges, Point screen) {
        var reach = GraphMetrics.BendDotRadius + GraphMetrics.BendDotSlop;
        var reachSquared = reach * reach;

        // Topmost first — the same order the dots are drawn in.
        for (var i = edges.Count - 1; i >= 0; i--) {
            var edge = edges[i];
            if (!_geometry.TryLine(edges, edge, out var start, out var end, out var control, out var unit)) {
                continue;
            }

            var at = _geometry.DotAt(_geometry.CurveMiddle(start, end, control), unit, edge);
            var dx = screen.X - at.X;
            var dy = screen.Y - at.Y;

            if (dx * dx + dy * dy <= reachSquared) {
                return edge;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public GraphEdgeViewModel? LabelAt(IReadOnlyList<GraphEdgeViewModel> edges, Point screen) {
        // Topmost first: the words drawn last are the words a hand means.
        for (var i = edges.Count - 1; i >= 0; i--) {
            var edge = edges[i];
            if (edge.Label.Length == 0) {
                continue;
            }

            if (!TryLabelBox(edges, edge, out var box)) {
                continue;
            }

            if (box.Contains(screen)) {
                return edge;
            }
        }

        return null;
    }

    /// <summary>
    /// Where one tie's words stand on the glass — the very pill the drawing
    /// inks, asked by the window when it is about to stand its box over
    /// them and let the writer rewrite them in place.
    /// </summary>
    /// <param name="edges">Every tie in the graph.</param>
    /// <param name="edge">The tie whose words to find.</param>
    /// <param name="box">Where they stand, in the canvas's own coordinates.</param>
    /// <returns>False when the tie carries no words, or has no line to stand beside.</returns>
    public bool TryLabelBox(IReadOnlyList<GraphEdgeViewModel> edges, GraphEdgeViewModel edge, out Rect box) {
        if (edge.Label.Length == 0 ||
            !_geometry.TryLine(edges, edge, out var start, out var end, out var control, out var unit)) {
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
}
