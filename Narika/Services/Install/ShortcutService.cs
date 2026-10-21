using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Narika.Services.Install;

/// <summary>
/// A Windows shortcut (.lnk), written the way the shell itself writes one:
/// an <c>IShellLink</c> dressed as a <c>WScript.Shell</c> link and saved
/// through <c>IPersistFile</c>. Nothing outside the platform is pulled in
/// for this, because a shortcut is the platform's own idea of a mark and
/// the platform already knows how to make one.
/// </summary>
/// <remarks>
/// This is the one place that knows a shortcut is a Windows file format
/// rather than a folder of loose files. Everything above it — the install
/// service, the options that ask for a desktop or start menu mark — asks in
/// terms of an application, a target and an icon, and never in terms of a
/// link file.
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed class ShortcutService : IShortcutService {
    /// <inheritdoc />
    public void CreateDesktop(string targetPath, string? iconPath, string description) {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        var folder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        Create(Path.Combine(folder, "Shiranuix.lnk"), targetPath, iconPath, description);
    }

    /// <inheritdoc />
    public void CreateStartMenu(string targetPath, string? iconPath, string description) {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        // The per-user start menu: no permission is needed to write here,
        // and what lands here outlives the session that put it there.
        var programs = Environment.GetFolderPath(
            Environment.SpecialFolder.Programs,
            Environment.SpecialFolderOption.Create);
        var folder = Path.Combine(programs, "Shiranuix");
        Directory.CreateDirectory(folder);

        Create(Path.Combine(folder, "Shiranuix.lnk"), targetPath, iconPath, description);
    }

    /// <inheritdoc />
    public void PinToTaskbar(string targetPath, string? iconPath, string description) {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        // Windows offers no public say in pinning: the taskbar keeps a
        // folder of links of its own, and a link written there is one the
        // shell reads as pinned. It is the only door there is, and it may
        // take the taskbar a moment to notice the newcomer.
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "Internet Explorer",
            "Quick Launch",
            "User Pinned",
            "TaskBar");
        Directory.CreateDirectory(folder);

        Create(Path.Combine(folder, "Shiranuix.lnk"), targetPath, iconPath, description);
    }

    /// <inheritdoc />
    public void RemoveDesktop(string executable) {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);

        var folder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        TryDelete(Path.Combine(folder, NameOf(executable)));
    }

    /// <inheritdoc />
    public void RemoveStartMenu(string executable) {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);

        var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        var folder = Path.Combine(programs, "Shiranuix");

        TryDelete(Path.Combine(folder, NameOf(executable)));

        // The folder was made to hold the entry, so with the entry gone it
        // should go too — and only when it is empty, because whatever else
        // a person put in there is not this walk's to take.
        try {
            if (Directory.Exists(folder)) {
                Directory.Delete(folder, recursive: false);
            }
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
        }
    }

    /// <inheritdoc />
    public void RemoveTaskbar(string executable) {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);

        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "Internet Explorer",
            "Quick Launch",
            "User Pinned",
            "TaskBar");

        TryDelete(Path.Combine(folder, NameOf(executable)));
    }

    /// <summary>
    /// The link file a program's mark goes by: its own name with a link's
    /// ending, which is the name every mark here was written under.
    /// </summary>
    private static string NameOf(string executable) =>
        Path.GetFileNameWithoutExtension(executable) + ".lnk";

    /// <summary>
    /// Takes one mark back, if it is there — a surface that refuses to give
    /// a mark back is not worth failing the whole removal over, because the
    /// link points at a program that will be gone either way.
    /// </summary>
    private static void TryDelete(string linkPath) {
        try {
            if (File.Exists(linkPath)) {
                File.Delete(linkPath);
            }
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
        }
    }

    /// <summary>
    /// Writes one link file, resolving the shell's own type through its
    /// programmatic ID rather than through a pinned GUID, which is the form
    /// that survives a machine whose shell has been registered elsewhere.
    /// </summary>
    private static void Create(
        string linkPath,
        string targetPath,
        string? iconPath,
        string description) {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException(
                "This machine offers no WScript.Shell, so no shortcut can be written.");

        dynamic shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("The shell could not be created.");

        try {
            dynamic link = shell.CreateShortcut(linkPath);

            try {
                link.TargetPath = targetPath;
                link.WorkingDirectory = Path.GetDirectoryName(targetPath);
                link.IconLocation = string.IsNullOrWhiteSpace(iconPath)
                    ? targetPath + ",0"
                    : iconPath + ",0";
                link.Description = description;
                link.Save();
            } finally {
                (link as IDisposable)?.Dispose();
            }
        } finally {
            (shell as IDisposable)?.Dispose();
        }
    }
}
