namespace Shiranuix.Utilities;

/// <summary>
/// Recognises image file extensions that the reader can display.
/// </summary>
/// <remarks>
/// Kept in one place so the folder scanner, the home screen, and a future
/// library indexer all agree on which files count as pages.
/// </remarks>
public static class ImageFormats
{
    /// <summary>
    /// Supported extensions, lowercase and without the leading dot.
    /// </summary>
    /// <remarks>
    /// <c>webp</c> is listed for forward compatibility; whether it can be
    /// decoded depends on the platform, so callers should rely on
    /// <see cref="IsSupported"/> rather than trusting this set for decoding
    /// guarantees.
    /// </remarks>
    public static readonly IReadOnlySet<string> SupportedExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "jpg", "jpeg", "png", "gif", "bmp", "webp"
        };

    /// <summary>
    /// Checks whether the given file looks like a supported image.
    /// </summary>
    /// <remarks>
    /// Only the extension is inspected; the file is not opened or decoded. A
    /// <c>.jpg</c> that is actually a text file would still return
    /// <c>true</c>.
    /// </remarks>
    /// <param name="path">File path to test.</param>
    /// <returns><c>true</c> if the extension is a supported image format.</returns>
    public static bool IsSupported(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string name = Path.GetFileName(path);
        int dot = name.LastIndexOf('.');
        if (dot < 0 || dot == name.Length - 1)
        {
            return false;
        }

        string ext = name[(dot + 1)..];
        return SupportedExtensions.Contains(ext);
    }
}
