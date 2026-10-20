namespace Narika.Services.Install;

/// <summary>
/// The installation as it stands today: a stand-in that walks the steps a
/// real install will walk, in order, at a human pace.
/// </summary>
/// <remarks>
/// Not the installer — the installer is a later task, and will take its
/// bytes from <see cref="IReleaseCatalog"/>'s release and write them into
/// the destination this stub only pretends to use. What this class is for
/// is everything around it: the wizard's progress page, its bars and its
/// labels, its cancellation and the tests over all of them need a service
/// that reports like one, and this is exactly that much and no more. When
/// the real service arrives it replaces this registration and no view
/// model changes with it.
/// </remarks>
public sealed class StubInstallService : IInstallService {
    /// <summary>
    /// The steps, in the order a real install will take them: the label
    /// says what is happening, the number says how far along the whole
    /// thing that leaves the person waiting.
    /// </summary>
    private static readonly (string Message, int Percent)[] Steps = [
        ("Downloading Shiranuix from its release page...", 30),
        ("Unpacking the archive...", 55),
        ("Writing the files...", 85),
        ("Finishing up...", 100)
    ];

    /// <inheritdoc />
    public async Task<InstallOutcome> InstallAsync(
        InstallOptions options,
        IProgress<InstallProgress> progress,
        CancellationToken cancellation) {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(progress);

        progress.Report(new InstallProgress(-1, "Starting..."));
        await Task.Delay(250, cancellation);

        foreach (var (message, percent) in Steps) {
            cancellation.ThrowIfCancellationRequested();
            progress.Report(new InstallProgress(percent, message));
            await Task.Delay(350, cancellation);
        }

        // Where the real service will have written its bytes: the
        // executable at the bottom of the destination, which is what the
        // finish page needs in order to offer starting it.
        return new InstallOutcome(Path.Combine(options.Destination, "Shiranuix.exe"));
    }
}
