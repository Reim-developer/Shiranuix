using Narika.Services.Download;
using Narika.Services.Install;

namespace Narika.Tests.Fakes;

/// <summary>
/// The download service's double: it reports what it was told to report
/// and never touches a network.
/// </summary>
/// <remarks>
/// By default the whole fetch completes on the spot, so a test walks the
/// launcher and finds the work already done. Setting <see cref="Gate"/>
/// holds it open until the test lets it go — or until a cancellation lets
/// it go instead, which is how the cancel path is watched. Setting
/// <see cref="Fail"/> makes it throw the way a dead network would, for the
/// status line's own account of one.
/// </remarks>
public sealed class FakeDownloadService : IDownloadService {
    /// <summary>
    /// Holds the fetch open when set; the test closes it by completing it,
    /// and a cancellation completes it by cancelling it.
    /// </summary>
    public TaskCompletionSource? Gate { get; set; }

    /// <summary>
    /// Whether the fetch should fail rather than land.
    /// </summary>
    public bool Fail { get; set; }

    /// <summary>
    /// Completed the moment the service is entered, so a test knows the
    /// fetch has begun before it does anything that should stop it.
    /// </summary>
    public TaskCompletionSource Started { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Completed when the service observes the cancellation it was handed.
    /// </summary>
    public TaskCompletionSource CancelObserved { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// What this fetch "downloads"; a test only ever reads it.
    /// </summary>
    public DownloadOutcome Outcome { get; set; } =
        new(@"C:\Temp\Narika\Shiranuix.zip");

    /// <summary>
    /// What has landed, exactly as the real service keeps it: written by a
    /// fetch that finished, or set by a test standing in for one that did.
    /// </summary>
    public DownloadOutcome? Landed { get; set; }

    /// <summary>
    /// How many times the fetch was walked — one is a Download or a Repair
    /// that fetched its bytes; two is an install that should have asked
    /// <see cref="Landed"/> instead of pulling them down again.
    /// </summary>
    public int Calls { get; private set; }

    /// <inheritdoc />
    public async Task<DownloadOutcome> DownloadAsync(
        IProgress<InstallProgress> progress,
        CancellationToken cancellation) {
        progress.Report(new InstallProgress(-1, "Starting..."));
        Calls++;
        Started.TrySetResult();

        if (Gate is { } gate) {
            // The wait fails the way the real service's delay fails: on a
            // cancelled token, by throwing — and only then, so a test that
            // is still holding the gate keeps the fetch running.
            using var registration = cancellation.Register(() => {
                CancelObserved.TrySetResult();
                gate.TrySetCanceled(cancellation);
            });

            await gate.Task;
        } else {
            cancellation.ThrowIfCancellationRequested();
        }

        if (Fail) {
            throw new IOException("The network, as ever.");
        }

        progress.Report(new InstallProgress(100, "Done."));
        Landed = Outcome;
        return Outcome;
    }
}
