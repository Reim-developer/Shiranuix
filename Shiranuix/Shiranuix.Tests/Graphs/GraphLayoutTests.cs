using Shiranuix.Graphs;

namespace Shiranuix.Tests.Graphs;

/// <summary>
/// The arrangement, against nothing but numbers — which is the whole point
/// of it living apart from the canvas: if a graph is arranged wrongly, the
/// test says so without a window being opened to look.
/// </summary>
/// <remarks>
/// The arrangement is deterministic under its seed, so "passes" here is
/// "passes every time, on every machine" rather than an average: the same
/// graph asked twice is compared against itself, never against a hope.
/// </remarks>
public sealed class GraphLayoutTests {
    [Fact]
    public void A_Graph_With_Nothing_In_It_Has_Nothing_To_Place() {
        var placed = GraphLayout.Arrange<int>([], [], 1000, 700);

        Assert.Empty(placed);
    }

    [Fact]
    public void The_One_Character_Stands_In_The_Middle() {
        var placed = GraphLayout.Arrange([7], [], 1000, 700);

        var only = Assert.Single(placed);
        Assert.Equal(7, only.Node);
        Assert.Equal(500, only.X);
        Assert.Equal(350, only.Y);
    }

    [Fact]
    public void The_Same_Graph_Asked_Twice_Comes_Out_The_Same_Way() {
        var nodes = new[] { 1, 2, 3, 4, 5, 6 };
        var edges = new[] { (1, 2), (2, 3), (3, 4), (4, 5), (5, 6), (1, 4) };

        var first = GraphLayout.Arrange(nodes, edges, 1000, 700);
        var second = GraphLayout.Arrange(nodes, edges, 1000, 700);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Every_Node_Lands_Inside_The_Paper() {
        var nodes = Enumerable.Range(1, 40).ToArray();
        var edges = Enumerable.Range(1, 39).Select(i => (i, i + 1)).ToArray();

        var placed = GraphLayout.Arrange(nodes, edges, 1200, 800);

        Assert.Equal(40, placed.Count);
        Assert.All(placed, node => {
            Assert.True(double.IsFinite(node.X), $"X of {node.Node} is {node.X}");
            Assert.True(double.IsFinite(node.Y), $"Y of {node.Node} is {node.Y}");
            Assert.InRange(node.X, 0, 1200);
            Assert.InRange(node.Y, 0, 800);
        });
    }

    [Fact]
    public void Every_Node_Keeps_Off_The_Paper_S_Edge_By_The_Margin_Asked_Of_It() {
        var nodes = Enumerable.Range(1, 40).ToArray();
        var edges = Enumerable.Range(1, 39).Select(i => (i, i + 1)).ToArray();

        var placed = GraphLayout.Arrange(nodes, edges, 1200, 800, 48);

        // The margin is the room a node's circle and the name under it need:
        // without it, a node standing against the edge stands half off the
        // paper.
        Assert.All(placed, node => {
            Assert.InRange(node.X, 48, 1152);
            Assert.InRange(node.Y, 48, 752);
        });
    }

    [Fact]
    public void Characters_Tied_Together_Stand_Nearer_Than_Characters_They_Are_Not() {
        // A chain: 1—2—3. The tie pulls1 and2 together, and1 and3 have
        // nothing but the repulsion between them holding them apart.
        var placed = GraphLayout.Arrange([1, 2, 3], [(1, 2), (2, 3)], 1000, 700);

        var byId = placed.ToDictionary(node => node.Node, node => (node.X, node.Y));
        var tied = Distance(byId[1], byId[2]);
        var strangers = Distance(byId[1], byId[3]);

        Assert.True(tied < strangers,
            $"1 and2 stand {tied} apart,1 and3 {strangers} — a tie should bring its ends nearer.");
    }

    [Fact]
    public void A_Tie_To_Self_Or_To_Nobody_Leaves_The_Arrangement_Whole() {
        // The canvas can offer a self-loop and a tie whose second end was
        // deleted a moment ago; neither is the arrangement's business to
        // fall over.
        var placed = GraphLayout.Arrange(
            [1, 2],
            [(1, 1), (9, 10)],
            1000,
            700);

        Assert.Equal(2, placed.Count);
        Assert.All(placed, node => Assert.True(double.IsFinite(node.X)));
    }

    [Fact]
    public void Whatever_The_Nodes_Were_Known_By_Travels_With_Their_Place() {
        // The arrangement asks nothing about what it is arranging: an id
        // one day, a name the next, a whole row after that — the place is
        // handed back with the very thing that was lent to it.
        var placed = GraphLayout.Arrange(
            ["Anna", "Boris", "Cleo"],
            [("Anna", "Boris"), ("Boris", "Cleo")],
            500,
            400);

        Assert.Equal(3, placed.Count);
        Assert.Equal("Anna", placed[0].Node);
        Assert.Equal("Boris", placed[1].Node);
        Assert.Equal("Cleo", placed[2].Node);
    }

    private static double Distance((double X, double Y) a, (double X, double Y) b) {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
