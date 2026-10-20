using Narika.Services.Install;

namespace Narika.Tests.Fakes;

/// <summary>
/// The install service's double: it records what it was asked to do and
/// reports what it was told to report, and never touches a disk.
/// </summary>
/// <remarks>
/// By default the whole installation completes on the spot, so a test
/// walks the wizard and finds the work already done. Setting
/// <see cref="Gate"/> holds the work open until the test lets it go — or
/// until cancellation lets it go instead, which is how the cancel path is
/// watched: a cancelled token trips the registration below, which both
/// tells the test the cancellation arrived and fails the wait the way the
/// real service's own delay would.
/// </remarks>
public sealed class FakeInstallService : IInstallService {
    /// <summary>
    /// Holds the installation open when set; the test closes it by
    /// completing it, and a cancellation completes it by cancelling it.
    /// </summary>
    public TaskCompletionSource? Gate { get; set; }

    /// <summary>
    /// Completed the moment the service is entered, so a test knows the
    /// walk has reached the installation before it does anything that
    /// should stop it.
    /// </summary>
    public TaskCompletionSource Started { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Completed when the service observes the cancellation it was handed.
    /// </summary>
    public TaskCompletionSource CancelObserved { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// The options the wizard collected along the whole walk, recorded the
    /// moment they are handed over.
    /// </summary>
    public InstallOptions? Received { get; private set; }

    /// <summary>
    /// What this installation leaves behind; a test points it somewhere
    /// the finish page's launch can be watched for.
    /// </summary>
    public InstallOutcome Outcome { get; set; } =
        new(@"C:\Apps\Shiranuix\Shiranuix.exe");

    /// <inheritdoc />
    public async Task<InstallOutcome> InstallAsync(
        InstallOptions options,
        IProgress<InstallProgress> progress,
        CancellationToken cancellation) {
        Received = options;
        progress.Report(new InstallProgress(-1, "Starting..."));
        Started.TrySetResult();

        if (Gate is { } gate) {
            // The wait fails the way the real service's delay fails: on a
            // cancelled token, by throwing — and only then, so a test that
            // is still holding the gate keeps the installation running.
            using var registration = cancellation.Register(() => {
                CancelObserved.TrySetResult();
                gate.TrySetCanceled(cancellation);
            });

            await gate.Task;
        } else {
            cancellation.ThrowIfCancellationRequested();
        }

        progress.Report(new InstallProgress(100, "Done."));
        return Outcome;
    }
}
