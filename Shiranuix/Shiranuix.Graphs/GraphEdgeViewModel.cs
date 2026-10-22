using CommunityToolkit.Mvvm.ComponentModel;

namespace Shiranuix.Graphs;

/// <summary>
/// One tie as the canvas carries it: the row the store knows it by, the two
/// nodes it joins, and the words drawn along it.
/// </summary>
/// <remarks>
/// The ends are nodes rather than ids, because the arrow is drawn between
/// two places that move — an id would have to go looking for the node every
/// time the canvas drew a frame, and the canvas draws a frame whenever
/// anything twitches.
/// </remarks>
public sealed partial class GraphEdgeViewModel : ObservableObject {
    /// <summary>
    /// Creates a tie over the row and the two nodes it joins.
    /// </summary>
    /// <param name="id">The relationship's row in the store.</param>
    /// <param name="from">The node the arrow points away from.</param>
    /// <param name="to">The node the arrow arrives at.</param>
    /// <param name="label">What the tie says, or nothing for a bare arrow.</param>
    /// <param name="bend">How far the line is curved off the straight one.</param>
    public GraphEdgeViewModel(
        int id,
        GraphNodeViewModel from,
        GraphNodeViewModel to,
        string label,
        double bend = 0) {
        Id = id;
        From = from ?? throw new ArgumentNullException(nameof(from));
        To = to ?? throw new ArgumentNullException(nameof(to));
        Label = label;
        Bend = bend;
    }

    /// <summary>The relationship's row in the store.</summary>
    public int Id { get; }

    /// <summary>The node the arrow points away from.</summary>
    public GraphNodeViewModel From { get; }

    /// <summary>The node the arrow arrives at.</summary>
    public GraphNodeViewModel To { get; }

    /// <summary>What the tie says, drawn along the arrow.</summary>
    [ObservableProperty]
    private string _label;

    /// <summary>
    /// How far the line is bent away from the straight one, in the canvas's
    /// own units and either side of it — dragged into place by the writer's
    /// hand on the dot the selected tie wears at its middle.
    /// </summary>
    [ObservableProperty]
    private double _bend;

    /// <summary>Whether this tie stands between the same two nodes as that one.</summary>
    /// <param name="other">The other tie to ask about.</param>
    /// <returns>True when both join the same pair, either way round.</returns>
    public bool IsParallelTo(GraphEdgeViewModel other) =>
        other is not null &&
        (ReferenceEquals(From, other.From) && ReferenceEquals(To, other.To) ||
         ReferenceEquals(From, other.To) && ReferenceEquals(To, other.From));
}
