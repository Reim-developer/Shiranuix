namespace Shiranuix.Services;

using Shiranuix.Models;
using Shiranuix.Utilities;

/// <summary>
/// Default <see cref="IFolderScanner"/> that reads a plain directory of images.
/// </summary>
/// <remarks>
/// Pages are ordered using <see cref="NaturalSort"/> so names like
/// <c>1, 2, 10</c> come out in the order a human expects. Non-image files and
/// sub-directories are ignored.
/// </remarks>
public sealed class DirectoryFolderScanner : IFolderScanner
{
    /// <inheritdoc />
    public Chapter? Scan(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return null;
        }

        string[] images;
        try
        {
            images = Directory
                .EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
                .Where(ImageFormats.IsSupported)
                .OrderBy(p => Path.GetFileName(p) ?? string.Empty, NaturalSort.Comparer())
                .ToArray();
        }
        catch (IOException)
        {
            // Unreadable directory: treat as "no chapter".
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        if (images.Length == 0)
        {
            return null;
        }

        var pages = new List<MangaPage>(images.Length);
        for (int i = 0; i < images.Length; i++)
        {
            // 1-based page numbers.
            pages.Add(new MangaPage(images[i], i + 1));
        }

        string title = Path.GetFileName(Path.TrimEndingDirectorySeparator(folder));
        if (string.IsNullOrEmpty(title))
        {
            // e.g. a drive root like "C:\" has no meaningful file name.
            title = folder;
        }

        return new Chapter(folder, title, pages);
    }
}
