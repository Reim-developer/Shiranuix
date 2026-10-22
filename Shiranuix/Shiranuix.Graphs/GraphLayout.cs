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
/// Nodes arrive as whatever the caller knows them by — ids, names, whole
/// rows — and leave with themselves attached to the place they were given.
/// The physics works on plain arrays of numbers, and the one dictionary
/// that maps a caller's node to its slot is asked once at the door: the
/// passes below that run a thousand times ask nothing but arrays.
/// </para>
/// <para>
/// Deterministic under its seed: the same graph arranged twice comes out
/// the same way twice, which is the whole of what makes this testable
/// without a canvas anywhere near.
/// </para>
/// </remarks>
public static class GraphLayout {
    /// <summary>One node's place after the arrangement.</summary>
    /// <typeparam name="T">Whatever the caller knows the node by.</typeparam>
    /// <param name="Node">The node placed there.</param>
    /// <param name="X">Its horizontal place, within the given area.</param>
    /// <param name="Y">Its vertical place, within the given area.</param>
    public readonly record struct Placed<T>(T Node, double X, double Y) where T : notnull;

    /// <summary>
    /// Places every node on the canvas: a fresh arrangement of the given
    /// graph, every node inside the given area.
    /// </summary>
    /// <typeparam name="T">Whatever the caller knows its nodes by.</typeparam>
    /// <param name="nodes">The nodes to place, each once.</param>
    /// <param name="ties">The ties that pull their two ends together.</param>
    /// <param name="width">The area's width to arrange in.</param>
    /// <param name="height">The area's height to arrange in.</param>
    /// <param name="margin">
    /// The room to keep between every node and the area's edge — the room
    /// a node's own circle and the name under it need, so that nothing
    /// stands half off the paper.
    /// </param>
    /// <param name="seed">The randomness's seed — same seed, same arrangement.</param>
    /// <param name="iterations">How long the graph is left to settle.</param>
    /// <returns>Every node with the place it was given, in the order asked.</returns>
    public static IReadOnlyList<Placed<T>> Arrange<T>(
        IReadOnlyList<T> nodes,
        IReadOnlyList<(T From, T To)> ties,
        double width,
        double height,
        double margin = 0,
        int seed = 17,
        int iterations = 300)
        where T : notnull {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(ties);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegative(margin);

        // A graph with nothing in it has nowhere to arrange, and a graph
        // with one node in it has only the middle to stand in.
        if (nodes.Count == 0) {
            return [];
        }

        if (nodes.Count == 1) {
            return [new Placed<T>(nodes[0], width / 2, height / 2)];
        }

        // A margin wider than the paper itself would leave nowhere to
        // stand at all; the paper's own width is the whole of the room.
        margin = Math.Min(margin, Math.Min(width, height) / 2);

        // The length a tie would like to be: big enough that the whole
        // graph fills its area, worked out of the area and the node count
        // so it holds at any size.
        var ideal = Math.Sqrt(width * height / nodes.Count);

        // The one dictionary the whole run gets: a caller's node looked up
        // into its slot once here, so the thousand passes below never ask.
        var slot = new Dictionary<T, int>(nodes.Count);
        for (var i = 0; i < nodes.Count; i++) {
            slot[nodes[i]] = i;
        }

        var random = new Random(seed);
        var places = new double[nodes.Count * 2];
        for (var i = 0; i < nodes.Count; i++) {
            places[i * 2] = margin + random.NextDouble() * (width - margin * 2);
            places[i * 2 + 1] = margin + random.NextDouble() * (height - margin * 2);
        }

        // The ties as slot pairs, asked once: a stand-in for a tie whose
        // ends are the same node, or names no node at all, is nothing to
        // pull with, and it never reaches the passes below.
        var springs = new List<(int From, int To)>(ties.Count);
        foreach (var (from, to) in ties) {
            if (slot.TryGetValue(from, out var a) &&
                slot.TryGetValue(to, out var b) &&
                a != b) {
                springs.Add((a, b));
            }
        }

        // The furthest a node may travel in one pass, cooling to nothing:
        // early passes move whole clusters, later ones only settle them.
        var temperature = width / 10;
        var centerX = width / 2;
        var centerY = height / 2;
        var push = new double[nodes.Count * 2];

        for (var step = 0; step < iterations; step++) {
            Array.Clear(push);

            // Every pair pushes apart — closer pairs harder, so nothing
            // stands on anything else however the graph began.
            for (var i = 0; i < nodes.Count; i++) {
                for (var j = i + 1; j < nodes.Count; j++) {
                    var dx = places[i * 2] - places[j * 2];
                    var dy = places[i * 2 + 1] - places[j * 2 + 1];
                    var distance = Math.Max(Math.Sqrt(dx * dx + dy * dy), 0.01);
                    var force = ideal * ideal / distance;
                    var stepX = dx / distance * force;
                    var stepY = dy / distance * force;

                    push[i * 2] += stepX;
                    push[i * 2 + 1] += stepY;
                    push[j * 2] -= stepX;
                    push[j * 2 + 1] -= stepY;
                }
            }

            // Every tie pulls its two ends together.
            foreach (var (from, to) in springs) {
                var dx = places[from * 2] - places[to * 2];
                var dy = places[from * 2 + 1] - places[to * 2 + 1];
                var distance = Math.Max(Math.Sqrt(dx * dx + dy * dy), 0.01);
                var force = distance * distance / ideal;
                var stepX = dx / distance * force;
                var stepY = dy / distance * force;

                push[from * 2] -= stepX;
                push[from * 2 + 1] -= stepY;
                push[to * 2] += stepX;
                push[to * 2 + 1] += stepY;
            }

            // A weak pull toward the middle, so a graph of loose parts
            // gathers around the paper's centre rather than drifting off
            // its edges. Disconnected components still spread — they are
            // only kept in sight.
            for (var i = 0; i < nodes.Count; i++) {
                push[i * 2] += (centerX - places[i * 2]) * 0.02;
                push[i * 2 + 1] += (centerY - places[i * 2 + 1]) * 0.02;
            }

            // Move each node by what pressed and pulled it, no further than
            // the temperature allows.
            for (var i = 0; i < nodes.Count; i++) {
                var dx = push[i * 2];
                var dy = push[i * 2 + 1];
                var length = Math.Sqrt(dx * dx + dy * dy);
                if (length < 0.001) {
                    continue;
                }

                var travel = Math.Min(length, temperature);
                places[i * 2] += dx / length * travel;
                places[i * 2 + 1] += dy / length * travel;
            }

            temperature *= 1.0 - (double)step / iterations;
        }

        // Every place back inside the area: the forces above do not know
        // where the paper's edges are, and the canvas does not draw what it
        // cannot be scrolled to. The margin keeps every node clear of the
        // edge, so that no circle and no name stands half off the paper.
        var placed = new List<Placed<T>>(nodes.Count);
        for (var i = 0; i < nodes.Count; i++) {
            placed.Add(new Placed<T>(nodes[i],
                Math.Clamp(places[i * 2], margin, width - margin),
                Math.Clamp(places[i * 2 + 1], margin, height - margin)));
        }

        return placed;
    }
}
