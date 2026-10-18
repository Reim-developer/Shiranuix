using System.IO;
using Shiranuix.Services.Pdf;
using SkiaSharp;

namespace Shiranuix.Tests.Fakes;

/// <summary>
/// A paper whose pages are already known, standing in for
/// <see cref="PdfPages"/>, so the viewer state reads without a renderer.
/// </summary>
public sealed class FakePdfPages : IPdfPages {
    /// <summary>
    /// Builds the fake over the page count it reports.
    /// </summary>
    /// <param name="count">The page count the paper carries.</param>
    public FakePdfPages(int count) => Count = count;

    /// <summary>How many pages the paper has.</summary>
    public int Count { get; }

    /// <summary>Every draw asked of it, in order.</summary>
    public List<(int Index, int Width)> Draws { get; } = new();

    /// <inheritdoc />
    public SKBitmap Draw(int index, int width) {
        Draws.Add((index, width));
        return new SKBitmap(4, 4);
    }
}
