using Narika.Services.Install;

namespace Narika.Tests.Fakes;

/// <summary>
/// The launcher's double: records what it was asked to start, so both
/// accounts of a launch — the one that runs and the one that fails — have
/// something to be watched through.
/// </summary>
public sealed class FakeAppLauncher : IAppLauncher {
    /// <summary>
    /// The executable the wizard handed over, or null when it never did.
    /// </summary>
    public string? Started { get; private set; }

    /// <summary>
    /// Whether the start should fail rather than take.
    /// </summary>
    public bool Fail { get; set; }

    /// <inheritdoc />
    public Task LaunchAsync(string executablePath) {
        Started = executablePath;

        return Fail
            ? Task.FromException(new InvalidOperationException("It will not start."))
            : Task.CompletedTask;
    }
}
