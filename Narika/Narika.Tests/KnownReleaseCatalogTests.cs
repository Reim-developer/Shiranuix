using Narika.Services.Install;

namespace Narika.Tests;

/// <summary>
/// Tests for <see cref="KnownReleaseCatalog"/>: the floor under the pages —
/// one pinned release per runtime it was built over, and a refusal for any
/// runtime or application it knows nothing of.
/// </summary>
public sealed class KnownReleaseCatalogTests {
    [Fact]
    public async Task The_Pinned_Release_Is_Said_For_The_Runtime_It_Was_Built_Over() {
        var catalog = new KnownReleaseCatalog(new Runtime("win-x64", ".zip", "Shiranuix.exe"));

        var release = await catalog.ResolveAsync("shiranuix");

        Assert.Equal("0.6.4", release.Version);
        Assert.Equal("Shiranuix-0.6.4-win-x64.zip", release.FileName);
    }

    [Fact]
    public async Task A_Runtime_It_Knows_Nothing_Of_Is_Refused_Rather_Than_Asked() {
        // The pinned URL points at one machine's archive; handing it to a
        // person on another would be the wrong bytes in the right folder,
        // so the honest answer is no answer at all.
        var catalog = new KnownReleaseCatalog(new Runtime("linux-x64", ".tar.gz", "Shiranuix"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => catalog.ResolveAsync("shiranuix"));
    }

    [Fact]
    public async Task An_Application_It_Has_No_Number_For_Is_Refused() {
        var catalog = new KnownReleaseCatalog(new Runtime("win-x64", ".zip", "Shiranuix.exe"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => catalog.ResolveAsync("somebody-elses-app"));
    }
}
