namespace Narika.Services.Install;

/// <summary>
/// The catalog that says today's release out loud, without asking anyone.
/// </summary>
/// <remarks>
/// The real one will ask the repository's release pages for the latest of
/// every app the monorepo keeps and answer from what they say; this one
/// hard-codes the number the repository's own release train cut last, so
/// the welcome line has something true to print and the install service
/// has a URL that really resolves while the plumbing around them is what
/// is being built. Both move to the network in the task that makes the
/// download real.
/// </remarks>
public sealed class KnownReleaseCatalog : IReleaseCatalog {
    /// <inheritdoc />
    public Task<ReleaseAsset> ResolveAsync(string appId, CancellationToken cancellation = default) =>
        appId switch {
            "shiranuix" => Task.FromResult(new ReleaseAsset(
                "shiranuix",
                "0.6.0",
                new Uri("https://github.com/Reim-developer/Shiranuix/releases/download/v0.6.0/Shiranuix-0.6.0-win-x64.zip"),
                "Shiranuix-0.6.0-win-x64.zip")),
            _ => throw new ArgumentOutOfRangeException(
                nameof(appId), appId, "The catalog keeps no entry for this application yet.")
        };
}
