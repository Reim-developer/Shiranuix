using Avalonia;
using Avalonia.Headless;
using Shiranuix.Graphs;
using Shiranuix.Tests.Headless;

namespace Shiranuix.Tests.Graphs;

/// <summary>
/// What stands under the hand: a node and its rim, a tie's line, the dot a
/// tie is bent by, and the words a tie says — each found where the drawing
/// put it, so that a hand reaching for one finds the thing it reached for.
/// </summary>
/// <remarks>
/// The words are measured, and measuring asks the text engine for a font:
/// the test therefore stands on the headless platform, as the other tests
/// that draw do.
/// </remarks>
[Collection("Headless UI")]
public sealed class GraphHitTesterTests {
    private readonly GraphViewport _viewport = new();
    private readonly GraphEdgeGeometry _geometry;
    private readonly GraphHitTester _hits;

    public GraphHitTesterTests() {
        _geometry = new GraphEdgeGeometry(_viewport);
        _hits = new GraphHitTester(_viewport, _geometry, new GraphTextCache());
    }

    [Fact]
    public void The_Node_Under_The_Hand_Is_The_Node_Under_The_Hand() =>
        HeadlessUi.On(() => {
            var node = new GraphNodeViewModel(1, "Mara", "", 400, 300);

            Assert.Same(node, _hits.NodeAt([node], new Point(400, 300)));
            Assert.Null(_hits.NodeAt([node], new Point(500, 300)));
        });

    [Fact]
    public void A_Node_S_Rim_Is_Where_A_Tie_Is_Dragged_Out_Of() =>
        HeadlessUi.On(() => {
            var node = new GraphNodeViewModel(1, "Mara", "", 400, 300);

            Assert.True(_hits.IsRim(node, new Point(400 + GraphMetrics.RimInnerRadius + 1, 300)));
            Assert.False(_hits.IsRim(node, new Point(400, 300)));
        });

    [Fact]
    public void The_Tie_Under_The_Hand_Is_The_Tie_Under_The_Hand() =>
        HeadlessUi.On(() => {
            var edge = Tie();

            Assert.Same(edge, _hits.EdgeAt([edge], new Point(500, 300)));
            Assert.Null(_hits.EdgeAt([edge], new Point(900, 500)));
        });

    [Fact]
    public void The_Dot_Of_A_Tie_Is_Under_The_Hand_Where_It_Stands() =>
        HeadlessUi.On(() => {
            var edge = Tie();

            // The dot stands off the line, on the other side from the words.
            Assert.Same(edge, _hits.HandleAt([edge], new Point(498, 280)));
            Assert.Null(_hits.HandleAt([edge], new Point(500, 300)));
        });

    [Fact]
    public void The_Words_Of_A_Tie_Are_Under_The_Hand_Where_They_Stand() =>
        HeadlessUi.On(() => {
            var edge = Tie();

            // The words stand twenty world units off the line, on a pill of
            // their own over everything.
            Assert.Same(edge, _hits.LabelAt([edge], new Point(498, 320)));
            Assert.Null(_hits.LabelAt([edge], new Point(500, 300)));
        });

    /// <summary>One straight tie, from Mara to Tomas, a hundred apart.</summary>
    private static GraphEdgeViewModel Tie() {
        var from = new GraphNodeViewModel(1, "Mara", "", 400, 300);
        var to = new GraphNodeViewModel(2, "Tomas", "", 600, 300);

        return new GraphEdgeViewModel(1, from, to, "the teacher of");
    }
}
