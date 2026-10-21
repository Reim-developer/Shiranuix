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
/// A release may publish several archives — one machine's identifier per
/// arm and per system — and which one is this machine's is not this
/// class's guess: it is what <see cref="Runtime"/> says the assets spell
/// themselves with, so the walk from a release to its bytes stays one
/// record's business.
///
/// A cancellation is not a failure and never falls back: it is the person
/// having stopped asking, and it is thrown out as the platform's own so
/// every caller sees the same shape it would from any other await.
/// </remarks>
public sealed class GitHubReleaseCatalog : IReleaseCatalog {
    private readonly HttpClient _http;
    private readonly IReleaseCatalog _fallback;
    private readonly Runtime _runtime;

    /// <summary>
    /// Creates the catalog over the client it asks, the catalog it falls
    /// back to when the pages cannot be reached, and the machine whose
    /// archive it is here for.
    /// </summary>
    /// <param name="http">The client every request is sent through.</param>
    /// <param name="fallback">What to answer when the pages cannot say.</param>
    /// <param name="runtime">The machine whose asset is wanted.</param>
    public GitHubReleaseCatalog(
        HttpClient http,
        IReleaseCatalog fallback,
        Runtime runtime) {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
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
    /// is spelled with, and the archive this machine came for among the
    /// assets — which is the one file a person on it actually wants.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The pages answered with a release that carries no archive for this
    /// runtime — which the caller above takes the same way as no answer
    /// at all.
    /// </exception>
    private ReleaseAsset Parse(string appId, string json) {
        using var page = JsonDocument.Parse(json);

        var tag = page.RootElement.GetProperty("tag_name").GetString() ?? string.Empty;
        var version = tag.StartsWith('v') ? tag[1..] : tag;
        var stem = Runtime.NameOf(appId) + "-";
        var suffix = $"-{_runtime.Identifier}{_runtime.ArchiveExtension}";

        foreach (var asset in page.RootElement.GetProperty("assets").EnumerateArray()) {
            var name = asset.GetProperty("name").GetString();

            if (name is not null
                && name.StartsWith(stem, StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) {
                var url = asset.GetProperty("browser_download_url").GetString();

                if (url is not null && Uri.TryCreate(url, UriKind.Absolute, out var uri)) {
                    return new ReleaseAsset(appId, version, uri, name);
                }
            }
        }

        throw new InvalidOperationException(
            $"The release for {appId} published no {_runtime.Identifier} archive.");
    }
}
