using Narika.Services.Install;

namespace Narika.Tests.Fakes;

/// <summary>
/// The uninstall service's double: it records what it was asked to take
/// back and reports what it was told to report, and never touches a disk.
/// </summary>
/// <remarks>
/// By default the whole removal completes on the spot, so a test presses
/// Uninstall twice and finds the work already done. Setting
/// <see cref="Gate"/> holds the work open until the test lets it go — or
/// until cancellation lets it go instead, which is how the cancel path is
/// watched.
/// </remarks>
public sealed class FakeUninstallService : IUninstallService {
    /// <summary>
    /// Holds the removal open when set; the test closes it by completing
    /// it, and a cancellation completes it by cancelling it.
    /// </summary>
    public TaskCompletionSource? Gate { get; set; }

    /// <summary>
    /// Completed the moment the service is entered, so a test knows the
    /// walk has reached the removal before it does anything that should
    /// stop it.
    /// </summary>
    public TaskCompletionSource Started { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Completed when the service observes the cancellation it was handed.
    /// </summary>
    public TaskCompletionSource CancelObserved { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// The installation the removal was asked to take back, recorded the
    /// moment it is handed over.
    /// </summary>
    public string? Received { get; private set; }

    /// <summary>
    /// Whether the removal should fail rather than finish, the way a
    /// folder held open elsewhere would — for the status line's own
    /// account of one.
    /// </summary>
    public bool Fail { get; set; }

    /// <summary>
    /// How many times the service has been walked: the difference between
    /// a removal that was asked for twice and one that actually ran.
    /// </summary>
    public int Calls { get; private set; }

    /// <inheritdoc />
    public async Task UninstallAsync(
        string executablePath,
        IProgress<InstallProgress> progress,
        CancellationToken cancellation) {
        Received = executablePath;
        Calls++;
        progress.Report(new InstallProgress(-1, "Starting..."));
        Started.TrySetResult();

        if (Gate is { } gate) {
            using var registration = cancellation.Register(() => {
                CancelObserved.TrySetResult();
                gate.TrySetCanceled(cancellation);
            });

            await gate.Task;
        } else {
            cancellation.ThrowIfCancellationRequested();
        }

        if (Fail) {
            throw new IOException("The disk, as ever.");
        }

        progress.Report(new InstallProgress(100, "Done."));
    }
}
