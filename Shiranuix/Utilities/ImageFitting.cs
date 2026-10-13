namespace Shiranuix.Utilities;

using Avalonia;
using Shiranuix.Models;

/// <summary>
/// <summary>
/// Pure arithmetic for fitting a source image into a viewport under a given
/// <see cref="FitMode"/>.
/// </summary>
/// <remarks>
/// Kept free of any control or layout object so the scaling rules can be unit
/// tested against known dimensions while staying fully headless. The reader
/// view calls this to size its image; tests call it directly.
/// </remarks>
public static class ImageFitting {
    /// <summary>
    /// Computes the on-screen display size for a source image within a
    /// viewport, honouring the requested fit mode.
    /// </summary>
    /// <remarks>
    /// Every result preserves the source aspect ratio (uniform scaling), so a
    /// caller can safely use <c>Stretch="Uniform"</c> to draw the image into
    /// the returned box without distortion.
    /// </remarks>
    /// <param name="source">Natural pixel size of the source image.</param>
    /// <param name="viewport">Available area to fit into.</param>
    /// <param name="mode">How to scale the image.</param>
    /// <returns>
    /// The display size in pixels; an empty size if the source is degenerate
    /// or the viewport is not yet laid out (except for
    /// <see cref="FitMode.OriginalSize"/>, which needs no viewport).
    /// </returns>
    public static Size ComputeDisplaySize(Size source, Size viewport, FitMode mode) {
        // Degenerate source: nothing to draw.
        if (source.Width <= 0 || source.Height <= 0) {
            return default;
        }

        // Original size needs no viewport at all.
        if (mode == FitMode.OriginalSize) {
            return source;
        }

        // Viewport not laid out yet (e.g. before the first size pass):
        // nothing sensible to compute for the other modes.
        if (viewport.Width <= 0 || viewport.Height <= 0) {
            return default;
        }

        return mode switch {
            // Uniform scale that fits entirely inside the viewport.
            FitMode.FitScreen => ScaleUniform(source, Math.Min(
                viewport.Width / source.Width,
                viewport.Height / source.Height)),

            // Match viewport width; vertical overflow becomes scrollable.
            FitMode.FitWidth => new Size(
                viewport.Width,
                source.Height * (viewport.Width / source.Width)),

            // Match viewport height; horizontal overflow becomes scrollable.
            FitMode.FitHeight => new Size(
                source.Width * (viewport.Height / source.Height),
                viewport.Height),

            _ => source
        };
    }

    /// <summary>
    /// Scales both dimensions of <paramref name="source"/> by
    /// <paramref name="factor"/>.
    /// </summary>
    private static Size ScaleUniform(Size source, double factor) =>
        new(source.Width * factor, source.Height * factor);
}
