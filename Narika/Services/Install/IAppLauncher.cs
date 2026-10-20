namespace Narika.Services.Install;

/// <summary>
/// Starts an application that is already installed, the one thing the
/// finish page offers after everything else is done.
/// </summary>
public interface IAppLauncher {
    /// <summary>
    /// Starts the program at the given path.
    /// </summary>
    /// <param name="executablePath">The installed executable.</param>
    Task LaunchAsync(string executablePath);
}
