namespace Shiranuix.Models;

/// <summary>
/// One character standing in a graph: a name, whatever the writer wants to
/// remember about them, and where their node last stood on the canvas.
/// </summary>
public sealed class GraphCharacter {
    /// <summary>The row's key.</summary>
    public int Id { get; set; }

    /// <summary>The graph the character stands in.</summary>
    public int GraphId { get; set; }

    /// <summary>The graph itself, when a query has joined it.</summary>
    public CharacterGraph? Graph { get; set; }

    /// <summary>The character's name, drawn under their node.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The writer's own note about them — carried beside the canvas rather
    /// than drawn on it, and read in the dialog that edits the character.
    /// </summary>
    public string Note { get; set; } = string.Empty;

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
