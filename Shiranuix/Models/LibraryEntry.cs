namespace Shiranuix.Models;

/// <summary>
/// A remembered entry for a folder that was opened (the "recent" list).
/// </summary>
/// <remarks>
/// Immutable snapshot captured at the moment the folder was opened. The
/// in-memory repository de-duplicates by <see cref="Folder"/>; a future
/// SQLite-backed repository will persist the same shape.
/// </remarks>
public sealed record LibraryEntry
{
    /// <summary>
    /// Creates an entry.
    /// </summary>
    /// <param name="folder">Folder path being remembered.</param>
    /// <param name="title">Display title (usually the folder name).</param>
    /// <param name="pageCount">Number of pages seen when it was opened.</param>
    /// <param name="openedAt">Timestamp of the most recent open.</param>
    public LibraryEntry(string folder, string title, int pageCount, DateTimeOffset openedAt)
    {
        Folder = folder;
        Title = title;
        PageCount = pageCount;
        OpenedAt = openedAt;
    }

    /// <summary>Folder path being remembered.</summary>
    public string Folder { get; }

    /// <summary>Display title (usually the folder name).</summary>
    public string Title { get; }

    /// <summary>Number of pages seen when it was opened.</summary>
    public int PageCount { get; }

    /// <summary>Timestamp of the most recent open.</summary>
    public DateTimeOffset OpenedAt { get; }

    /// <summary>
    /// Captures an entry from a freshly scanned chapter.
    /// </summary>
    /// <param name="chapter">The chapter that was opened.</param>
    /// <param name="openedAt">When it was opened.</param>
    /// <returns>A new entry mirroring the chapter's folder, title and page count.</returns>
    public static LibraryEntry From(Chapter chapter, DateTimeOffset openedAt)
    {
        ArgumentNullException.ThrowIfNull(chapter);
        return new LibraryEntry(chapter.Folder, chapter.Title, chapter.PageCount, openedAt);
    }
}
