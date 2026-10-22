using Avalonia;
using Shiranuix.Graphs;

namespace Shiranuix.Tests.Graphs;

/// <summary>
/// Where a tie's line runs: pulled back to the circles' edges, spread
/// sideways among the ties standing between the same two nodes, bowed
/// where the hand holds it, and keeping its words and its bend dot on
/// opposite sides of itself.
/// </summary>
public sealed class GraphEdgeGeometryTests {
    private readonly GraphViewport _viewport = new();
    private readonly GraphEdgeGeometry _geometry;

    public GraphEdgeGeometryTests() {
        _geometry = new GraphEdgeGeometry(_viewport);
    }

    [Fact]
    public void A_Tie_Runs_From_The_Rim_Of_One_Node_To_The_Rim_Of_Another() {
        var edge = Tie();

        Assert.True(_geometry.TryLine([edge], edge, out var start, out var end, out var control, out var unit));

        Assert.Equal(400 + GraphMetrics.NodeRadius, start.X);
        Assert.Equal(300, start.Y);
        Assert.Equal(600 - GraphMetrics.NodeRadius - GraphMetrics.ArrowGap, end.X);
        Assert.Equal(300, end.Y);
        Assert.Null(control);
        Assert.Equal(1, unit.X);
        Assert.Equal(0, unit.Y);
    }

    [Fact]
    public void A_Tie_Whose_Ends_Stand_On_Each_Other_Has_No_Line_At_All() {
        var from = new GraphNodeViewModel(1, 1, "Mara", "", "", "", 400, 300);
        var to = new GraphNodeViewModel(2, 1, "Tomas", "", "", "", 400, 300);
        var edge = new GraphEdgeViewModel(1, from, to, "the teacher of");

        Assert.False(_geometry.TryLine([edge], edge, out _, out _, out _, out _));
    }

    [Fact]
    public void Two_Ties_Between_The_Same_Two_Nodes_Are_Spread_Into_Parallel_Curves() {
        var from = new GraphNodeViewModel(1, 1, "Mara", "", "", "", 400, 300);
        var to = new GraphNodeViewModel(2, 1, "Tomas", "", "", "", 600, 300);

        var first = new GraphEdgeViewModel(1, from, to, "the teacher of");
        var second = new GraphEdgeViewModel(2, from, to, "the enemy of");

        // Alone, a tie stands on the straight line between its two nodes.
        Assert.Equal(0, _geometry.ParallelOffset([first], first));

        // Beside another, the two are spread apart: one to either side of
        // the straight line, so neither stands on the other.
        Assert.Equal(-GraphMetrics.ParallelSpread / 2, _geometry.ParallelOffset([first, second], first));
        Assert.Equal(GraphMetrics.ParallelSpread / 2, _geometry.ParallelOffset([first, second], second));
    }

    [Fact]
    public void The_Words_And_The_Dot_Stand_On_Opposite_Sides_Of_The_Line() {
        var edge = Tie();
        _geometry.TryLine([edge], edge, out var start, out var end, out var control, out var unit);

        var middle = _geometry.CurveMiddle(start, end, control);
        var words = _geometry.WordsAt(middle, unit, edge);
        var dot = _geometry.DotAt(middle, unit, edge);

        Assert.True(words.Y > middle.Y, "the words stand below the line");
        Assert.True(dot.Y < middle.Y, "the dot stands above the line");
    }

    [Fact]
    public void A_Bend_Is_Twice_The_Height_The_Hand_Holds_The_Middle_At() {
        var edge = Tie();

        // The hand holds the middle fifty world units below the line, and
        // a quadratic bows half as far as its control point bends.
        Assert.Equal(100, _geometry.BendToward([edge], edge, new Point(500, 350)));
    }

    [Fact]
    public void The_Pill_A_Ties_Words_Stand_On_Holds_Them_Whole() {
        var at = new Point(500, 320);

        var pill = _geometry.Pill(at, 80, 14);

        Assert.Equal(500, pill.Center.X);
        Assert.Equal(320, pill.Center.Y);
        Assert.True(pill.Width >= 80);
        Assert.True(pill.Height >= 14);
    }

    /// <summary>One straight tie, from Mara to Tomas, a hundred apart.</summary>
    private static GraphEdgeViewModel Tie() {
        var from = new GraphNodeViewModel(1, 1, "Mara", "", "", "", 400, 300);
        var to = new GraphNodeViewModel(2, 1, "Tomas", "", "", "", 600, 300);

        return new GraphEdgeViewModel(1, from, to, "the teacher of");
    }
}
