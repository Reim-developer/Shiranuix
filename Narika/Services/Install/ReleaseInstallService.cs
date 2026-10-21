using System.Formats.Tar;
using System.IO.Compression;
using Narika.Services.Download;

namespace Narika.Services.Install;

/// <summary>
/// The installation, made real: the bytes the launcher's fetch put down are
/// unpacked into the folder the person chose, the marks they asked for are
/// left on the machine — a desktop icon, a start menu entry, a taskbar pin —
/// and what comes out the far end is the program's own path, which is what
/// the wizard's last page starts and the launcher reads after.
/// </summary>
/// <remarks>
/// Where the bytes come from is <see cref="IDownloadService"/>'s business,
/// and the two halves of this walk ask it the same way: a copy that has
/// already landed in this run is the one unpacked, and no copy at all sends
/// this walk through the fetch itself — one errand, never two. A fetch that
/// has landed is taken back once its bytes are on disk, because the archive
/// beside the installation would be the whole program held twice; a walk
/// that stops early leaves its copy where it is, so the next attempt costs
/// no band at all.
///
/// What shape an archive is in and what the program goes by are the
/// machine's own business rather than this class's assumption — both
/// arrive as the <see cref="Runtime"/> the composition root built it
/// over, so nothing here spells a platform of its own behind the
/// catalog's back.
///
/// Nothing here asks for elevation. Every destination it will be handed —
/// the current user's own Programs folder by default — is one the user may
/// write to without a prompt, and putting an editor on a machine should not
/// feel like changing the machine.
/// </remarks>
public sealed class ReleaseInstallService : IInstallService {
    private readonly IDownloadService _download;
    private readonly IShortcutService _shortcuts;
    private readonly Runtime _runtime;

