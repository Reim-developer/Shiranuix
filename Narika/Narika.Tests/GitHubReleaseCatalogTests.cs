using System.Net;
using System.Text;
using Narika.Services.Install;
using Narika.Tests.Fakes;

namespace Narika.Tests;

/// <summary>
/// Tests for <see cref="GitHubReleaseCatalog"/>: what the launcher says the
/// current release is, asked of pages that answer whatever the test says —
/// never of the internet, and never of a version anybody had to guess.
/// </summary>
public sealed class GitHubReleaseCatalogTests {
    /// <summary>
    /// The machine the tests stand on: the one the repository's train
    /// publishes an archive for, so the asset below is the one the walk
    /// comes out holding.
    /// </summary>
    private static readonly Runtime Windows = new("win-x64", ".zip", "Shiranuix.exe");

    /// <summary>
    /// One release as the pages publish it: the tag the version is spelled
    /// with, this machine's archive among the assets, and a file that is
    /// not one — which is how the archive is told apart from the rest.
    /// </summary>
    private const string Page = """
        {
          "tag_name": "v0.6.3",
          "assets": [
            {
              "name": "Shiranuix-0.6.3-win-x64.zip",
              "browser_download_url": "https://example.test/Shiranuix-0.6.3-win-x64.zip"
            },
            {
              "name": "notes.txt",
              "browser_download_url": "https://example.test/notes.txt"
            }
          ]
        }
        """;

    [Fact]
    public async Task The_Pages_Say_The_Version_And_This_Machines_Archive() {
        var catalog = Catalog(() => Answer(Page));

        var release = await catalog.ResolveAsync(
            "shiranuix", TestContext.Current.CancellationToken);

        Assert.Equal("0.6.3", release.Version);
        Assert.Equal("Shiranuix-0.6.3-win-x64.zip", release.FileName);
        Assert.Equal(
            "https://example.test/Shiranuix-0.6.3-win-x64.zip",
            release.DownloadUri.ToString());
    }

    [Fact]
    public async Task A_Release_With_No_Archive_For_This_Machine_Is_No_Answer_At_All() {
        // The pages answered, and what they answered with was nothing this
        // machine could install — which is the same as not answering.
        var catalog = Catalog(() => Answer("""
            {
              "tag_name": "v0.6.3",
              "assets": [
                {
                  "name": "notes.txt",
                  "browser_download_url": "https://example.test/notes.txt"
                }
              ]
            }
            """));

        var release = await catalog.ResolveAsync(
            "shiranuix", TestContext.Current.CancellationToken);

        Assert.Equal("9.9.9", release.Version);
    }

    [Fact]
    public async Task A_Release_Whose_Archives_Belong_To_Other_Machines_Falls_Back() {
        // Every archive on the page is somebody else's — this walk is on a
        // machine the train did not publish for, and taking one of theirs
        // would be the wrong bytes in the right folder.
        var catalog = Catalog(
            () => Answer(Page),
            new Runtime("linux-x64", ".tar.gz", "Shiranuix"));

        var release = await catalog.ResolveAsync(
            "shiranuix", TestContext.Current.CancellationToken);

        Assert.Equal("9.9.9", release.Version);
    }

    [Fact]
    public async Task A_Place_That_Cannot_Answer_Falls_Back_To_The_Pinned_Release() {
        // The double's 9.9.9 is what the fallback answers with, so that
        // number being here at all is the proof the pages were not.
        var catalog = Catalog(
            () => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var release = await catalog.ResolveAsync(
            "shiranuix", TestContext.Current.CancellationToken);

        Assert.Equal("9.9.9", release.Version);
    }

    [Fact]
    public async Task A_Cancelled_Question_Is_Thrown_Out_Rather_Than_Fallen_Back_On() {
        // A cancellation is the person having stopped asking, not the pages
        // having failed to answer — falling back would be answering a
        // question nobody is waiting for.
        var catalog = new GitHubReleaseCatalog(
            new HttpClient(new StubHttpHandler(async (_, cancellation) => {
                await Task.Delay(Timeout.Infinite, cancellation);
                return new HttpResponseMessage(HttpStatusCode.OK);
            })),
            new FakeReleaseCatalog(),
            Windows);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => catalog.ResolveAsync("shiranuix", cancellation.Token));
    }

    /// <summary>
    /// The catalog over one answer from the pages and one machine — the
    /// Windows one the train publishes for, unless a test says otherwise —
    /// with the double standing in as the fallback: its 9.9.9 marks every
    /// place the fallback spoke.
    /// </summary>
    private static GitHubReleaseCatalog Catalog(
        Func<HttpResponseMessage> respond,
        Runtime? runtime = null) =>
        new(new HttpClient(new StubHttpHandler(
            (_, _) => Task.FromResult(respond()))),
            new FakeReleaseCatalog(),
            runtime ?? Windows);

    /// <summary>
    /// One page of the release, as the pages publish them.
    /// </summary>
    private static HttpResponseMessage Answer(string json) =>
        new(HttpStatusCode.OK) {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
}
