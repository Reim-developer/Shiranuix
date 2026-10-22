using Avalonia;

namespace Shiranuix.Graphs;

/// <summary>
/// What stands under the hand on the glass: a node, a node's rim, a tie's
/// line, the dot a tie is bent by, or the words a tie says.
/// </summary>
public interface IGraphHitTester {
    /// <summary>The node standing under a point on the glass, topmost first.</summary>
    /// <param name="nodes">Every node in the graph.</param>
    /// <param name="screen">The point on the glass.</param>
    /// <returns>The node there, or nothing.</returns>
    GraphNodeViewModel? NodeAt(IReadOnlyList<GraphNodeViewModel> nodes, Point screen);

    /// <summary>
    /// Whether a press stands on a node's rim — the band a tie is dragged
    /// out of — rather than in its middle.
    /// </summary>
    /// <param name="node">The node the press landed on.</param>
    /// <param name="screen">The point on the glass.</param>
    /// <returns>True on the rim; false in the middle.</returns>
    bool IsRim(GraphNodeViewModel node, Point screen);

    /// <summary>The tie whose line stands under a point on the glass.</summary>
    /// <param name="edges">Every tie in the graph.</param>
    /// <param name="screen">The point on the glass.</param>
    /// <returns>The tie there, or nothing.</returns>
    GraphEdgeViewModel? EdgeAt(IReadOnlyList<GraphEdgeViewModel> edges, Point screen);

    /// <summary>
    /// The dot of whichever tie wears one under a point on the glass.
    /// </summary>
    /// <param name="edges">Every tie in the graph.</param>
    /// <param name="screen">The point on the glass.</param>
    /// <returns>The tie whose dot is there, or nothing.</returns>
    GraphEdgeViewModel? HandleAt(IReadOnlyList<GraphEdgeViewModel> edges, Point screen);

    /// <summary>The tie whose words stand under a point on the glass.</summary>
    /// <param name="edges">Every tie in the graph.</param>
    /// <param name="screen">The point on the glass.</param>
    /// <returns>The tie there, or nothing.</returns>
    GraphEdgeViewModel? LabelAt(IReadOnlyList<GraphEdgeViewModel> edges, Point screen);
}
