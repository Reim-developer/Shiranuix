using System.Diagnostics;

namespace Narika.Services.Install;

/// <summary>
/// Starts an installed program the ordinary way: hands its path to the
/// machine and lets the machine do the rest.
/// </summary>
public sealed class ProcessAppLauncher : IAppLauncher {
    /// <inheritdoc />
    public Task LaunchAsync(string executablePath) {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        Process.Start(new ProcessStartInfo(executablePath) {
            UseShellExecute = true
        });

        return Task.CompletedTask;
    }
}
