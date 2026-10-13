namespace Shiranuix.Models;

/// <summary>
/// An ordered sequence of pages read as one unit (typically one folder).
/// </summary>
public sealed class Chapter {
    private readonly IReadOnlyList<MangaPage> _pages;

    /// <summary>
    /// Creates a chapter from an already-ordered page list.
    /// </summary>
    /// <param name="folder">Folder the pages were read from.</param>
    /// <param name="title">Display title (usually the folder name).</param>
    /// <param name="pages">Pages in reading order; never empty.</param>
    public Chapter(string folder, string title, IReadOnlyList<MangaPage> pages) {
        Folder = folder;
        Title = title;
        _pages = pages ?? throw new ArgumentNullException(nameof(pages));
    }

    /// <summary>Folder the pages were read from.</summary>
    public string Folder { get; }

    /// <summary>Display title (usually the folder name).</summary>
    public string Title { get; }

    /// <summary>Pages in reading order.</summary>
    public IReadOnlyList<MangaPage> Pages => _pages;

    /// <summary>Total number of pages.</summary>
    public int PageCount => _pages.Count;

    /// <summary>
    /// Looks up a page by its 1-based index.
    /// </summary>
    /// <param name="pageNumber">1-based page index.</param>
    /// <returns>The page, or <c>null</c> if the index is out of range.</returns>
    public MangaPage? FindPage(int pageNumber) {
        if (pageNumber < 1 || pageNumber > _pages.Count) {
            return null;
        }

        return _pages[pageNumber - 1];
    }
}
