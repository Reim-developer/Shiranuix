using Avalonia.Media.Imaging;
using Shiranuix.Services;

namespace Shiranuix.Tests.Fakes;

/// <summary>
/// A stand-in <see cref="IImageLoader"/> that records every requested path and
/// never decodes a real image, keeping view-model tests fully headless.
/// </summary>
internal sealed class FakeImageLoader : IImageLoader
{
    /// <summary>Paths passed to <see cref="LoadAsync"/>, in call order.</summary>
    public List<string> LoadedPaths { get; } = new();

    /// <inheritdoc />
    public Task<Bitmap?> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        LoadedPaths.Add(path);
        // A completed task keeps navigation deterministic for the tests.
        return Task.FromResult<Bitmap?>(null);
    }
}
