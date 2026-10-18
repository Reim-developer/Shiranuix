using System.IO;
using Shiranuix.Services.Pdf;
using SkiaSharp;

namespace Shiranuix.ViewModels;

/// <summary>
/// The paper being read: its pages, the shape each page stands in, and the
/// pages held drawn for the window to stand on.
/// </summary>
/// <remarks>
/// <para>
/// The reader is a view over a paper, not the writing. It holds no document;
/// the gallery shows the paper's pages one after another in a scroll, and
/// the pages under the eye are the pages the state is asked for.
/// </para>
/// <para>
/// A drawing is dearer than a turn, so a page is drawn once and kept while
/// it is within reach: a few pages are held, the rest are let go when they
/// go past, and a page asked again while it still stands is handed the same
/// paper, drawn a second time only when the ask is wider. A page let go is
/// dropped as a reference and left to the collector, since the window may
/// still be handing one over.
/// </para>
/// <para>
/// The paper is handed in at birth, so this runs headlessly in tests over a
/// fake whose pages are already known.
/// </para>
/// </remarks>
public sealed class PdfViewerState {
    /// <summary>
    /// How many pages the reading keeps drawn while it stands; pages beyond
    /// these are let go and drawn again when they return.
    /// </summary>
    public const int Hold = 6;

    private readonly IPdfPages _paper;
    private readonly string _title;
    private readonly object _gate = new();
    private readonly Dictionary<(int Index, int Width), SKBitmap> _pages = new();
    private readonly List<(int Index, int Width)> _standing = new();

    /// <summary>
    /// Opens the reading over a paper already read.
    /// </summary>
    /// <param name="path">The file the paper was read from, for its name alone.</param>
    /// <param name="paper">The paper: pages counted, rendering on ask.</param>
    public PdfViewerState(string path, IPdfPages paper) {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _paper = paper ?? throw new ArgumentNullException(nameof(paper));
        _title = Path.GetFileName(path);
    }

    /// <summary>The file's own name, the way a window's title carries it.</summary>
    public string Title => _title;

    /// <summary>How many pages the paper carries.</summary>
    public int Count => _paper.Count;

    /// <summary>
    /// The shape one page stands in: its height over its width. From this and
    /// the pane's width, every place the gallery lays out is known before
    /// any drawing runs.
    /// </summary>
    /// <param name="index">The page's place, counting from zero.</param>
    /// <exception cref="ArgumentOutOfRangeException">The page does not exist.</exception>
    public double PageShape(int index) {
        var size = _paper.PageSize(index);
        return size.Height / size.Width;
    }

    /// <summary>
    /// Draws the page asked at the width asked, or hands back the same paper
    /// the reading still holds.
    /// </summary>
    /// <param name="index">The page's place, counting from zero.</param>
    /// <param name="width">The width to draw it at, in pixels.</param>
    /// <returns>The page as drawn or held, or null when the paper holds no such page.</returns>
    public SKBitmap? Draw(int index, int width) {
        if (index < 0 || index >= Count) {
            return null;
        }

        lock (_gate) {
            var key = (Index: index, Width: width);
            if (_pages.TryGetValue(key, out var held)) {
                // A page still within reach is stood at the reach's end: the
                // paper the reading holds least goes first when room is asked.
                _standing.Remove(key);
                _standing.Add(key);
                return held;
            }

            // PDFium is serialized within the process by its own bridge; the
            // gate only orders the held pages' book.
            var page = _paper.Draw(index, width);
            _pages[key] = page;
            _standing.Add(key);

            while (_standing.Count > Hold) {
                var leaving = _standing[0];
                _standing.RemoveAt(0);
                _pages.Remove(leaving);
            }

            return page;
        }
    }
}
