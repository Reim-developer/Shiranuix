using System.IO;
using System.Runtime.Versioning;
using PDFtoImage;
using SkiaSharp;

namespace Shiranuix.Services.Pdf;

/// <summary>
/// A paper read by PDFium: the file is held once, its pages are counted, and
/// a page is drawn only when the reader turns to it.
/// </summary>
/// <remarks>
/// <para>
/// A paper is not the writing: nothing here reaches the document or the
/// window engine. The reader holds this and asks for pages as the window
/// stands on them.
/// </para>
/// <para>
/// PDFium is not thread-safe; PDFtoImage serializes the calls it makes within
/// each process, so no lock is carried here in addition.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class PdfPages : IPdfPages {
    private readonly byte[] _paper;

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
}
