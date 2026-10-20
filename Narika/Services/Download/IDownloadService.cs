namespace Narika.Services.Download;

/// <summary>
/// The archive an application's release arrives as, once it has landed.
/// </summary>
/// <param name="ArchivePath">Where the downloaded archive was put.</param>
public sealed record DownloadOutcome(string ArchivePath);

/// <summary>
/// Fetching the bytes the installation will be made from — the launcher's
/// half of getting an application onto the machine, run before the setup
/// wizard is ever opened.
/// </summary>
/// <remarks>
/// Download and repair are the same work under two names: both fetch a
/// clean copy of the current release, one because nothing is there and one
/// because what is there has gone wrong. Where the bytes come from — the
/// release pages the repository's own train publishes to — is this
/// implementation's business behind <see cref="Install.IReleaseCatalog"/>,
/// never the view model's, which is what lets the launcher's flow be tested
/// against a double that answers instantly.
/// </remarks>
public interface IDownloadService {
    /// <summary>
    /// Where the fetch that landed most recently put its bytes, or null
    /// while nothing has landed in this run. The install that follows a
    /// fetch asks here for the copy rather than pulling the same archive
    /// down a second time: one errand, two halves, one set of bytes.
    /// </summary>
    DownloadOutcome? Landed { get; }

    /// <summary>
    /// Fetches the current release's archive, reporting as it goes.
    /// </summary>
    /// <param name="progress">Told how far along the fetch is.</param>
    /// <param name="cancellation">
    /// Asked of the service throughout: a cancelled token stops the fetch
    /// and throws.
    /// </param>
    /// <returns>Where the archive was put.</returns>
    Task<DownloadOutcome> DownloadAsync(
        IProgress<Install.InstallProgress> progress,
        CancellationToken cancellation);
}
