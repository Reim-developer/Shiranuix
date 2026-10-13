using Avalonia;
using Shiranuix.Models;
using Shiranuix.Utilities;

namespace Shiranuix.Tests.Utilities;

/// <summary>
/// Tests for the pure fit arithmetic in <see cref="ImageFitting"/>.
/// </summary>
public sealed class ImageFittingTests {
    private static readonly Size Viewport = new(800, 600);

    [Fact]
    public void FitScreen_Scales_Down_To_Fit_And_Preserves_Aspect() {
        // A tall portrait image: limited by height.
        var source = new Size(400, 800);
        Size box = ImageFitting.ComputeDisplaySize(source, Viewport, FitMode.FitScreen);

        // Height becomes 600; width scales by the same factor (0.75).
        Assert.Equal(600, box.Height, 3);
        Assert.Equal(300, box.Width, 3);
    }

    [Fact]
    public void FitScreen_Scales_Up_When_Smaller_Than_Viewport() {
        var source = new Size(400, 300);
        Size box = ImageFitting.ComputeDisplaySize(source, Viewport, FitMode.FitScreen);

        // Doubles to fill the 800x600 viewport exactly.
        Assert.Equal(800, box.Width, 3);
        Assert.Equal(600, box.Height, 3);
    }

    [Fact]
    public void FitWidth_Matches_Viewport_Width() {
        var source = new Size(1000, 2000);
        Size box = ImageFitting.ComputeDisplaySize(source, Viewport, FitMode.FitWidth);

        Assert.Equal(Viewport.Width, box.Width, 3);
        // Height scales proportionally (0.8) and overflows the viewport.
        Assert.Equal(1600, box.Height, 3);
    }

    [Fact]
    public void FitHeight_Matches_Viewport_Height() {
        var source = new Size(2000, 1000);
        Size box = ImageFitting.ComputeDisplaySize(source, Viewport, FitMode.FitHeight);

        Assert.Equal(Viewport.Height, box.Height, 3);
        // Height scales to 600 (factor 0.6); width scales proportionally to 1200.
        Assert.Equal(1200, box.Width, 3);
    }

    [Fact]
    public void OriginalSize_Returns_Source_Unscaled() {
        var source = new Size(1234, 5678);
        Size box = ImageFitting.ComputeDisplaySize(source, Viewport, FitMode.OriginalSize);

        Assert.Equal(source, box);
    }

    [Fact]
    public void Degenerate_Source_Returns_Empty() {
        Assert.Equal(
            default,
            ImageFitting.ComputeDisplaySize(new Size(0, 100), Viewport, FitMode.FitScreen));
    }

    [Fact]
    public void Unlaid_Out_Viewport_Returns_Empty_Except_OriginalSize() {
        var source = new Size(100, 100);
        var emptyViewport = default(Size);

        Assert.Equal(
            default,
            ImageFitting.ComputeDisplaySize(source, emptyViewport, FitMode.FitScreen));
        // Original size needs no viewport, so it still works.
        Assert.Equal(source, ImageFitting.ComputeDisplaySize(source, emptyViewport, FitMode.OriginalSize));
    }
}
