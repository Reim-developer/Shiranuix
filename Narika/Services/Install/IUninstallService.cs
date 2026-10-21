namespace Narika.Services.Install;

/// <summary>
/// Taking an installation back off the machine: the marks the install
/// left on its way in, and the folder its files were unpacked into —
/// everything back to how the machine stood before that install, and
/// nothing of anybody else's touched on the way.
/// </summary>
/// <remarks>
/// Which surfaces a mark was left on, and what taking one back is made
/// of, is the implementation's own business; this name only says a
/// removal was asked for — the same wall the install side keeps between
/// the walk and the machine.
/// </remarks>
public interface IUninstallService {
    /// <summary>
    /// Takes the installation at the given path off the machine.
    /// </summary>
    /// <param name="executablePath">Where the installed program lives.</param>
    /// <param name="progress">Where every stretch of the way is reported.</param>
    /// <param name="cancellation">What stops the walk if the person gives up.</param>
    Task UninstallAsync(
        string executablePath,
        IProgress<InstallProgress> progress,
        CancellationToken cancellation);
}