    /// <summary>
    /// Creates the service over the two halves of its work — the fetch that
    /// hands it bytes and the surfaces it may leave marks on — and the
    /// machine whose archive shape and program name the walk answers to.
    /// </summary>
    /// <param name="download">Where the bytes come from.</param>
    /// <param name="shortcuts">Where the marks may be left.</param>
    /// <param name="runtime">The machine being installed onto.</param>
    public ReleaseInstallService(
        IDownloadService download,
        IShortcutService shortcuts,
        Runtime runtime) {
        _download = download ?? throw new ArgumentNullException(nameof(download));
        _shortcuts = shortcuts ?? throw new ArgumentNullException(nameof(shortcuts));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    /// <inheritdoc />
    public async Task<InstallOutcome> InstallAsync(
        InstallOptions options,
        IProgress<InstallProgress> progress,
        CancellationToken cancellation) {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(progress);

        progress.Report(new InstallProgress(-1, "Starting..."));

        var archive = await AcquireAsync(progress, cancellation).ConfigureAwait(false);

        var executable = await UnpackAsync(archive, options, progress, cancellation)
            .ConfigureAwait(false);

        LeaveMarks(executable, options, progress);
        progress.Report(new InstallProgress(100, "Finishing up..."));

        // Its bytes are the program now, so the archive goes: this is the
        // one point where it is provably not wanted any more.
        TryDelete(archive);

        return new InstallOutcome(executable);
    }

    /// <summary>
    /// The bytes to install from: the copy the launcher's fetch already put
    /// down, or a fetch of this walk's own when there is none. The bar
    /// counts the fetch as the first two fifths of the whole work, so a
    /// person watching one bar never sees it run backwards.
    /// </summary>
    private async Task<string> AcquireAsync(
        IProgress<InstallProgress> progress,
        CancellationToken cancellation) {
        if (_download.Landed is { ArchivePath: { } landed } && File.Exists(landed)) {
            progress.Report(new InstallProgress(40, "Using the copy already downloaded..."));
            return landed;
        }

        var wrapped = new InlineProgress<InstallProgress>(note => progress.Report(
            new InstallProgress(
                note.Percent < 0 ? -1 : note.Percent * 40 / 100,
                note.Message)));

        var outcome = await _download.DownloadAsync(wrapped, cancellation).ConfigureAwait(false);
        return outcome.ArchivePath;
    }

    /// <summary>
    /// Unpacks the archive into the chosen folder and finds the program at
    /// the bottom of it. The unpack runs off the UI thread because it is
    /// real work — a self-contained editor is tens of megabytes of one
    /// file — and the person is watching a bar rather than a frozen window.
    /// An archive in a shape this installer does not open is refused at
    /// the door rather than misread. Two are opened below by a walk of
    /// their own — the zip, and the tar.gz the Unix legs are packed in —
    /// and any other shape meets the same refusal it always did, because
    /// an unknown shape is a stream this walk cannot honestly read.
    /// </summary>
    private Task<string> UnpackAsync(
        string archive,
        InstallOptions options,
        IProgress<InstallProgress> progress,
        CancellationToken cancellation) {
        if (!archive.EndsWith(_runtime.ArchiveExtension, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidDataException(
                $"The archive is not in the {_runtime.ArchiveExtension} shape this installer opens.");
        }

        progress.Report(new InstallProgress(-1, "Unpacking the archive..."));

        var destination = Path.GetFullPath(options.Destination);
        var inside = destination.EndsWith(Path.DirectorySeparatorChar)
            ? destination
            : destination + Path.DirectorySeparatorChar;

        return Task.Run(() => {
            Directory.CreateDirectory(destination);

            // The door above matched the archive's own name against the
            // shape this machine declared; what opens it is the same shape,
            // read the way that family of bytes is written.
            if (string.Equals(_runtime.ArchiveExtension, ".zip", StringComparison.OrdinalIgnoreCase)) {
                UnpackZip(archive, destination, inside, progress, cancellation);
            } else if (string.Equals(_runtime.ArchiveExtension, ".tar.gz", StringComparison.OrdinalIgnoreCase)) {
                UnpackTarGz(archive, destination, inside, progress, cancellation);
            } else {
                throw new InvalidDataException(
                    $"The {_runtime.ArchiveExtension} shape is not one this installer opens.");
            }

            return FindExecutable(destination, _runtime.ExecutableName);
        }, cancellation);
    }

    /// <summary>
    /// The zip walk: every entry read for its size up front, so the bar can
    /// count the whole unpack from the very start.
    /// </summary>
    private static void UnpackZip(
        string archive,
        string destination,
        string inside,
        IProgress<InstallProgress> progress,
        CancellationToken cancellation) {
        using var zip = ZipFile.OpenRead(archive);
        var total = zip.Entries.Sum(entry => entry.Length);
        long done = 0;

        foreach (var entry in zip.Entries) {
            cancellation.ThrowIfCancellationRequested();

            // Every entry must land under the folder the person chose:
            // an archive is somebody else's bytes, and it does not get
            // to decide where on this machine they end up.
            var path = Path.GetFullPath(Path.Combine(destination, entry.FullName));
            RefuseEscape(path, inside);

            if (entry.Name.Length == 0) {
                Directory.CreateDirectory(path);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            entry.ExtractToFile(path, overwrite: true);

            done += entry.Length;
            progress.Report(new InstallProgress(
                total > 0 ? 40 + (int)(done * 50 / total) : -1,
                "Unpacking the archive..."));
        }
    }

    /// <summary>
    /// The tar.gz walk: the gzip layer comes off first, then each entry the
    /// tar names, and what the tar said the file may be allowed to do is
    /// put back beside its bytes. A tar carries no standing total, so the
    /// bar stays honest by not guessing at a number it was never handed.
    /// </summary>
    private static void UnpackTarGz(
        string archive,
        string destination,
        string inside,
        IProgress<InstallProgress> progress,
        CancellationToken cancellation) {
        using var gzip = new GZipStream(File.OpenRead(archive), CompressionMode.Decompress);
        using var tar = new TarReader(gzip);

        while (tar.GetNextEntry() is { } entry) {
            cancellation.ThrowIfCancellationRequested();

            var path = Path.GetFullPath(Path.Combine(destination, entry.Name));
            RefuseEscape(path, inside);

            switch (entry.EntryType) {
                case TarEntryType.Directory:
                    Directory.CreateDirectory(path);
                    break;
                case TarEntryType.RegularFile:
                case TarEntryType.V7RegularFile:
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    entry.ExtractToFile(path, overwrite: true);
                    GiveMode(path, entry.Mode);

                    progress.Report(new InstallProgress(-1, "Unpacking the archive..."));
                    break;
            }
        }
    }

    /// <summary>
    /// The rule every entry answers to, wherever it came from: nothing the
    /// archive carries reaches outside the folder the person chose.
    /// </summary>
    private static void RefuseEscape(string path, string inside) {
        if (!path.StartsWith(inside, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidDataException(
                "The archive reaches outside the folder it was unpacked into.");
        }
    }

    /// <summary>
    /// What the tar said the file may do, put back after its bytes are: a
    /// program unpacked without its permission to run is bytes on a disk
    /// rather than something this machine will start. Windows keeps no such
    /// bits, and not keeping them there is not a failure.
    /// </summary>
    private static void GiveMode(string path, UnixFileMode mode) {
        if (OperatingSystem.IsWindows()) {
            return;
        }

        try {
            File.SetUnixFileMode(path, mode);
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
        }
    }

    /// <summary>
    /// The program the archive was carrying, named the way this machine
    /// names it and falling back to whatever top-level file carries the
    /// program's own stem, so a release that ever renames the file still
    /// installs rather than failing over a name nobody outside this folder
    /// has ever seen.
    /// </summary>
    private static string FindExecutable(string destination, string executableName) {
        var named = Path.Combine(destination, executableName);
        if (File.Exists(named)) {
            return named;
        }

        var stem = Path.GetFileNameWithoutExtension(executableName);
        var found = Directory
            .GetFiles(destination)
            .Where(path => Path.GetFileName(path)
                .StartsWith(stem, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return found.Length > 0
            ? found[0]
            : throw new InvalidDataException(
                "The archive held no program, so there is nothing to install.");
    }

    /// <summary>
    /// The marks the person asked for — each one offered on the options
    /// page rather than assumed, and each absent one simply not asked for.
    /// </summary>
    private void LeaveMarks(
        string executable,
        InstallOptions options,
        IProgress<InstallProgress> progress) {
        var description = "A small plain-text editor for Windows.";

        if (options.CreateDesktopShortcut) {
            progress.Report(new InstallProgress(92, "Adding a desktop icon..."));
            _shortcuts.CreateDesktop(executable, executable, description);
        }

        if (options.CreateStartMenuShortcut) {
            progress.Report(new InstallProgress(95, "Adding a Start menu entry..."));
            _shortcuts.CreateStartMenu(executable, executable, description);
        }

        if (options.CreateTaskbarShortcut) {
            progress.Report(new InstallProgress(98, "Pinning to the taskbar..."));
            _shortcuts.PinToTaskbar(executable, executable, description);
        }
    }

    /// <summary>
    /// Takes an archive back, if it can — the work is over by then, and a
    /// file that refuses to go is not worth failing the install over.
    /// </summary>
    private static void TryDelete(string archive) {
        try {
            if (File.Exists(archive)) {
                File.Delete(archive);
            }
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
        }
    }
}
