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
/// It only speaks for a runtime the pinned release published an archive
/// for, because a release's URL points at one machine's archive and a
/// person on another machine would be sent the wrong bytes: a runtime the
/// pin holds no archive for is refused rather than answered with somebody
/// else's file.
/// </remarks>
public sealed class KnownReleaseCatalog : IReleaseCatalog {
    // The number the repository's own train cut last, and the six archives
    // it published that number under — spelled the way the train spells its
    // assets, so every URL assembled below is one that really resolves.
    private const string Pinned = "0.6.6";

    private static readonly HashSet<string> Archives = [
        "win-x64",
        "win-arm64",
        "linux-x64",
        "linux-arm64",
        "osx-x64",
        "osx-arm64"
    ];

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
            ("shiranuix", var identifier) when Archives.Contains(identifier) =>
                Task.FromResult(PinnedArchive(appId, identifier)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(appId), appId,
                "The catalog keeps no entry for this application on this runtime yet.")
        };

    /// <summary>
    /// One archive of the pin, said in the train's own naming: the program's
    /// name over the file, the number over the tag's door, the machine's
    /// own identifier between them, and the family's own ending after —
    /// a URL spelled with the wrong one resolves to nothing at all.
    /// </summary>
    private static ReleaseAsset PinnedArchive(string appId, string identifier) {
        var extension = ExtensionOf(identifier);
        var name = $"{Runtime.NameOf(appId)}-{Pinned}-{identifier}{extension}";

        return new ReleaseAsset(
            appId,
            Pinned,
            new Uri($"https://github.com/Reim-developer/Shiranuix/releases/download/v{Pinned}/{name}"),
            name);
    }

    /// <summary>
    /// The shape the pin's archive is packed in for this machine, spelled
    /// the way the train spells it: a zip where Windows keeps its files, a
    /// tar.gz everywhere else.
    /// </summary>
    private static string ExtensionOf(string identifier) =>
        identifier.StartsWith("win-", StringComparison.Ordinal) ? ".zip" : ".tar.gz";
}
