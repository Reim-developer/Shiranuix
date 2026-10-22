namespace Shiranuix.Graphs;

/// <summary>
/// The arrangement that places a graph's nodes: a force-directed layout,
/// run whole in one pass over the graph.
/// </summary>
/// <remarks>
/// <para>
/// The old stand-by — every pair of nodes pushing apart, every tie pulling
/// its two ends together, and a temperature that cools while the graph
/// settles so it stops rather than oscillates. Chosen deliberately over
/// the cleverer arrangements because a writer's graph is tens of nodes
/// rather than thousands, and tens of nodes settle in one breath; the day
/// a graph arrives that does not, the arrangement's name in the call
/// changes and nothing else does.
/// </para>
/// <para>
/// Deterministic under its seed: the same graph arranged twice comes out
/// the same way twice, which is the whole of what makes this testable
/// without a canvas anywhere near.
/// </para>
/// </remarks>
public static class GraphLayout {
    /// <summary>One node's place after the arrangement.</summary>
    /// <param name="Id">The node placed there.</param>
    /// <param name="X">Its horizontal place, within the given area.</param>
    /// <param name="Y">Its vertical place, within the given area.</param>
    public readonly record struct Placed(int Id, double X, double Y);

    /// <summary>
    /// Places every node on the canvas: a fresh arrangement of the given
    /// graph, every node inside the given area.
    /// </summary>
    /// <param name="nodeIds">The nodes to place.</param>
    /// <param name="edges">The ties that pull their two ends together.</param>
    /// <param name="width">The area's width to arrange in.</param>
    /// <param name="height">The area's height to arrange in.</param>
    /// <param name="seed">The randomness's seed — same seed, same arrangement.</param>
    /// <param name="iterations">How long the graph is left to settle.</param>
    /// <returns>Every node with the place it was given, in the order asked.</returns>
    public static IReadOnlyList<Placed> Arrange(
        IReadOnlyList<int> nodeIds,
        IReadOnlyList<(int From, int To)> edges,
        double width,
        double height,
        int seed = 17,
        int iterations = 300) {
        ArgumentNullException.ThrowIfNull(nodeIds);
        ArgumentNullException.ThrowIfNull(edges);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        // A graph with nothing in it has nowhere to arrange, and a graph
        // with one node in it has only the middle to stand in.
        if (nodeIds.Count == 0) {
            return [];
        }

        if (nodeIds.Count == 1) {
            return [new Placed(nodeIds[0], width / 2, height / 2)];
        }

        // The length a tie would like to be: big enough that the whole
        // graph fills its area, worked out of the area and the node count
        // so it holds at any size.
        var ideal = Math.Sqrt(width * height / nodeIds.Count);

        var random = new Random(seed);
        var places = new Dictionary<int, (double X, double Y)>(nodeIds.Count);
        foreach (var id in nodeIds) {
            places[id] = (random.NextDouble() * width, random.NextDouble() * height);
        }

        // The furthest a node may travel in one pass, cooling to nothing:
        // early passes move whole clusters, later ones only settle them.
        var temperature = width / 10;
        var centerX = width / 2;
        var centerY = height / 2;

        for (var step = 0; step < iterations; step++) {
            var push = new Dictionary<int, (double X, double Y)>(nodeIds.Count);
            foreach (var id in nodeIds) {
                push[id] = (0, 0);
            }

            // Every pair pushes apart — closer pairs harder, so nothing
            // stands on anything else however the graph began.
            for (var i = 0; i < nodeIds.Count; i++) {
                for (var j = i + 1; j < nodeIds.Count; j++) {
                    var a = places[nodeIds[i]];
                    var b = places[nodeIds[j]];
                    var dx = a.X - b.X;
                    var dy = a.Y - b.Y;
                    var distance = Math.Max(Math.Sqrt(dx * dx + dy * dy), 0.01);
                    var force = ideal * ideal / distance;

                    push[nodeIds[i]] = (push[nodeIds[i]].X + dx / distance * force,
                        push[nodeIds[i]].Y + dy / distance * force);
                    push[nodeIds[j]] = (push[nodeIds[j]].X - dx / distance * force,
                        push[nodeIds[j]].Y - dy / distance * force);
                }
            }

            // Every tie pulls its two ends together — a stand-in for a tie
            // whose ends are the same node, or names no node at all, is
            // nothing to pull with.
            foreach (var (from, to) in edges) {
                if (from == to || !places.ContainsKey(from) || !places.ContainsKey(to)) {
                    continue;
                }

                var a = places[from];
                var b = places[to];
                var dx = a.X - b.X;
                var dy = a.Y - b.Y;
                var distance = Math.Max(Math.Sqrt(dx * dx + dy * dy), 0.01);
                var force = distance * distance / ideal;

                push[from] = (push[from].X - dx / distance * force,
                    push[from].Y - dy / distance * force);
                push[to] = (push[to].X + dx / distance * force,
                    push[to].Y + dy / distance * force);
            }

            // A weak pull toward the middle, so a graph of loose parts
            // gathers around the paper's centre rather than drifting off
            // its edges. Disconnected components still spread — they are
            // only kept in sight.
            foreach (var id in nodeIds) {
                var place = places[id];
                push[id] = (push[id].X + (centerX - place.X) * 0.02,
                    push[id].Y + (centerY - place.Y) * 0.02);
            }

            // Move each node by what pressed and pulled it, no further than
            // the temperature allows.
            foreach (var id in nodeIds) {
                var (dx, dy) = push[id];
                var length = Math.Sqrt(dx * dx + dy * dy);
                if (length < 0.001) {
                    continue;
                }

                var travel = Math.Min(length, temperature);
                var place = places[id];
                places[id] = (place.X + dx / length * travel, place.Y + dy / length * travel);
            }

            temperature *= 1.0 - (double)step / iterations;
        }

        // Every place back inside the area: the forces above do not know
        // where the paper's edges are, and the canvas does not draw what it
        // cannot be scrolled to.
        return [.. nodeIds.Select(id => {
            var (x, y) = places[id];
            return new Placed(id, Math.Clamp(x, 0, width), Math.Clamp(y, 0, height));
        })];
    }
}
