namespace Shiranuix.Services;

using Shiranuix.Models;

/// <summary>
/// Reads a folder of images into a <see cref="Chapter"/>.
/// </summary>
/// <remarks>
/// Implementations decide what counts as an image and in what order pages
/// appear. The reader depends only on this interface, so a future
/// implementation (e.g. reading a ZIP/CBZ archive, or a remote source) can be
/// swapped in without touching the UI.
/// </remarks>
public interface IFolderScanner {
    /// <summary>
    /// Scans the given folder and builds a chapter from its images.
    /// </summary>
    /// <param name="folder">Folder path to scan.</param>
    /// <returns>
    /// The chapter, or <c>null</c> if the folder contains no usable images
    /// or is not a readable directory.
    /// </returns>
    Chapter? Scan(string folder);
}
