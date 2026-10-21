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
///
/// It only speaks for the runtime it was built over, because a release's
/// URL points at one machine's archive and a person on another machine
/// would be sent the wrong bytes: a runtime this catalog has no number for
/// is refused rather than answered with somebody else's file.
/// </remarks>
public sealed class KnownReleaseCatalog : IReleaseCatalog {
    private readonly Runtime _runtime;

    /// <summary>
    /// Creates the catalog over the machine whose pinned release it knows.
    /// </summary>
    /// <param name="runtime">The machine answers are wanted for.</param>
    public KnownReleaseCatalog(Runtime runtime) =>
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

    /// <inheritdoc />
    public Task<ReleaseAsset> ResolveAsync(string appId, CancellationToken cancellation = default) =>
        (appId, _runtime.Identifier) switch {
            ("shiranuix", "win-x64") => Task.FromResult(new ReleaseAsset(
                "shiranuix",
                "0.6.4",
                new Uri("https://github.com/Reim-developer/Shiranuix/releases/download/v0.6.4/Shiranuix-0.6.4-win-x64.zip"),
                "Shiranuix-0.6.4-win-x64.zip")),
            _ => throw new ArgumentOutOfRangeException(
                nameof(appId), appId,
                "The catalog keeps no entry for this application on this runtime yet.")
        };
}
