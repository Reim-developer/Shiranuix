using Shiranuix.Services.Pdf;
using SkiaSharp;

namespace Shiranuix.Tests.Fakes;

/// <summary>
/// A paper whose pages and shapes are already known, standing in for
/// <see cref="PdfPages"/>, so the viewer state reads without a renderer.
/// </summary>
public sealed class FakePdfPages : IPdfPages {
    /// <summary>The shape a default page stands in, as the exporter's A4.</summary>
    public static readonly SKSize A4 = new(595f, 842f);

    /// <summary>
    /// Builds the fake over the page count it reports, every page of the
    /// exporter's own A4.
    /// </summary>
    /// <param name="count">The page count the paper carries.</param>
    public FakePdfPages(int count) : this(Enumerable.Repeat(A4, count).ToArray()) { }

    /// <summary>
    /// Builds the fake over the sizes its pages were drawn in.
    /// </summary>
    /// <param name="sizes">The size each page stands in, in points.</param>
    public FakePdfPages(IReadOnlyList<SKSize> sizes) {
        Sizes = sizes;
        Count = sizes.Count;
    }

    /// <summary>The size each page stands in.</summary>
    public IReadOnlyList<SKSize> Sizes { get; }

    /// <summary>How many pages the paper carries.</summary>
    public int Count { get; }

    /// <summary>Every draw asked of it, in order.</summary>
    public List<(int Index, int Width)> Draws { get; } = new();

    /// <inheritdoc />
    public SKSize PageSize(int index) =>
        index >= 0 && index < Count
            ? Sizes[index]
            : throw new ArgumentOutOfRangeException(nameof(index), "The fake holds no such page.");

    /// <inheritdoc />
    public SKBitmap Draw(int index, int width) {
        Draws.Add((index, width));
        return new SKBitmap(4, 4);
    }
}
