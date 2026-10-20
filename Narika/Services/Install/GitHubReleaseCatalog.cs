using System.Text.Json;

namespace Narika.Services.Install;

/// <summary>
/// The catalog that asks the repository's release pages what is current,
/// and answers from what they say.
/// </summary>
/// <remarks>
/// The pages are the source of truth — the number on the card and the URL
/// the bytes come from are both whatever the latest release published — and
/// this class reaches them anonymously, which means a machine offline, a
/// network that refuses, or an anonymous rate the API has had enough of are
/// all the same answer here: fall back to <see cref="KnownReleaseCatalog"/>,
/// the release the repository's own train cut last. A launcher that cannot
/// ask the internet still opens, still shows a true version, and still has a
/// URL that resolves; it simply speaks for the last release rather than the
/// current one.
///
/// A cancellation is not a failure and never falls back: it is the person
/// having stopped asking, and it is thrown out as the platform's own so
/// every caller sees the same shape it would from any other await.
/// </remarks>
public sealed class GitHubReleaseCatalog : IReleaseCatalog {
    private readonly HttpClient _http;
    private readonly IReleaseCatalog _fallback;

    /// <summary>
    /// Creates the catalog over the client it asks and the catalog it falls
    /// back to when the pages cannot be reached.
    /// </summary>
    /// <param name="http">The client every request is sent through.</param>
    /// <param name="fallback">What to answer when the pages cannot say.</param>
    public GitHubReleaseCatalog(HttpClient http, IReleaseCatalog fallback) {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
    }

    /// <inheritdoc />
    public async Task<ReleaseAsset> ResolveAsync(
        string appId,
        CancellationToken cancellation = default) {
        try {
            using var response = await _http
                .GetAsync(LatestFor(appId), HttpCompletionOption.ResponseContentRead, cancellation)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            return Parse(
                appId,
                await response.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false));
        } catch (Exception e) when (
            e is not OperationCanceledException || !cancellation.IsCancellationRequested) {
            // Everything the internet can be, and none of it is fatal:
            // the pinned release knows the same answers for one version.
            return await _fallback.ResolveAsync(appId, cancellation).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Where the latest release of one application is published — the
    /// repository's own pages, asked the way they require: by name.
    /// </summary>
    private static string LatestFor(string appId) {
        var name = char.ToUpperInvariant(appId[0]) + appId[1..];
        return $"https://api.github.com/repos/Reim-developer/{name}/releases/latest";
    }

    /// <summary>
    /// Reads one release out of the pages' own answer: the tag the version
    /// is spelled with, and the Windows archive among the assets — which is
    /// the one file a person on this machine came for.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The pages answered with a release that carries no Windows archive —
    /// which the caller above takes the same way as no answer at all.
    /// </exception>
    private static ReleaseAsset Parse(string appId, string json) {
        using var page = JsonDocument.Parse(json);

        var tag = page.RootElement.GetProperty("tag_name").GetString() ?? string.Empty;
        var version = tag.StartsWith('v') ? tag[1..] : tag;
        var stem = char.ToUpperInvariant(appId[0]) + appId[1..] + "-";

        foreach (var asset in page.RootElement.GetProperty("assets").EnumerateArray()) {
            var name = asset.GetProperty("name").GetString();

            if (name is not null
                && name.StartsWith(stem, StringComparison.OrdinalIgnoreCase)
                && name.EndsWith("-win-x64.zip", StringComparison.OrdinalIgnoreCase)) {
                var url = asset.GetProperty("browser_download_url").GetString();

                if (url is not null && Uri.TryCreate(url, UriKind.Absolute, out var uri)) {
                    return new ReleaseAsset(appId, version, uri, name);
                }
            }
        }

        throw new InvalidOperationException(
            $"The release for {appId} published no Windows archive.");
    }
}
