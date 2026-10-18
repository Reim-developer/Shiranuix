using SkiaSharp;

namespace Shiranuix.Services.Pdf;

/// <summary>
/// A paper PDF opened for reading: its pages counted, their sizes told, any
/// page drawn.
/// </summary>
/// <remarks>
/// Behind an interface for the same reason as every reading the view model
/// depends on: the reader state runs headlessly in tests, over a fake paper
/// whose pages are already known.
/// </remarks>
public interface IPdfPages {
    /// <summary>How many pages the paper carries.</summary>
    int Count { get; }

    /// <summary>
    /// The size one page is drawn in, in points as the paper holds it.
    /// </summary>
    /// <remarks>
    /// The size is asked for the places a gallery lays out, before any
    /// drawing runs; a page's place stands on its width against its height.
    /// </remarks>
    /// <param name="index">The page's place, counting from zero.</param>
    /// <returns>The page's size, its width by its height.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The page does not exist.</exception>
    SKSize PageSize(int index);

    /// <summary>
    /// Draws one page at the width asked.
    /// </summary>
    /// <param name="index">The page's place, counting from zero.</param>
    /// <param name="width">The width to draw it at, in pixels.</param>
    /// <returns>The page as drawn.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The page does not exist.</exception>
    SKBitmap Draw(int index, int width);
}
