namespace Narika.Services.Install;

/// <summary>
/// What the launcher knows about what is already on the machine: whether
/// one application is installed, and where it was put when it was.
/// </summary>
/// <remarks>
/// Today's implementation remembers only in memory — honest, because no
/// real installation has written anything yet and a receipt file claiming
/// otherwise would send Launch at a program that does not exist. The day
/// the real installer runs, this interface is where it will drop its
/// receipt, and nothing above the interface learns the difference.
/// </remarks>
public interface IInstallState {
    /// <summary>
    /// Whether an installation is recorded as present.
    /// </summary>
    bool IsInstalled { get; }

    /// <summary>
    /// The installed executable, or null while nothing is installed.
    /// </summary>
    string? ExecutablePath { get; }

    /// <summary>
    /// Raised when the answer to "is it installed" changes — which is how
    /// the launcher hears that a setup window it opened has finished.
    /// </summary>
    event EventHandler? Changed;

    /// <summary>
    /// Records an installation as present.
    /// </summary>
    /// <param name="executablePath">What was installed, and where.</param>
    void MarkInstalled(string executablePath);
}
