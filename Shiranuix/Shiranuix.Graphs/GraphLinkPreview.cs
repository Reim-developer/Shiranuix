using Avalonia;
using Avalonia.Media;

namespace Shiranuix.Graphs;

/// <summary>
/// The tie the hand is dragging out of a node's rim, waiting to be one.
/// </summary>
/// <param name="From">The node the tie is dragged out of.</param>
/// <param name="Cursor">Where the hand stands.</param>
/// <param name="Hover">The node under the hand, or nothing over empty paper.</param>
public readonly record struct GraphLinkPreview(
    GraphNodeViewModel From,
    Point Cursor,
    GraphNodeViewModel? Hover);
