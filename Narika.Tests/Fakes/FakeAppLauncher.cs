using Narika.Services.Install;

namespace Narika.Tests.Fakes;

/// <summary>
/// The launcher's double: records what it was asked to start, so the
/// finish page's last question has something to be answered by.
/// </summary>
public sealed class FakeAppLauncher : IAppLauncher {
    /// <summary>
    /// The executable the wizard handed over, or null when it never did.
    /// </summary>
    public string? Started { get; private set; }

    /// <inheritdoc />
    public Task LaunchAsync(string executablePath) {
        Started = executablePath;
        return Task.CompletedTask;
    }
}
