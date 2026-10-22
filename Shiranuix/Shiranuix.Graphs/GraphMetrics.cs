namespace Shiranuix.Graphs;

/// <summary>
/// The map's own measure: how wide a node is drawn, how near a tie's line
/// a press still counts as the tie, how far the words and the bend dot
/// stand off that line, and the two ends of the zoom the wheel may push
/// the paper to.
/// </summary>
/// <remarks>
/// The numbers live here rather than in the canvas because four pieces of
/// the map ask the same questions of them — the drawing, the hit test, the
/// camera and the arrangement — and a number that two of them kept apart
/// would be a number that could disagree with itself.
/// </remarks>
public static class GraphMetrics {
    /// <summary>The radius a node is drawn at, in the world's own units.</summary>
    public const double NodeRadius = 26;

    /// <summary>
    /// How far from a node's centre its rim begins — the band a press drags
    /// a tie out of rather than the node itself, kept thin and at the very
    /// edge so a hand reaching for a node still takes the node.
    /// </summary>
    public const double RimInnerRadius = 20;

    /// <summary>How near a tie's line a press still counts as the tie.</summary>
    public const double EdgeHitSlop = 7;

    /// <summary>How far apart parallel ties are drawn, in the world's own units.</summary>
    public const double ParallelSpread = 34;

    /// <summary>The dot a tie wears beside its middle, for bending.</summary>
    public const double BendDotRadius = 5;

    /// <summary>How far around the dot a hand may miss it and still take hold.</summary>
    public const double BendDotSlop = 4;

    /// <summary>How far a tie's words stand off its line, to one side.</summary>
    public const double LabelOffset = 20;

    /// <summary>
    /// How far a tie's bend dot stands off its line — the other side from
    /// the words, at the same remove, so the two never stand on each other
    /// and neither stands on the line it belongs to.
    /// </summary>
    public const double DotOffset = 20;

    /// <summary>How far the arrow of a tie stands off the node it arrives at.</summary>
    public const double ArrowGap = 4;

    /// <summary>The furthest the wheel may pull back, and the closest it may push in.</summary>
    public const double MinZoom = 0.15;

    /// <summary>The closest the wheel may push in; see <see cref="MinZoom"/>.</summary>
    public const double MaxZoom = 5;

    /// <summary>
    /// The room a node keeps from the paper's edge: its own circle, and the
    /// name that stands under it.
    /// </summary>
    public const double NodeMargin = 48;

    /// <summary>How big a node's letter is drawn, in the world's own units.</summary>
    public const double InitialSize = 16;

    /// <summary>How big a node's name is drawn under it.</summary>
    public const double NodeNameSize = 12;

    /// <summary>How big a tie's words are drawn along it.</summary>
    public const double EdgeLabelSize = 11;

    /// <summary>How big the empty paper's own words are drawn.</summary>
    public const double HintSize = 13;
}
