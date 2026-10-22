using Avalonia;

namespace Shiranuix.Graphs;

/// <summary>
/// Where a tie's line actually runs: pulled back to the circles' edges,
/// spread sideways among the ties standing between the same two nodes, and
/// bent either side of the straight by the writer's own hand.
/// </summary>
/// <remarks>
/// One place asked, so the drawing, the words and the dot all agree on
/// where the curve is — and so the hit test and the box the window opens
/// can never disagree with the drawing about it either.
/// </remarks>
public interface IGraphEdgeGeometry {
    /// <summary>Where one tie's line runs.</summary>
    /// <param name="edges">Every tie in the graph, for the ones standing beside it.</param>
    /// <param name="edge">The tie to trace.</param>
    /// <param name="start">Where the line leaves the first circle.</param>
    /// <param name="end">Where it arrives at the second.</param>
    /// <param name="control">The point the curve bows through, or nothing for a straight line.</param>
    /// <param name="unit">The direction the line runs in.</param>
    /// <returns>False when the two nodes stand on each other and there is no line at all.</returns>
    bool TryLine(
        IReadOnlyList<GraphEdgeViewModel> edges,
        GraphEdgeViewModel edge,
        out Point start,
        out Point end,
        out Point? control,
        out Vector unit);

    /// <summary>
    /// How far sideways one tie is drawn among the ties standing between
    /// the same two nodes — and zero when it stands alone.
    /// </summary>
    /// <param name="edges">Every tie in the graph.</param>
    /// <param name="edge">The tie to ask about.</param>
    /// <returns>The spread's own share, in world units.</returns>
    double ParallelOffset(IReadOnlyList<GraphEdgeViewModel> edges, GraphEdgeViewModel edge);

    /// <summary>Where the middle of a tie's curve stands on the glass.</summary>
    /// <param name="start">Where the line leaves the first circle.</param>
    /// <param name="end">Where it arrives at the second.</param>
    /// <param name="control">The point the curve bows through, or nothing for a straight line.</param>
    /// <returns>The middle — halfway along a straight tie, halfway between the bows of a curved one.</returns>
    Point CurveMiddle(Point start, Point end, Point? control);

    /// <summary>Where one tie's words stand: beside the middle of its curve.</summary>
    /// <param name="middle">Where the curve's middle stands on the glass.</param>
    /// <param name="unit">The direction the line runs in.</param>
    /// <param name="edge">The tie whose words these are.</param>
    /// <returns>The middle of the words.</returns>
    Point WordsAt(Point middle, Vector unit, GraphEdgeViewModel edge);

    /// <summary>Where a tie's bend dot stands: off to the other side from its words.</summary>
    /// <param name="middle">Where the curve's middle stands on the glass.</param>
    /// <param name="unit">The direction the line runs in.</param>
    /// <param name="edge">The tie whose dot this is.</param>
    /// <returns>The dot's own centre.</returns>
    Point DotAt(Point middle, Vector unit, GraphEdgeViewModel edge);

    /// <summary>The pill one tie's words are inked on, around their own middle.</summary>
    /// <param name="at">Where the words' middle stands.</param>
    /// <param name="textWidth">How wide the words measure.</param>
    /// <param name="textHeight">How tall they measure.</param>
    /// <returns>The pill's own bounds.</returns>
    Rect Pill(Point at, double textWidth, double textHeight);

    /// <summary>
    /// How far a tie must bend for its curve's middle to stand where the
    /// hand is holding it.
    /// </summary>
    /// <param name="edges">Every tie in the graph, for the ones standing beside it.</param>
    /// <param name="edge">The tie being bent.</param>
    /// <param name="world">Where the hand holds the middle, in the world's own units.</param>
    /// <returns>The bend to draw from now on.</returns>
    double BendToward(IReadOnlyList<GraphEdgeViewModel> edges, GraphEdgeViewModel edge, Point world);
}
