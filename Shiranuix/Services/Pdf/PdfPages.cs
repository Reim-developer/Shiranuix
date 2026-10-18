using System.IO;
using System.Runtime.Versioning;
using PdfSharp.Pdf.IO;
using PDFtoImage;
using SkiaSharp;

namespace Shiranuix.Services.Pdf;

/// <summary>
/// A paper read by PDFium: the file is held once, its pages are counted,
/// their sizes told in points, and a page is drawn only when the reader
/// stands on it.
/// </summary>
/// <remarks>
/// <para>
/// A paper is not the writing: nothing here reaches the document or the
/// window engine. The reader holds this and asks for pages as the window
/// stands on them.
/// </para>
/// <para>
/// A page's size is read from the paper's own structure, once at the
/// opening, so a gallery can lay its places out before any drawing runs; a
/// paper whose structure cannot be weighed is measured instead by a tiny
/// drawing of its first page.
/// </para>
/// <para>
/// PDFium is not thread-safe; PDFtoImage serializes the calls it makes
/// within each process, so no lock is carried here in addition.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class PdfPages : IPdfPages {
    private readonly byte[] _paper;
    private readonly SKSize[] _sizes;

    /// <summary>
    /// Opens the paper a file holds.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <exception cref="IOException">The file could not be read.</exception>
    /// <exception cref="InvalidDataException">The file holds no paper PDFium can read.</exception>
    public PdfPages(string path) {
        ArgumentNullException.ThrowIfNull(path);
        _paper = File.ReadAllBytes(path);

        try {
            Count = Conversion.GetPageCount(_paper);
            _sizes = ReadSizes(_paper, Count);
        } catch (Exception flaw) {
            // PDFium reads papers; anything else the file holds — text named
            // like a paper, a truncated save — is refused as what it is,
            // rather than as some machinery's remark on the inside.
            throw new InvalidDataException("This file is not a paper PDFium could read.", flaw);
        }
    }

    /// <inheritdoc />
    public int Count { get; }

    /// <inheritdoc />
    public SKSize PageSize(int index) {
        if (index < 0 || index >= Count) {
            throw new ArgumentOutOfRangeException(nameof(index), "The paper holds no such page.");
        }

        // The size stands once read: naming a page's size asks no renderer.
        return _sizes[index];
    }

    /// <inheritdoc />
    public SKBitmap Draw(int index, int width) {
        if (index < 0 || index >= Count) {
            throw new ArgumentOutOfRangeException(nameof(index), "The paper holds no such page.");
        }
        if (width <= 0) {
            throw new ArgumentOutOfRangeException(nameof(width), "A page is drawn at a real width.");
        }

        var options = new RenderOptions { Width = width };
        return Conversion.ToImage(_paper, index, null, options);
    }

    /// <summary>
    /// Reads the size in points each page is held in, from the paper's own
    /// structure.
    /// </summary>
    /// <param name="paper">The bytes the file holds.</param>
    /// <param name="count">How many pages the paper carries.</param>
    /// <returns>One size per page, its width by its height.</returns>
    /// <remarks>
    /// PDFsharp reads the structure without drawing, so a gallery's places
    /// are known before any page is drawn. A page turned on its side carries
    /// its width as its height, and the shape it stands in is turned with it.
    /// A paper that cannot be weighed at all is measured by a tiny drawing of
    /// its first page, and every page is told the same shape — the fence of
    /// last resort for a paper PDFium can draw but the structure reader
    /// cannot follow.
    /// </remarks>
    private static SKSize[] ReadSizes(byte[] paper, int count) {
        try {
            using var stream = new MemoryStream(paper, writable: false);
            using var document = PdfReader.Open(stream);
            var sizes = new SKSize[count];

            for (var index = 0; index < count; index++) {
                sizes[index] = SizeOf(document.Pages[index]);
            }

            return sizes;
        } catch (Exception) {
            var tiny = Conversion.ToImage(paper, 0, null, new RenderOptions { Width = 16 });
            using (tiny) {
                var size = new SKSize(16f, (float)tiny.Height * 16f / tiny.Width);
                return Enumerable.Repeat(size, count).ToArray();
            }
        }
    }

    /// <summary>The size a page stands in, page turned and all.</summary>
    /// <param name="page">The page of the paper.</param>
    private static SKSize SizeOf(PdfSharp.Pdf.PdfPage page) {
        var width = page.Width.Point;
        var height = page.Height.Point;

        return width <= 0 || height <= 0
            ? new SKSize(595f, 842f)
            : page.Rotate is 90 or 270 or -90 or -270
                ? new SKSize((float)height, (float)width)
                : new SKSize((float)width, (float)height);
    }
}
