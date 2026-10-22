namespace Shiranuix.Models;

/// <summary>
/// One character standing in a graph: a place on the paper, and the wiki
/// entry they stand for.
/// </summary>
/// <remarks>
/// The character themselves are kept in the wiki — the name, the day they
/// were born, what the writer says about them — and this row says only
/// that they stand in this graph, and where. A character may stand in
/// several graphs at once, and is one character in all of them.
/// </remarks>
public sealed class GraphCharacter {
    /// <summary>The row's key.</summary>
    public int Id { get; set; }

    /// <summary>The graph the character stands in.</summary>
    public int GraphId { get; set; }

    /// <summary>The graph itself, when a query has joined it.</summary>
    public CharacterGraph? Graph { get; set; }

    /// <summary>The wiki entry this character stands for.</summary>
    public int WikiCharacterId { get; set; }

    /// <summary>
    /// The wiki entry itself, when a query has joined it — what the node
    /// is drawn under and what the writer knows about them.
    /// </summary>
    public WikiCharacter? WikiCharacter { get; set; }

    /// <summary>
    /// Where the node stands on the canvas, in the canvas's own world
    /// units. Written by the layout when the graph is arranged and by the
    /// writer's own hand when a node is dragged, so a graph opens where it
    /// was left rather than where a new arrangement happens to throw it.
    /// </summary>
    public double X { get; set; }

    /// <summary>The node's other coordinate; the same rules as <see cref="X"/>.</summary>
    public double Y { get; set; }
}
