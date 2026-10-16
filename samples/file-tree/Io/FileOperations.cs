using FileTree.Nodes;
using Shiranuix.Plugins;

namespace FileTree.Io;

/// <summary>
/// The things a tree asks the disk to do: make, rename, move, delete.
/// </summary>
/// <remarks>
/// <para>
/// Every one of these is checked before the disk is touched, and every refusal
/// says which name was refused and why - because a name typed into a tree is
/// the most likely place in an editor for something to go quietly wrong. A
/// name that is empty, that carries a separator, that is one of the two
/// relatives, or that is already taken is refused here rather than becoming a
/// file somewhere nobody meant.
/// </para>
/// <para>
/// Nothing is asked twice: a refusal from the disk is passed on as it came,
/// since the disk knows things about permissions that no list of names does.
/// </para>
/// </remarks>
public static class FileOperations {
    private static readonly char[] ForbiddenInAName = Path.GetInvalidFileNameChars();

    /// <summary>
    /// Makes an empty file.
    /// </summary>
    /// <param name="folder">The folder to put it in.</param>
    /// <param name="name">What to call it.</param>
    /// <returns>The outcome, and where the file went when it worked.</returns>
    public static PluginOutcome<string> CreateFile(string folder, string name) {
        if (RefuseName(name) is { } refused) {
            return PluginOutcome<string>.Failure(refused);
        }

        var path = System.IO.Path.Combine(folder, name);
        if (System.IO.File.Exists(path) || Directory.Exists(path)) {
            return PluginOutcome<string>.Failure($"There is already something called '{name}'.");
        }

        try {
            using var _ = new FileStream(path, FileMode.CreateNew);
            return PluginOutcome<string>.From(path);
        } catch (Exception error) when (IsRefusal(error)) {
            return PluginOutcome<string>.Failure($"Could not make '{name}': {error.Message}");
        }
    }

    /// <summary>
    /// Makes a folder, with every folder above it.
    /// </summary>
    /// <param name="folder">The folder to put it in.</param>
    /// <param name="name">What to call it.</param>
    /// <returns>The outcome, and where the folder went when it worked.</returns>
    public static PluginOutcome<string> CreateFolder(string folder, string name) {
        if (RefuseName(name) is { } refused) {
            return PluginOutcome<string>.Failure(refused);
        }

        var path = System.IO.Path.Combine(folder, name);
        if (System.IO.File.Exists(path) || Directory.Exists(path)) {
            return PluginOutcome<string>.Failure($"There is already something called '{name}'.");
        }

        try {
            Directory.CreateDirectory(path);
            return PluginOutcome<string>.From(path);
        } catch (Exception error) when (IsRefusal(error)) {
            return PluginOutcome<string>.Failure($"Could not make '{name}': {error.Message}");
        }
    }

    /// <summary>
    /// Gives a file or folder another name, in the folder it is already in.
    /// </summary>
    /// <param name="node">What to rename.</param>
    /// <param name="name">What to call it instead.</param>
    /// <returns>The outcome, and where it went when it worked.</returns>
    public static PluginOutcome<string> Rename(FileNode node, string name) {
        ArgumentNullException.ThrowIfNull(node);

        if (node.Parent is null) {
            return PluginOutcome<string>.Failure("A tree's root has no name to change.");
        }

        if (RefuseName(name) is { } refused) {
            return PluginOutcome<string>.Failure(refused);
        }

        if (string.Equals(node.Name, name, StringComparison.OrdinalIgnoreCase)) {
            return PluginOutcome<string>.Failure($"It is already called '{name}'.");
        }

        var folder = node.Parent.FullPath;
        var wanted = System.IO.Path.Combine(folder, name);
        if (System.IO.File.Exists(wanted) || Directory.Exists(wanted)) {
            return PluginOutcome<string>.Failure($"There is already something called '{name}'.");
        }

        try {
            if (node.IsDirectory) {
                Directory.Move(node.FullPath, wanted);
            } else {
                File.Move(node.FullPath, wanted);
            }

            return PluginOutcome<string>.From(wanted);
        } catch (Exception error) when (IsRefusal(error)) {
            return PluginOutcome<string>.Failure(
                $"Could not rename '{node.Name}': {error.Message}");
        }
    }

