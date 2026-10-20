namespace Narika.Services.Install;

/// <summary>
/// The work of installing one application, behind the one name the wizard
/// knows it by.
/// </summary>
/// <remarks>
/// The wizard hands over options and takes back progress; where the bytes
/// come from — a download from the release pages, a folder beside the
/// installer, a payload tucked inside it — is this implementation's own
/// business and never the view model's. That split is what lets the whole
/// wizard be tested against a double that answers instantly.
/// </remarks>
public interface IInstallService {
    /// <summary>
    /// Carries out the requested installation, reporting as it goes.
    /// </summary>
    /// <param name="options">Where to install and what marks to leave.</param>
    /// <param name="progress">Told how far along the work is.</param>
    /// <param name="cancellation">
    /// Asked of the service throughout: a cancelled token stops the work
    /// and throws, leaving what was written as it was found.
    /// </param>
    /// <returns>What the installation left behind.</returns>
    Task<InstallOutcome> InstallAsync(
        InstallOptions options,
        IProgress<InstallProgress> progress,
        CancellationToken cancellation);
}
