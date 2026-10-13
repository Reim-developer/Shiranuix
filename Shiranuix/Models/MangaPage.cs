namespace Shiranuix.Models;

/// <summary>
/// A single page of a chapter: an image file plus its 1-based position.
/// </summary>
/// <remarks>
/// Immutable — the page order is fixed when the folder is scanned. The
/// path is stored as a plain string so the model stays free of any I/O
/// concern and is trivially testable.
/// </remarks>
public sealed record MangaPage
{
    /// <summary>
    /// Creates a page.
    /// </summary>
    /// <param name="imageFile">Full path of the backing image file.</param>
    /// <param name="pageNumber">1-based index of this page within its chapter.</param>
    public MangaPage(string imageFile, int pageNumber)
    {
        ImageFile = imageFile;
        PageNumber = pageNumber;
    }

    /// <summary>Full path of the backing image file.</summary>
    public string ImageFile { get; }

    /// <summary>1-based index of this page within its chapter.</summary>
    public int PageNumber { get; }
}
