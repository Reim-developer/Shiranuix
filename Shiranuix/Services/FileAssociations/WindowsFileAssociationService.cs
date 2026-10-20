using System.Runtime.Versioning;
using Microsoft.Win32;
using Serilog;

namespace Shiranuix.Services.FileAssociations;

/// <summary>
/// Writes <see cref="FileAssociationPlan"/> into the current user's half of
/// the machine's registry, so Explorer can name a Shiranuix Document and
/// offer to open a text file with one.
/// </summary>
/// <remarks>
/// <para>
/// Everything is written under <c>HKEY_CURRENT_USER</c>, never the machine's
/// own half, so nothing here needs an administrator and nothing here reaches
/// past the person running it. Deleting takes back exactly what creating put
/// in, which is what makes an unregister worth offering at all.
/// </para>
///
/// <para>
/// Nothing in this class guesses. Which keys, which names and which words go
/// in them are all decided by the plan and read back by nothing here; this is
/// the part that has to touch a machine, and it does only that. Which is also
/// why no test opens it: a test that wrote a real registry would be a test
/// about somebody's computer, and the plan is where the questions live.
/// </para>
///
/// <para>
/// This is also the only thing in the application that is given a chance to
/// fail at a point where the application must not. A machine that will not
/// have its registry written is still a machine that has to run the editor,
/// so every door in here is answered from the caller's side by a caller that
/// logs what happened and opens the window anyway.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsFileAssociationService : IFileAssociationService {
    private readonly string _executablePath;
    private readonly string _version;

    /// <summary>
    /// Creates the service over the running build.
    /// </summary>
    /// <param name="executablePath">
    /// Where the editor was started from; the running executable when not
    /// said. This is the path every menu the plan writes will name, so a
    /// build standing somewhere odd has to say so rather than be wrong.
    /// </param>
    /// <param name="version">The build's version; the app's when not said.</param>
    public WindowsFileAssociationService(string? executablePath = null, string? version = null) {
        _executablePath = executablePath ?? Environment.ProcessPath
            ?? throw new InvalidOperationException(
                "There is no executable to point a shell at.");

        _version = version ?? AppVersion.Text;
    }

    /// <inheritdoc />
    public void EnsureRegistered() {
        if (MarkerSays(_executablePath, _version)) {
            // This very build, standing in this very place, already has.
            // Reading a handful of values is what this costs; writing them
            // again would be the same work done over.
            return;
        }

        foreach (var entry in FileAssociationPlan.For(_executablePath, _version)) {
            Write(entry);
        }

        Log.Information(
            "Windows was told about Shiranuix {Version}, running from {Path}.",
            _version,
            _executablePath);
    }

    /// <inheritdoc />
    public void Unregister() {
        foreach (var entry in FileAssociationPlan.ValuesToForget) {
            using var key = Registry.CurrentUser.OpenSubKey(entry.Key, writable: true);
            key?.DeleteValue(entry.Name, throwOnMissingValue: false);
        }

        foreach (var name in FileAssociationPlan.KeysToForget) {
            Registry.CurrentUser.DeleteSubKeyTree(name, throwOnMissingSubKey: false);
        }

        Log.Information("Windows was let go of Shiranuix.");
    }

    /// <summary>
    /// Puts one value in, creating the key it goes under if it is not there.
    /// </summary>
    /// <param name="entry">What to keep, and where.</param>
    private static void Write(RegisteredValue entry) {
        using var key = Registry.CurrentUser.CreateSubKey(entry.Key, writable: true);

        // A value with nothing in it is a fact about what a file type could
        // be opened with, and is read by presence rather than by contents.
        if (entry.Value is { } value) {
            key.SetValue(entry.Name, value, RegistryValueKind.String);
        } else {
            key.SetValue(entry.Name, Array.Empty<byte>(), RegistryValueKind.None);
        }
    }

    /// <summary>
    /// Whether a note left by an earlier run still describes this one.
    /// </summary>
    /// <param name="executablePath">Where the editor was started from.</param>
    /// <param name="version">The build's version, in words.</param>
    /// <returns>True when the very same build stands in the very same place.</returns>
    private static bool MarkerSays(string executablePath, string version) {
        using var key = Registry.CurrentUser.OpenSubKey(FileAssociationPlan.MarkerKey);

        // Comparing ordinally rather than as paths: the shell that wrote the
        // last one wrote it in its own words, and a path that differs in case
        // is a path this build was started by another way through.
        return string.Equals(
            key?.GetValue(FileAssociationPlan.MarkerName) as string,
            FileAssociationPlan.Marker(executablePath, version),
            StringComparison.Ordinal);
    }
}
