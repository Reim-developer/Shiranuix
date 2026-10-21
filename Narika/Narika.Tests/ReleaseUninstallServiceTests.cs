using Narika.Services.Install;
using Narika.Tests.Fakes;

namespace Narika.Tests;

/// <summary>
/// Tests for <see cref="ReleaseUninstallService"/>: the removal that takes
/// the marks back and the program's own folder with them — everything
/// watched inside a folder of this test's own, against doubles that touch
/// no real surface.
/// </summary>
public sealed class ReleaseUninstallServiceTests : IDisposable {
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "narika-tests", Guid.NewGuid().ToString("N"));

    public ReleaseUninstallServiceTests() => Directory.CreateDirectory(_folder);

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
    public async Task Everything_The_Install_Left_Is_Taken_Back() {
        var program = Plant();
        var shortcuts = new FakeShortcutService();
        var service = new ReleaseUninstallService(shortcuts);
        var notes = new List<InstallProgress>();

        await service.UninstallAsync(
            program,
            new InlineProgress<InstallProgress>(notes.Add),
            CancellationToken.None);

        // The program's own folder, and everything inside it, is gone.
        Assert.False(Directory.Exists(Path.GetDirectoryName(program)));

        // The marks come back off the same program the install left them
        // on, in the order every surface knows them by.
        Assert.Equal(new[] { "desktop", "start menu", "taskbar" }, shortcuts.Unmarked);
        Assert.Equal(program, shortcuts.UnmarkedTarget);

        Assert.Equal(100, notes[^1].Percent);
    }

    [WindowsFact]
    public async Task A_Folder_That_Is_Busy_Refuses_Rather_Than_Pretending() {
        var program = Plant();
        var service = new ReleaseUninstallService(new FakeShortcutService());
        var folder = Path.GetDirectoryName(program)!;

        // A file held open elsewhere is the whole reason a removal can
        // fail, and it must be said rather than swallowed — the status
        // line's account of it depends on this walk throwing. Only the
        // Windows disk holds a folder busy this way: an unlink carries an
        // open file away on Unix without a word, so this refusal is
        // Windows's own and the test stands there.
        using (new FileStream(program, FileMode.Open, FileAccess.Read)) {
            await Assert.ThrowsAsync<IOException>(() => service.UninstallAsync(
                program,
                new InlineProgress<InstallProgress>(_ => { }),
                CancellationToken.None));
        }
    }

    [Fact]
    public async Task A_Machine_With_Nothing_On_It_Is_Not_An_Error() {
        // Removing twice is the same as removing once: a folder that is
        // already gone is the answer the first removal was hoping for.
        var program = Path.Combine(_folder, "gone", "Shiranuix.exe");
        var service = new ReleaseUninstallService(new FakeShortcutService());

        await service.UninstallAsync(
            program,
            new InlineProgress<InstallProgress>(_ => { }),
            CancellationToken.None);

        Assert.False(Directory.Exists(Path.GetDirectoryName(program)));
    }

    [Fact]
    public async Task The_Bottom_Of_A_Drive_Is_Never_Taken_Back_Whole() {
        // Whatever a receipt somewhere claims, the one folder no removal
        // gets to take is the root of a disk — the walk refuses it the
        // same way the unpack refuses to cross a folder's line.
        var root = Path.GetPathRoot(Path.GetFullPath(_folder))!;
        var program = Path.Combine(root, "Shiranuix.exe");
        var service = new ReleaseUninstallService(new FakeShortcutService());

        await service.UninstallAsync(
            program,
            new InlineProgress<InstallProgress>(_ => { }),
            CancellationToken.None);

        Assert.True(Directory.Exists(root));
    }

    /// <summary>
    /// A planted installation: the program's own folder with the program
    /// in it and one file beside, the way an install leaves things.
    /// </summary>
    private string Plant() {
        var folder = Path.Combine(_folder, "Shiranuix");
        Directory.CreateDirectory(folder);

        var program = Path.Combine(folder, "Shiranuix.exe");
        File.WriteAllText(program, "a program, of sorts");
        File.WriteAllText(Path.Combine(folder, "data.txt"), "beside it");

        return program;
    }
}
