using System.Diagnostics;

namespace Shiranuix.FileTree.Io;

/// <summary>
/// Opening a folder where the operating system keeps it.
/// </summary>
/// <remarks>
/// <para>
/// Two things a tree can do that are nothing to do with a tree: put a path on
/// the clipboard, and show a folder in Explorer. Both need a running window
/// and a desktop, which is why they live beside the view rather than in the
/// tree's host - and both are quiet when they cannot be done, because a tree that
/// says "cannot" at a writer who right-clicked a row is worse than one that
/// simply does nothing there.
/// </para>
/// <para>
/// Explorer is asked directly, the way a Windows program asks for one. The
/// program is cut for every runtime the editor stands on now, but this door
/// is the platform's own - it opens a Windows window - so there is nothing
/// here to be portable to.
/// </para>
/// </remarks>
internal static class Explorer {
    /// <summary>
    /// Shows a folder in Explorer, with a file inside it selected when there
    /// is one.
    /// </summary>
    /// <param name="folder">The folder to show.</param>
    /// <param name="fileInside">A file in it to select, or null to show the
    /// folder on its own.</param>
    public static void Reveal(string folder, string? fileInside = null) {
        if (!Directory.Exists(folder)) {
            return;
        }

        try {
            var arguments = fileInside is null || !File.Exists(fileInside)
                ? $"\"{folder}\""
                : $"/select,\"{fileInside}\"";

            Process.Start(new ProcessStartInfo("explorer.exe", arguments) {
                UseShellExecute = true
            });
        } catch (Exception error) when (error is System.ComponentModel.Win32Exception
            or InvalidOperationException) {
            // Nothing to say: there is no window to say it in.
        }
    }
}
