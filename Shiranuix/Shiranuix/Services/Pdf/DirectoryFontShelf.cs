namespace Shiranuix.Services.Pdf;

/// <summary>
/// A shelf that is a set of folders: the way the other two systems keep
/// their typeface files, where no registry says which file holds which
/// family and the files say it themselves.
/// </summary>
/// <remarks>
/// <para>
/// Every typeface file under the folders is read for the name it carries
/// in its own name table, and what it says is what it offers: the family
/// plainly when the file is the regular cut of it, the family with its
/// style after it when the file is a bold or an italic — the two
/// spellings the resolver asks for. A file that cannot be opened, or
/// cannot name itself, is a file the shelf has never seen: the folders
/// may hold anything at all, and none of it stops the reading of the
/// rest.
/// </para>
/// <para>
/// The folders are read in the order given — the machine's own before a
/// user's — and the files within a folder in a fixed order, so a name
/// asked twice answers the same way both times.
/// </para>
/// </remarks>
internal sealed class DirectoryFontShelf : IFontShelf {
    private readonly string[] _directories;
    private readonly string[] _spares;

    internal DirectoryFontShelf(IReadOnlyList<string> directories, IReadOnlyList<string> spares) {
        _directories = [.. directories];
        _spares = [.. spares];
    }

    /// <inheritdoc />
    public string[] Spares => _spares;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> ReadFaces() {
        var faces = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in _directories) {
            foreach (var file in HeldBy(directory)) {
                try {
                    using var stream = File.OpenRead(file);
                    var names = TypefaceNames.Read(stream);

                    if (names is not { } said || said.Family.Length == 0) {
                        continue;
                    }

                    var title = said.Subfamily is "Regular" or { Length: 0 }
                        ? said.Family
                        : said.Family + " " + said.Subfamily;

                    faces.TryAdd(title.ToLowerInvariant(), file);
                } catch (Exception flaw) when (flaw is IOException or UnauthorizedAccessException) {
                    // A file the shelf cannot open is a file the shelf does
                    // not hold: it is passed over, and the folder goes on
                    // answering out of the files it can read.
                    _ = flaw;
                }
            }
        }

        return faces;
    }

    /// <summary>The typeface files a folder holds, in a fixed order.</summary>
    /// <param name="directory">One folder the shelf was given.</param>
    private static IReadOnlyList<string> HeldBy(string directory) {
        if (!Directory.Exists(directory)) {
            return [];
        }

        try {
            return Directory
                .EnumerateFiles(
                    directory,
                    "*",
                    new EnumerationOptions {
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = true,
                    })
                .Where(IsTypeface)
                .OrderBy(file => file, StringComparer.Ordinal)
                .ToList();
        } catch (Exception flaw) when (flaw is IOException or UnauthorizedAccessException or NotSupportedException) {
            // A folder the shelf cannot open is a folder the shelf was
            // never really given: it is passed over whole.
            _ = flaw;
            return [];
        }
    }

    /// <summary>Whether a file's ending is one a typeface file wears.</summary>
    private static bool IsTypeface(string file) =>
        file.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
        file.EndsWith(".otf", StringComparison.OrdinalIgnoreCase) ||
        file.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase);
}
