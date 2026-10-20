namespace Narika.Services.Install;

/// <summary>
/// The catalog that says the repository's last release out loud, without
/// asking anyone.
/// </summary>
/// <remarks>
/// This is the floor under <see cref="GitHubReleaseCatalog"/>: the release
/// pages are the source of truth when they can be reached, and this is what
/// answers when they cannot — no network, a machine offline, an anonymous
/// rate the API has had enough of. The number is the one the repository's
/// own release train cut last, so the welcome line still prints something
/// true and the install service still has a URL that really resolves, at
/// the cost of speaking for one release behind the current one.
/// </remarks>
public sealed class KnownReleaseCatalog : IReleaseCatalog {
    /// <inheritdoc />
    public Task<ReleaseAsset> ResolveAsync(string appId, CancellationToken cancellation = default) =>
        appId switch {
            "shiranuix" => Task.FromResult(new ReleaseAsset(
                "shiranuix",
                "0.6.3",
                new Uri("https://github.com/Reim-developer/Shiranuix/releases/download/v0.6.3/Shiranuix-0.6.3-win-x64.zip"),
                "Shiranuix-0.6.3-win-x64.zip")),
            _ => throw new ArgumentOutOfRangeException(
                nameof(appId), appId, "The catalog keeps no entry for this application yet.")
        };
}
