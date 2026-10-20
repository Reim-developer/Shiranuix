using Narika.Services.Install;

namespace Narika.Tests.Fakes;

/// <summary>
/// The release catalog's double: one fixed release, answered on the spot,
/// so the welcome line has a number without asking a network.
/// </summary>
public sealed class FakeReleaseCatalog : IReleaseCatalog {
    /// <summary>
    /// The release every question is answered with.
    /// </summary>
    public ReleaseAsset Release { get; set; } = new(
        "shiranuix",
        "9.9.9",
        new Uri("https://example.test/Shiranuix.zip"),
        "Shiranuix.zip");

    /// <inheritdoc />
    public Task<ReleaseAsset> ResolveAsync(string appId, CancellationToken cancellation = default) =>
        Task.FromResult(Release);
}
