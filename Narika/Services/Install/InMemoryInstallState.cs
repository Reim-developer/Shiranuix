namespace Narika.Services.Install;

/// <summary>
/// <see cref="IInstallState"/> remembered for as long as the launcher is
/// open: one flag, one path, one event when either of them is written.
/// </summary>
public sealed class InMemoryInstallState : IInstallState {
    /// <inheritdoc />
    public bool IsInstalled { get; private set; }

    /// <inheritdoc />
    public string? ExecutablePath { get; private set; }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public void MarkInstalled(string executablePath) {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        IsInstalled = true;
        ExecutablePath = executablePath;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Uninstall() {
        IsInstalled = false;
        ExecutablePath = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
