using System.Runtime.InteropServices;

namespace Narika.Services.Install;

/// <summary>
/// The machine this launcher is standing on, said in the one vocabulary
/// the repository's release train publishes with: a runtime identifier
/// the assets spell themselves with, the archive shape this installer
/// opens, and the name the program goes by once its bytes are on disk.
/// </summary>
/// <remarks>
/// The catalog reads the identifier to know which asset among a release's
/// several is the one for this machine, and the install service reads the
/// name to know what it is unpacking for; neither spells a platform of its
/// own behind the other's back, which is the whole reason this record
/// stands between them. It is read once at the composition root and handed
/// to whoever needs it, so every test can stand the same code on any
/// machine there is.
/// </remarks>
public sealed record Runtime(
    string Identifier,
    string ArchiveExtension,
    string ExecutableName) {

    /// <summary>
    /// The machine actually running this launcher: the identifier the
    /// platform spells itself with, the one archive shape the train
    /// publishes today, and the program's own name — with an executable's
    /// ending where the machine keeps one.
    /// </summary>
    /// <param name="appId">Which application's release is being asked for.</param>
    public static Runtime ForMachine(string appId) => new(
        RuntimeInformation.RuntimeIdentifier,
        ".zip",
        OperatingSystem.IsWindows()
            ? NameOf(appId) + ".exe"
            : NameOf(appId));

    /// <summary>
    /// The application's own name as the repository spells it: the release
    /// train names its assets with it, and the program goes by it with an
    /// executable's ending where the machine keeps one.
    /// </summary>
    /// <param name="appId">The name's lower-case form.</param>
    public static string NameOf(string appId) =>
        char.ToUpperInvariant(appId[0]) + appId[1..];
}
