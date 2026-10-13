namespace Shiranuix.Services;

using Avalonia.Media.Imaging;

/// <summary>
/// Loads image files into Avalonia <see cref="Bitmap"/>s, off the UI thread.
/// </summary>
/// <remarks>
/// Reading manga pages means decoding multi-megabyte images; doing that on
/// the UI thread would freeze the app. This interface lets a view model
/// <c>await</c> a page and get it back as a task, with threading handled by
/// the implementation rather than the caller.
/// </remarks>
public interface IImageLoader
{
    /// <summary>
    /// Loads the image at the given path.
    /// </summary>
    /// <param name="path">Full path of the image file.</param>
    /// <param name="cancellationToken">Token to cancel the decode.</param>
    /// <returns>
    /// The decoded bitmap, or <c>null</c> if the file is missing or unreadable.
    /// </returns>
    Task<Bitmap?> LoadAsync(string path, CancellationToken cancellationToken = default);
}
