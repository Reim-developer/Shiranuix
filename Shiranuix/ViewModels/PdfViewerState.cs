using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Shiranuix.Services.Pdf;
using SkiaSharp;

namespace Shiranuix.ViewModels;

/// <summary>
/// The paper being read: the pages its file carries, where the reading
/// stands, and a page at a size, drawn for the window.
/// </summary>
/// <remarks>
/// <para>
/// The reader is a view over a paper, not the writing. It holds no document,
/// turns a page pointer clamped to the paper's own ends, and hands the window
/// each page as the window stands on it.
/// </para>
/// <para>
/// The paper is handed in at birth, so this runs headlessly in tests over a
/// fake whose pages are already known; no bitmap type reaches a test.
/// </para>
/// </remarks>
public sealed partial class PdfViewerState : ObservableObject {
    private readonly IPdfPages _paper;
    private readonly string _title;

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

    /// <summary>Where the reading stands: a page, counting from zero.</summary>
    /// <remarks>
    /// The pointer alone moves; a turn announces the mark the view binds,
    /// since the mark is what a person is shown.
    /// </remarks>
    [ObservableProperty]
    private int _index;

    /// <summary>Where the reading stands, the way a person says it.</summary>
    public string PageMark => Count is 0 ? "0 / 0" : $"{Index + 1} / {Count}";

    /// <summary>Turns forward, or stays on the last page.</summary>
    public void Next() {
        var next = Math.Min(Index + 1, Count - 1);
        if (next >= 0) {
            Index = next;
        }
    }

    /// <summary>Turns back, or stays on the first page.</summary>
    public void Previous() => Index = Math.Max(Index - 1, 0);

    /// <summary>
    /// Draws the page asked at the width asked.
    /// </summary>
    /// <param name="index">The page's place, counting from zero.</param>
    /// <param name="width">The width to draw it at, in pixels.</param>
    /// <returns>The page as drawn, or null when the paper holds no such page.</returns>
    public SKBitmap? Draw(int index, int width) =>
        index < 0 || index >= Count ? null : _paper.Draw(index, width);

    partial void OnIndexChanged(int value) => OnPropertyChanged(nameof(PageMark));
}
