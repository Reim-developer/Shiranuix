using System.IO;
using Shiranuix.Services.Pdf;
using SkiaSharp;

namespace Shiranuix.ViewModels;

/// <summary>
/// The paper being read: its pages, the shape each page stands in, and the
/// pages held as pictures for the window to stand on.
/// </summary>
/// <remarks>
/// <para>
/// The reader is a view over a paper, not the writing. It holds no document;
/// the reading stands on one page at a time — a page the window shows whole,
/// never cut — and steps on or back through the paper one page at a time.
/// </para>
/// <para>
/// A drawing is dearer than a turn, so a page is drawn once and kept while
/// it is within reach: a few pages are held, the rest are let go when they
/// go past, and a page asked again while it still stands is handed the same
/// picture, drawn a second time only when the ask is wider. What the reading
/// holds is a picture in bytes — handed out once and never drawn on again —
/// so nobody who borrows a page can borrow it dead; the page's own drawing
/// lives only here, and is let go as soon as its picture is made.
/// </para>
/// <para>
/// The paper is handed in at birth, so this runs headlessly in tests over a
/// fake whose pages are already known.
/// </para>
/// </remarks>
public sealed class PdfViewerState {
    /// <summary>
    /// How many pages the reading keeps as pictures while it stands; pages
    /// beyond these are let go and drawn again when they return.
    /// </summary>
    public const int Hold = 6;

    private readonly IPdfPages _paper;
    private readonly string _title;
    private readonly object _gate = new();
    private readonly Dictionary<(int Index, int Width), byte[]> _pages = new();
    private readonly List<(int Index, int Width)> _held = new();
    private int _page;

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

    /// <summary>The page the reading stands on, counting from zero.</summary>
    public int Standing => _page;

    /// <summary>
    /// Walks the reading some pages on or back — never off the paper: a
    /// step past the last page stands on the last, a step before the first
    /// stands on the first.
    /// </summary>
    /// <param name="by">How many pages to walk; back when negative.</param>
    public void Step(int by) {
        var stood = _page + by;
        _page = Math.Clamp(stood, 0, Count - 1);
    }

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
    /// Draws the page asked at the width asked as a picture in bytes, or
    /// hands back the same picture the reading still holds.
    /// </summary>
    /// <param name="index">The page's place, counting from zero.</param>
    /// <param name="width">The width to draw it at, in pixels.</param>
    /// <returns>The page as a picture, or null when the paper holds no such page.</returns>
    /// <remarks>
    /// The drawing itself belongs to the reading: the page is borrowed only
    /// long enough to be made a picture, and let go inside. The bytes handed
    /// back are never drawn on again, so a page held by two readers is a
    /// page held twice, not a page held dead.
    /// </remarks>
    public byte[]? Draw(int index, int width) {
        if (index < 0 || index >= Count) {
            return null;
        }

        lock (_gate) {
            var key = (Index: index, Width: width);
            if (_pages.TryGetValue(key, out var held)) {
                // A page still within reach is stood at the reach's end: the
                // picture the reading holds least goes first when room is asked.
                _held.Remove(key);
                _held.Add(key);
                return held;
            }

            var page = _paper.Draw(index, width);
            byte[] picture;
            using (page) {
                using var encoded = page.Encode(SKEncodedImageFormat.Png, 90);
                picture = encoded.ToArray();
            }

            _pages[key] = picture;
            _held.Add(key);

            while (_held.Count > Hold) {
                var leaving = _held[0];
                _held.RemoveAt(0);
                _pages.Remove(leaving);
            }

            return picture;
        }
    }
}
