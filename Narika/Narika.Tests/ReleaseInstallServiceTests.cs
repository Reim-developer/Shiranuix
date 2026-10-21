using System.IO.Compression;
using Narika.Services.Download;
using Narika.Services.Install;
using Narika.Tests.Fakes;

namespace Narika.Tests;

/// <summary>
/// Tests for <see cref="ReleaseInstallService"/>: the install that unpacks
/// the fetch's own bytes into a folder and leaves the marks — everything
/// watched inside a folder of this test's own, against doubles that write
/// nothing to any real surface.
/// </summary>
public sealed class ReleaseInstallServiceTests : IDisposable {
    /// <summary>
    /// The machine the release train publishes for, and the one every walk
    /// here except the two seam tests is standing on.
    /// </summary>
    private static readonly Runtime Windows = new("win-x64", ".zip", "Shiranuix.exe");

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "narika-tests", Guid.NewGuid().ToString("N"));

    public ReleaseInstallServiceTests() => Directory.CreateDirectory(_folder);

    /// <summary>
    /// The folder this test installs in and out of, taken back whole.
    /// </summary>
    public void Dispose() {
        try {
            if (Directory.Exists(_folder)) {
                Directory.Delete(_folder, recursive: true);
            }
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
        }
    }

    [Fact]
    public async Task A_Copy_That_Has_Landed_Is_Unpacked_Without_Fetching_Again() {
        var archive = Archive();
        var download = new FakeDownloadService { Landed = new DownloadOutcome(archive) };
        var shortcuts = new FakeShortcutService();
        var service = new ReleaseInstallService(download, shortcuts, Windows);
        var notes = new List<InstallProgress>();

        var outcome = await service.InstallAsync(
            new InstallOptions { Destination = Path.Combine(_folder, "installed") },
            new InlineProgress<InstallProgress>(notes.Add),
            CancellationToken.None);

        // The launcher's fetch and this walk are one errand: the copy is
        // reused rather than the same bytes pulled down a second time.
        Assert.Equal(0, download.Calls);
        Assert.False(download.Started.Task.IsCompleted);

        Assert.Equal(
            Path.Combine(_folder, "installed", "Shiranuix.exe"),
            outcome.ExecutablePath);
        Assert.Equal("a program, of sorts", File.ReadAllText(outcome.ExecutablePath));

        // Every mark the options page offers by default, and all of them
        // pointed at the program that was just unpacked.
        Assert.Equal(
            new[] { "desktop", "start menu", "taskbar" },
            shortcuts.Marks);
        Assert.Equal(outcome.ExecutablePath, shortcuts.Target);

        // The archive's bytes are the program now, so the archive is not
        // held twice on the disk.
        Assert.False(File.Exists(archive));

        Assert.Equal(100, notes[^1].Percent);
    }

    [Fact]
    public async Task Nothing_Landed_Walks_The_Fetch_Itself() {
        var archive = Archive();
        var download = new FakeDownloadService { Outcome = new DownloadOutcome(archive) };
        var service = new ReleaseInstallService(download, new FakeShortcutService(), Windows);

        var outcome = await service.InstallAsync(
            new InstallOptions { Destination = Path.Combine(_folder, "installed") },
            new InlineProgress<InstallProgress>(_ => { }),
            CancellationToken.None);

        Assert.Equal(1, download.Calls);
        Assert.True(File.Exists(outcome.ExecutablePath));
    }

    [Fact]
    public async Task Marks_That_Were_Not_Asked_For_Are_Not_Left() {
        var archive = Archive();
        var shortcuts = new FakeShortcutService();
        var service = new ReleaseInstallService(
            new FakeDownloadService { Landed = new DownloadOutcome(archive) },
            shortcuts,
            Windows);

        await service.InstallAsync(
            new InstallOptions {
                Destination = Path.Combine(_folder, "installed"),
                CreateStartMenuShortcut = false,
                CreateTaskbarShortcut = false,
            },
            new InlineProgress<InstallProgress>(_ => { }),
            CancellationToken.None);

        Assert.Equal(new[] { "desktop" }, shortcuts.Marks);
    }

    [Fact]
    public async Task An_Archive_With_No_Program_In_It_Fails_Loudly() {
        var archive = Archive("readme.txt");
        var service = new ReleaseInstallService(
            new FakeDownloadService { Landed = new DownloadOutcome(archive) },
            new FakeShortcutService(),
            Windows);

        // A failure before anything is on disk is kept as it was found —
        // the copy stays put, so the next attempt costs no fetch.
        await Assert.ThrowsAsync<InvalidDataException>(() => service.InstallAsync(
            new InstallOptions { Destination = Path.Combine(_folder, "installed") },
            new InlineProgress<InstallProgress>(_ => { }),
            CancellationToken.None));

        Assert.True(File.Exists(archive));
    }

    [Fact]
    public async Task An_Archive_That_Reaches_Outside_Is_Refused() {
        var archive = Path.Combine(_folder, "Shiranuix.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create)) {
            using var entry = zip.CreateEntry("../Shiranuix.exe").Open();
            entry.Write("a program, of sorts"u8.ToArray());
        }

        var service = new ReleaseInstallService(
            new FakeDownloadService { Landed = new DownloadOutcome(archive) },
            new FakeShortcutService(),
            Windows);

        // Somebody else's bytes do not get to decide where on this machine
        // they end up, whatever the archive inside them says.
        await Assert.ThrowsAsync<InvalidDataException>(() => service.InstallAsync(
            new InstallOptions { Destination = Path.Combine(_folder, "installed") },
            new InlineProgress<InstallProgress>(_ => { }),
            CancellationToken.None));
    }

    [Fact]
    public async Task The_Program_Is_Found_By_The_Name_This_Machine_Goes_By() {
        // A machine the train publishes no executable-ending for still has
        // a name for its program, and the walk answers to that name rather
        // than to a Windows one it was never told.
        var archive = Archive("Shiranuix");
        var service = new ReleaseInstallService(
            new FakeDownloadService { Landed = new DownloadOutcome(archive) },
            new FakeShortcutService(),
            new Runtime("linux-x64", ".zip", "Shiranuix"));

        var outcome = await service.InstallAsync(
            new InstallOptions { Destination = Path.Combine(_folder, "installed") },
            new InlineProgress<InstallProgress>(_ => { }),
            CancellationToken.None);

        Assert.Equal(
            Path.Combine(_folder, "installed", "Shiranuix"),
            outcome.ExecutablePath);
    }

    [Fact]
    public async Task An_Archive_In_A_Shape_This_Installer_Does_Not_Open_Is_Refused() {
        // The day the train publishes anything but a zip is the day this
        // walk is taught that shape — until then, an archive in another
        // one is refused at the door rather than misread at the stream.
        var archive = Archive();
        var service = new ReleaseInstallService(
            new FakeDownloadService { Landed = new DownloadOutcome(archive) },
            new FakeShortcutService(),
            new Runtime("win-x64", ".tar.gz", "Shiranuix.exe"));

        await Assert.ThrowsAsync<InvalidDataException>(() => service.InstallAsync(
            new InstallOptions { Destination = Path.Combine(_folder, "installed") },
            new InlineProgress<InstallProgress>(_ => { }),
            CancellationToken.None));
    }

    /// <summary>
    /// An archive with one file at the root, the way the release train
    /// publishes one: the program itself.
    /// </summary>
    private string Archive(string name = "Shiranuix.exe") {
        var path = Path.Combine(_folder, "Shiranuix.zip");

        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        using var entry = zip.CreateEntry(name).Open();
        entry.Write("a program, of sorts"u8.ToArray());

        return path;
    }
}
