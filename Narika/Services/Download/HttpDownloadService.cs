using Narika.Services.Install;

namespace Narika.Services.Download;

/// <summary>
/// The fetch, made real: the current release is resolved from where the
/// repository's own train published it, and its bytes are streamed into a
/// folder this application owns, the share of them landed counted as they
/// arrive.
/// </summary>
/// <remarks>
/// Bytes land under a name that says they are not finished yet, and only a
/// fetch that reached the far end is given the release's own file name — a
/// line dropped half way leaves a partial file behind rather than an archive
/// that looks complete and is not. The last fetch that did finish is
/// remembered as <see cref="Landed"/>, which is how the install that follows
/// this one asks for the bytes instead of downloading them again.
///
/// Nothing here times out on its own: the archive is tens of megabytes and
/// a slow line is not a failure. The person's Cancel — the token handed
/// into every await below — is what stops a fetch that has stopped moving.
/// </remarks>
public sealed class HttpDownloadService : IDownloadService {
    private const int BufferSize = 81920;

    private readonly IReleaseCatalog _releases;
    private readonly string _appId;
    private readonly HttpClient _http;
    private readonly string _folder;

    /// <summary>
    /// Creates the fetch over the catalog that says what to fetch, the
    /// application being fetched, and the client that reaches out.
    /// </summary>
    /// <param name="releases">Where the current release is resolved from.</param>
    /// <param name="appId">Which application's release is wanted.</param>
    /// <param name="http">The client every request is sent through.</param>
    /// <param name="folder">
    /// Where archives are put; the application's own downloads folder when
    /// the caller has no opinion, which is every caller but a test.
    /// </param>
    public HttpDownloadService(
        IReleaseCatalog releases,
        string appId,
        HttpClient http,
        string? folder = null) {
        _releases = releases ?? throw new ArgumentNullException(nameof(releases));
        _appId = appId ?? throw new ArgumentNullException(nameof(appId));
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _folder = folder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Narika",
            "downloads");
    }

    /// <inheritdoc />
    public DownloadOutcome? Landed { get; private set; }

    /// <inheritdoc />
    public async Task<DownloadOutcome> DownloadAsync(
        IProgress<InstallProgress> progress,
        CancellationToken cancellation) {
        ArgumentNullException.ThrowIfNull(progress);

        progress.Report(new InstallProgress(-1, "Contacting the release page..."));
        var release = await _releases
            .ResolveAsync(_appId, cancellation)
            .ConfigureAwait(false);

        Directory.CreateDirectory(_folder);
        var part = Path.Combine(_folder, release.FileName + ".part");
        var archive = Path.Combine(_folder, release.FileName);

        try {
            using var response = await _http
                .GetAsync(release.DownloadUri, HttpCompletionOption.ResponseHeadersRead, cancellation)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? -1L;
            await using var source = await response.Content
                .ReadAsStreamAsync(cancellation)
                .ConfigureAwait(false);
            await using var target = new FileStream(
                part,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: BufferSize,
                useAsync: true);

            var buffer = new byte[BufferSize];
            long read = 0;
            int taken;

            while ((taken = await source.ReadAsync(buffer, cancellation).ConfigureAwait(false)) > 0) {
                await target.WriteAsync(buffer.AsMemory(0, taken), cancellation).ConfigureAwait(false);
                read += taken;

                progress.Report(new InstallProgress(
                    total > 0 ? (int)(read * 100 / total) : -1,
                    Message(read, total)));
            }
        } catch {
            // The half-finished file is this fetch's own: it goes the way
            // the fetch did, and nothing later mistakes it for an archive.
            TryDelete(part);
            throw;
        }

        File.Move(part, archive, overwrite: true);
        Landed = new DownloadOutcome(archive);
        progress.Report(new InstallProgress(100, "The archive is ready."));
        return Landed;
    }

    /// <summary>
    /// What the bar says while the bytes arrive — the megabytes landed so
    /// far, and of how many when the server could say so.
    /// </summary>
    private static string Message(long read, long total) =>
        total > 0
            ? $"Downloading Shiranuix... {read / 1048576} of {total / 1048576} MB"
            : $"Downloading Shiranuix... {read / 1048576} MB";

    /// <summary>
    /// Takes a partial file back, if it can — a folder that refuses is not
    /// worth failing the fetch's report over.
    /// </summary>
    private static void TryDelete(string path) {
        try {
            if (File.Exists(path)) {
                File.Delete(path);
            }
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
        }
    }
}
