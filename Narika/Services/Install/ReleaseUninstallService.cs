namespace Narika.Services.Install;

/// <summary>
/// The removal, made real: the marks the install left — the desktop icon,
/// the start menu entry, the taskbar pin — are taken back where each
/// surface keeps them, and the folder the files were unpacked into is
/// taken back whole. What comes out the far end is nothing at all, which
/// is the whole point.
/// </summary>
/// <remarks>
/// Only the program's own folder is ever taken: the folder above it may
/// be one a person chose because other things live beside this program,
/// and the bottom of a drive is never taken at all, whatever a receipt
/// somewhere claims — a removal walks the same line the unpack refuses
/// to cross, in the other direction. A mark that refuses to go is not
/// worth failing the removal over: the link will point at a program that
/// is gone either way, and saying so would be saying half a removal
/// failed when the machine is already clean of the program itself.
/// </remarks>
public sealed class ReleaseUninstallService : IUninstallService {
    private readonly IShortcutService _shortcuts;

    /// <summary>
    /// Creates the removal over the surfaces it takes marks back from.
    /// </summary>
    /// <param name="shortcuts">Where the marks were left, and are taken.</param>
    public ReleaseUninstallService(IShortcutService shortcuts) =>
        _shortcuts = shortcuts ?? throw new ArgumentNullException(nameof(shortcuts));

    /// <inheritdoc />
    public Task UninstallAsync(
        string executablePath,
        IProgress<InstallProgress> progress,
        CancellationToken cancellation) {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(progress);

        return Task.Run(() => {
            progress.Report(new InstallProgress(-1, "Removing the marks..."));

            _shortcuts.RemoveDesktop(executablePath);
            _shortcuts.RemoveStartMenu(executablePath);
            _shortcuts.RemoveTaskbar(executablePath);

            cancellation.ThrowIfCancellationRequested();

            // The program's own folder, taken back whole — and never the
            // bottom of a drive, which is the one folder no receipt gets
            // to name as a thing to take.
            var folder = Path.GetDirectoryName(Path.GetFullPath(executablePath));
            var root = folder is null ? null : Path.GetPathRoot(folder);

            if (folder is { Length: > 0 }
                && root is not null
                && !string.Equals(root, folder, StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(folder)) {
                progress.Report(new InstallProgress(50, "Taking the files back..."));
                Directory.Delete(folder, recursive: true);
            }

            progress.Report(new InstallProgress(100, "Done."));
        }, cancellation);
    }
}
