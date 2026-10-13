namespace Shiranuix.Services;

using Avalonia.Media.Imaging;

/// <summary>
/// <see cref="IImageLoader"/> that decodes images on the thread pool.
/// </summary>
/// <remarks>
/// Decoding runs via <see cref="Task.Run{TResult}"/> so the UI thread is never
/// blocked. Avalonia's <see cref="Bitmap"/> is safe to construct off the UI
/// thread; only controls that display it must be touched on the UI thread.
/// </remarks>
public sealed class AsyncImageLoader : IImageLoader
{
    /// <inheritdoc />
    public async Task<Bitmap?> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path)
            || !File.Exists(path))
        {
            return null;
        }

        try
        {
            // Decode on a background thread; the caller awaits the result.
            return await Task.Run(() => Decode(path), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Propagate cancellation so callers can react to a superseded load.
            throw;
        }
        catch (Exception)
        {
            // Malformed or unreadable image: treat as a soft failure.
            return null;
        }
    }

    /// <summary>
    /// Reads and decodes a bitmap from disk.
    /// </summary>
    /// <param name="path">File to decode.</param>
    /// <returns>The decoded bitmap.</returns>
    private static Bitmap Decode(string path)
    {
        // Copy the stream into memory so the file handle isn't held open for
        // the bitmap's lifetime (manga pages are re-read / paged frequently).
        using var stream = File.OpenRead(path);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        buffer.Position = 0;
        return new Bitmap(buffer);
    }
}
