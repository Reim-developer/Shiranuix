using Narika.Services.Install;

namespace Narika.Tests;

/// <summary>
/// Tests for <see cref="KnownReleaseCatalog"/>: the floor under the pages —
/// one pinned release for every archive the train published, and a refusal
/// for any runtime or application it knows nothing of.
/// </summary>
public sealed class KnownReleaseCatalogTests {
    [Theory]
    [InlineData("win-x64", ".zip")]
    [InlineData("win-arm64", ".zip")]
    [InlineData("linux-x64", ".tar.gz")]
    [InlineData("linux-arm64", ".tar.gz")]
    [InlineData("osx-x64", ".tar.gz")]
    [InlineData("osx-arm64", ".tar.gz")]
    public async Task The_Pinned_Release_Is_Said_For_Every_Archive_The_Train_Published(
        string identifier, string extension) {
        var catalog = new KnownReleaseCatalog(new Runtime(identifier, extension, "Shiranuix"));

        var release = await catalog.ResolveAsync(
            "shiranuix", TestContext.Current.CancellationToken);

        Assert.Equal("0.6.6", release.Version);
        Assert.Equal($"Shiranuix-0.6.6-{identifier}{extension}", release.FileName);
        Assert.Equal(
            $"https://github.com/Reim-developer/Shiranuix/releases/download/v0.6.6/Shiranuix-0.6.6-{identifier}{extension}",
            release.DownloadUri.AbsoluteUri);
    }

    [Fact]
    public async Task A_Runtime_It_Knows_Nothing_Of_Is_Refused_Rather_Than_Asked() {
        // The pin holds one archive per machine the train published for; a
        // machine outside those — musl's linux, which the train never cut —
        // would be handed the wrong bytes in the right folder, so the honest
        // answer is no answer at all.
        var catalog = new KnownReleaseCatalog(new Runtime("linux-musl-x64", ".tar.gz", "Shiranuix"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => catalog.ResolveAsync(
                "shiranuix", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task An_Application_It_Has_No_Number_For_Is_Refused() {
        var catalog = new KnownReleaseCatalog(new Runtime("win-x64", ".zip", "Shiranuix.exe"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => catalog.ResolveAsync(
                "somebody-elses-app", TestContext.Current.CancellationToken));
    }
}
