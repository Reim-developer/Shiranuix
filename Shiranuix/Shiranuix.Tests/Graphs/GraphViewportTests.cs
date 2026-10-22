using Avalonia;
using Shiranuix.Graphs;

namespace Shiranuix.Tests.Graphs;

/// <summary>
/// The camera the map is seen through: the world's own units magnified or
/// shrunk, and carried onto the glass — and the whole of a graph brought
/// into the glass at once when it has been arranged afresh.
/// </summary>
public sealed class GraphViewportTests {
    [Fact]
    public void A_Place_In_The_World_Stands_Where_It_Was_Put() {
        var viewport = new GraphViewport();

        Assert.Equal(new Point(400, 300), viewport.ToScreen(new Point(400, 300)));
        Assert.Equal(new Point(400, 300), viewport.ToWorld(new Point(400, 300)));
    }

    [Fact]
    public void The_Camera_Survives_A_Round_Trip() {
        var viewport = new GraphViewport();
        viewport.CenterOn(new Rect(0, 0, 1200, 800), new Size(800, 600));

        var world = new Point(400, 300);

        Assert.Equal(world, viewport.ToWorld(viewport.ToScreen(world)));
    }

    [Fact]
    public void The_Middle_Of_The_World_Stands_In_The_Middle_Of_The_Glass() {
        var viewport = new GraphViewport();

        viewport.CenterOn(new Rect(0, 0, 1200, 800), new Size(800, 600));

        var middle = viewport.ToScreen(new Point(600, 400));

        Assert.Equal(400, middle.X);
        Assert.Equal(300, middle.Y);
    }

    [Fact]
    public void Looking_Closer_Keeps_The_Place_Under_The_Hand_Where_It_Is() {
        var viewport = new GraphViewport();
        viewport.CenterOn(new Rect(0, 0, 1200, 800), new Size(800, 600));

        var hand = new Point(100, 100);
        var under = viewport.ToWorld(hand);

        viewport.ZoomAt(hand, 1.5);

        Assert.True(viewport.Zoom > 1);
        Assert.Equal(under.X, viewport.ToWorld(hand).X, 6);
        Assert.Equal(under.Y, viewport.ToWorld(hand).Y, 6);
    }

    [Fact]
    public void The_Wheel_Cannot_Pull_Back_Past_The_Paper_Nor_Push_In_Past_It() {
        var viewport = new GraphViewport();

        for (var i = 0; i < 60; i++) {
            viewport.ZoomAt(new Point(0, 0), 1 / 1.12);
        }

        Assert.Equal(GraphMetrics.MinZoom, viewport.Zoom);

        for (var i = 0; i < 120; i++) {
            viewport.ZoomAt(new Point(0, 0), 1.12);
        }

        Assert.Equal(GraphMetrics.MaxZoom, viewport.Zoom);
    }

    [Fact]
    public void The_Paper_Moves_Under_The_Hand() {
        var viewport = new GraphViewport();

        viewport.PanBy(new Vector(30, -20));

        Assert.Equal(new Vector(30, -20), viewport.Offset);
    }

    [Fact]
    public void A_Graph_Is_Brought_Whole_Into_The_Glass() {
        var viewport = new GraphViewport();
        var glass = new Size(800, 600);

        viewport.Fit(new Rect(0, 0, 1200, 800), glass, GraphMetrics.NodeMargin);

        var first = viewport.ToScreen(new Point(0, 0));
        var last = viewport.ToScreen(new Point(1200, 800));

        Assert.True(first.X >= 0, $"the graph starts at {first.X}");
        Assert.True(first.Y >= 0, $"the graph starts at {first.Y}");
        Assert.True(last.X <= glass.Width, $"the graph ends at {last.X}");
        Assert.True(last.Y <= glass.Height, $"the graph ends at {last.Y}");
    }

    [Fact]
    public void A_Small_Graph_Is_Shown_At_Its_Own_Size_And_Not_Bigger() {
        var viewport = new GraphViewport();

        viewport.Fit(new Rect(100, 100, 50, 50), new Size(800, 600), GraphMetrics.NodeMargin);

        Assert.Equal(1, viewport.Zoom);
    }

    [Fact]
    public void A_Graph_Is_Left_Alone_When_There_Is_No_Glass_To_Bring_It_Into() {
        var viewport = new GraphViewport();

        viewport.Fit(new Rect(0, 0, 1200, 800), new Size(0, 0), GraphMetrics.NodeMargin);

        Assert.Equal(1, viewport.Zoom);
    }
}
