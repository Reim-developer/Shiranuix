namespace Narika.Services.Install;

/// <summary>
/// What an installation leaves behind that the wizard might want next: the
/// executable, so the finish page can offer to start it.
/// </summary>
/// <param name="ExecutablePath">
/// The full path of the program that was installed.
/// </param>
public sealed record InstallOutcome(string ExecutablePath);
