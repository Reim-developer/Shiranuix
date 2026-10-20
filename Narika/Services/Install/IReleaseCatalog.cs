namespace Narika.Services.Install;

/// <summary>
/// Where an application's downloadable release is found, answered before
/// anything is installed.
/// </summary>
/// <remarks>
/// This is the seam the monorepo will grow through: one day the catalog
/// asks a release page for every app in the repository and answers with
/// whichever one was asked for. The wizard only ever wants the version to
/// put on the welcome line and, through the install service, the bytes
/// themselves — it never learns how either is reached.
/// </remarks>
public interface IReleaseCatalog {
    /// <summary>
    /// Resolves the current release of one application.
    /// </summary>
    /// <param name="appId">Which application is being asked for.</param>
    /// <param name="cancellation">Asks for the lookup to be abandoned.</param>
    /// <returns>The release to install from.</returns>
    Task<ReleaseAsset> ResolveAsync(string appId, CancellationToken cancellation = default);
}
