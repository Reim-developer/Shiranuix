namespace Narika.Services.Download;

/// <summary>
/// The fetch as it stands today: a stand-in that walks the steps a real
/// download will walk, in order, at a human pace.
/// </summary>
/// <remarks>
/// Not the download — the download is the same later task as the installer
/// itself, and will take its bytes from the release catalog for real. What
/// this class is for is everything around it: the launcher's progress bar,
/// its labels, its cancel button, and the tests over all of them need a
/// service that reports like one, and this is exactly that much and no
/// more. When the real service arrives it replaces this registration and
/// no view model changes with it.
/// </remarks>
public sealed class StubDownloadService : IDownloadService {
    /// <summary>
    /// The steps, in the order a real fetch takes them: the label says what
    /// is happening, the number says how far along the whole thing that
    /// leaves the person waiting.
    /// </summary>
    private static readonly (string Message, int Percent)[] Steps = [
        ("Contacting the release page...", 15),
        ("Downloading Shiranuix...", 60),
        ("Checking the archive...", 90),
        ("Verifying...", 100)
    ];

    /// <inheritdoc />
    public async Task<DownloadOutcome> DownloadAsync(
        IProgress<Install.InstallProgress> progress,
        CancellationToken cancellation) {
        ArgumentNullException.ThrowIfNull(progress);

        progress.Report(new Install.InstallProgress(-1, "Starting..."));
        await Task.Delay(250, cancellation);

        foreach (var (message, percent) in Steps) {
            cancellation.ThrowIfCancellationRequested();
            progress.Report(new Install.InstallProgress(percent, message));
            await Task.Delay(350, cancellation);
        }

        // Where the real service will have put its bytes: beside the
        // app's own state, ready for setup to open over.
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Narika",
            "downloads");
        Directory.CreateDirectory(directory);

        return new DownloadOutcome(Path.Combine(directory, "Shiranuix.zip"));
    }
}
