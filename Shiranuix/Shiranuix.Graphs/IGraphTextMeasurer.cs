using Avalonia.Media;

namespace Shiranuix.Graphs;

/// <summary>
/// The words the map writes, measured: how wide and how tall they stand,
/// so that the pill a tie's words are inked on and the box the writer
/// rewrites them in can be put exactly where the drawing put them.
/// </summary>
public interface IGraphTextMeasurer {
    /// <summary>Measures some words, at a size, in a brush.</summary>
    /// <param name="words">What to measure.</param>
    /// <param name="size">How big the words are.</param>
    /// <param name="brush">The brush they would be drawn in.</param>
    /// <returns>The words, ready to be drawn or measured.</returns>
    FormattedText Measure(string words, double size, IBrush brush);

    /// <summary>Forgets every word measured — the palette has changed under them.</summary>
    void Clear();
}