    /// <summary>
    /// Moves a file or folder into another folder.
    /// </summary>
    /// <param name="node">What to move.</param>
    /// <param name="intoFolder">The folder to move it into.</param>
    /// <returns>The outcome, and where it went when it worked.</returns>
    /// <remarks>
    /// A folder is never moved inside itself, which the disk would allow and
    /// nobody meant: the result is a tree that contains itself.
    /// </remarks>
    public static PluginOutcome<string> Move(FileNode node, string intoFolder) {
        ArgumentNullException.ThrowIfNull(node);

        if (node.IsDirectory && IsInside(node.FullPath, intoFolder)) {
            return PluginOutcome<string>.Failure(
                "A folder cannot be moved inside itself.");
        }

        if (!Directory.Exists(intoFolder)) {
            return PluginOutcome<string>.Failure($"There is no folder at '{intoFolder}'.");
        }

        var wanted = System.IO.Path.Combine(intoFolder, node.Name);
        if (System.IO.File.Exists(wanted) || Directory.Exists(wanted)) {
            return PluginOutcome<string>.Failure(
                $"'{intoFolder}' already holds something called '{node.Name}'.");
        }

        try {
            if (node.IsDirectory) {
                Directory.Move(node.FullPath, wanted);
            } else {
                File.Move(node.FullPath, wanted);
            }

            return PluginOutcome<string>.From(wanted);
        } catch (Exception error) when (IsRefusal(error)) {
            return PluginOutcome<string>.Failure(
                $"Could not move '{node.Name}': {error.Message}");
        }
    }

    /// <summary>
    /// Deletes a file, or a folder with everything in it.
    /// </summary>
    /// <param name="node">What to delete.</param>
    /// <returns>The outcome.</returns>
    /// <remarks>
    /// The tree never asks whether this is wise: it is asked once, through the
    /// editor's own question, and the answer arrives before this is called.
    /// </remarks>
    public static PluginOutcome Delete(FileNode node) {
        ArgumentNullException.ThrowIfNull(node);

        if (node.Parent is null) {
            return PluginOutcome.Failure("A tree's root cannot be deleted from inside itself.");
        }

        try {
            if (node.IsDirectory) {
                Directory.Delete(node.FullPath, recursive: true);
            } else {
                File.Delete(node.FullPath);
            }

            return PluginOutcome.Ok();
        } catch (Exception error) when (IsRefusal(error)) {
            return PluginOutcome.Failure($"Could not delete '{node.Name}': {error.Message}");
        }
    }

    /// <summary>
    /// Whether a path is inside another one.
    /// </summary>
    /// <param name="inner">The path that might be inside.</param>
    /// <param name="outer">The path that might contain it.</param>
    /// <returns>True when it is, or is the same folder.</returns>
    public static bool IsInside(string inner, string outer) {
        var prefix = outer.EndsWith(Path.DirectorySeparatorChar)
            ? outer
            : outer + Path.DirectorySeparatorChar;

        return string.Equals(inner, outer, StringComparison.OrdinalIgnoreCase)
            || inner.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Why a name cannot be used, or null when it can.
    /// </summary>
    /// <param name="name">The name to judge.</param>
    /// <returns>The sentence to refuse it with, or null.</returns>
    private static string? RefuseName(string? name) {
        if (string.IsNullOrWhiteSpace(name)) {
            return "A name cannot be empty.";
        }

        if (name is "." or "..") {
            return $"'{name}' is not a name; it is where you already are.";
        }

        if (name.IndexOfAny(ForbiddenInAName) >= 0) {
            return $"'{name}' carries something Windows will not put in a name.";
        }

        return null;
    }

    private static bool IsRefusal(Exception error) =>
        error is IOException or UnauthorizedAccessException or System.Security.SecurityException;
}
