namespace Shiranuix.FileTree.Testing;

/// <summary>
/// A folder of files a test can lean on, taken away with the test.
/// </summary>
/// <remarks>
/// The tree of files has to be tested against files. This is the disk it
/// gets: a real folder under the temporary directory, created in one call,
/// and deleted on the way out however loudly the test failed.
/// </remarks>
public sealed class TempTree : IDisposable {
    /// <summary>The folder's path.</summary>
    public string Root { get; }

    /// <summary>
    /// Creates an empty folder, with a path inside it for the odd relative path
    /// a test wants to check is refused.
    /// </summary>
    public TempTree() {
        Root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "shiranuix-tests", Guid.NewGuid().ToString("n"));

        System.IO.Directory.CreateDirectory(Root);
    }

    /// <summary>
    /// A path inside the folder, whether or not anything is there yet.
    /// </summary>
    /// <param name="relative">What the path is called, below the folder.</param>
    /// <returns>The full path.</returns>
    public string PathTo(string relative) => System.IO.Path.Combine(Root, relative);

    /// <summary>
    /// Writes a file, making the folders above it as it goes.
    /// </summary>
    /// <param name="relative">What the file is called, below the folder.</param>
    /// <param name="contents">What goes in it.</param>
    /// <returns>The full path of the file.</returns>
    public string WriteFile(string relative, string contents = "") {
        var path = PathTo(relative);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, contents);

        return path;
    }

    /// <summary>
    /// Makes a folder, with every folder above it.
    /// </summary>
    /// <param name="relative">What the folder is called, below the root.</param>
    /// <returns>The full path.</returns>
    public string WriteFolder(string relative) {
        var path = PathTo(relative);
        System.IO.Directory.CreateDirectory(path);

        return path;
    }

    /// <summary>
    /// Whether something is there.
    /// </summary>
    /// <param name="relative">What to look for, below the root.</param>
    /// <returns>True when a file or folder of that name exists.</returns>
    public bool Exists(string relative) =>
        System.IO.File.Exists(PathTo(relative)) || System.IO.Directory.Exists(PathTo(relative));

    /// <summary>
    /// The names directly inside a folder, sorted — which is what a tree of
    /// files is made of, in the order a test can assert on.
    /// </summary>
    /// <param name="relative">The folder, below the root, or nothing for the root itself.</param>
    /// <returns>The names in it.</returns>
    public IReadOnlyList<string> Names(string relative = "") =>
        System.IO.Directory
            .GetFileSystemEntries(relative == "" ? Root : PathTo(relative))
            .Select(name => name ?? string.Empty)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    /// <summary>
    /// Takes the folder away, whether or not everything in it can be.
    /// </summary>
    public void Dispose() {
        try {
            if (System.IO.Directory.Exists(Root)) {
                System.IO.Directory.Delete(Root, recursive: true);
            }
        } catch (System.IO.IOException) {
        } catch (UnauthorizedAccessException) {
        }
    }
}
