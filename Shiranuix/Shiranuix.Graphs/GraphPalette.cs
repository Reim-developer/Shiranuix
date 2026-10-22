using Avalonia.Controls;
using Avalonia.Media;

namespace Shiranuix.Graphs;

/// <summary>
/// What one frame is drawn in: the circle's inside and edge, a tie's line
/// and words, a node's letter and name, and the paper the words stand on.
/// </summary>
/// <remarks>
/// The palette is read from the theme the canvas stands in, and read afresh
/// every frame, so that a theme which changes under the map takes its ink
/// with it at once — nothing of the old theme's words is left standing on
/// the paper.
/// </remarks>
public sealed record GraphPalette(
    IBrush NodeFill,
    IPen NodeStroke,
    IPen NodeSelected,
    IBrush Edge,
    IBrush EdgeSelected,
    IBrush Name,
    IBrush Text,
    IBrush Hint,
    IBrush Surface) {
    /// <summary>
    /// Reads the palette off the theme a control stands in, falling back to
    /// the palette's own word for any brush the theme has not got.
    /// </summary>
    /// <param name="host">The control whose theme the map is drawn in.</param>
    /// <returns>The palette to draw this frame with.</returns>
    public static GraphPalette Resolve(Control host) {
        ArgumentNullException.ThrowIfNull(host);

        return new GraphPalette(
            Brush(host, "Sx.AccentSoftBrush", Color.Parse("#FFEBF3FC")),
            new Pen(Brush(host, "Sx.BorderSoftBrush", Color.Parse("#FFE1DFDD")), 1.5),
            new Pen(Brush(host, "Sx.AccentBrush", Color.Parse("#FF0F6CBD")), 2.5),
            Brush(host, "Sx.AccentBrush", Color.Parse("#FF0F6CBD")),
            Brush(host, "Sx.AccentBrush", Color.Parse("#FF0F6CBD")),
            Brush(host, "Sx.TextSecondaryBrush", Color.Parse("#FF605E5C")),
            Brush(host, "Sx.TextPrimaryBrush", Color.Parse("#FF201F1E")),
            Brush(host, "Sx.TextTertiaryBrush", Color.Parse("#FFA19F9D")),
            Brush(host, "Sx.SurfaceBrush", Color.Parse("#FFFFFFFF")));
    }

    /// <summary>A brush off the theme, falling back to the palette's own word for it.</summary>
    /// <param name="host">The control whose theme the map is drawn in.</param>
    /// <param name="key">The theme's name for the brush.</param>
    /// <param name="fallback">The colour to wear if the theme has no such brush.</param>
    /// <returns>The brush to draw in.</returns>
    private static IBrush Brush(Control host, string key, Color fallback) =>
        host.TryFindResource(key, host.ActualThemeVariant, out var value) && value is IBrush brush
            ? brush
            : new SolidColorBrush(fallback);
}
