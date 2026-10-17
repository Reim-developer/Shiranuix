using System.Runtime.Versioning;
using Microsoft.Win32;
using PdfSharp.Fonts;

namespace Shiranuix.Services.Pdf;

/// <summary>
/// The typefaces the machine itself carries, answered in the pen's own
/// kind of question: which file holds a family, and where.
/// </summary>
/// <remarks>
/// <para>
/// The pen that draws the PDF is not fluent in the machine's fonts on its
/// own: left to itself it knows a private shelf of a few core faces, and
/// a page set in Georgia would arrive with no face at all. This resolver
/// reads the two shelves the machine keeps - the one under the Windows
/// folder every writer shares, and the private one beside a user's own
/// folders - the places where Windows itself says which file each
/// family's name belongs to. What the sheet offers and what the paper
/// draws in are then one answer rather than two.
/// </para>
/// <para>
/// Styles arrive by their own family titles: the registry keeps
/// "Georgia Bold" beside "Georgia", each a file of its own. Where a
/// family has no separate bold or italic file, the plain face answers
/// anyway and the pen lays the style on itself, so nothing is drawn in a
/// face the machine never carried.
/// </para>
/// </remarks>
internal sealed class MachineFontResolver : IFontResolver {
    /// <summary>The resolver the pen is handed.</summary>
    public static readonly MachineFontResolver Instance = new();

    /// <summary>The shelves the machine keeps its typeface files on, read once.</summary>
    private IReadOnlyDictionary<string, string>? _faces;

    private MachineFontResolver() {
        // One resolver answers for the process. The pen is handed this
        // instance rather than building its own, so the shelves are read
        // once and read the same every time.
    }

    /// <inheritdoc />
    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic) {
        if (!OperatingSystem.IsWindows()) {
            return null;
        }

        _faces ??= ReadFaces();

        return _faces.TryGetValue(AskName(familyName, isBold, isItalic), out var styled)
            ? new FontResolverInfo(styled)
            : _faces.TryGetValue(familyName.ToLowerInvariant(), out var plain)
                ? new FontResolverInfo(plain, isBold, isItalic)
                : null;
    }

    /// <inheritdoc />
    public byte[]? GetFont(string faceName) {
        // The face the registry named is carried whole: the file's own
        // bytes go into the paper, so the reader's machine does not need
        // to carry the same faces to read the page as it was written.
        return File.Exists(faceName) ? File.ReadAllBytes(faceName) : null;
    }

    /// <summary>What the registry would call the asked family and style.</summary>
    private static string AskName(string familyName, bool isBold, bool isItalic) =>
        (familyName
            + (isBold ? " Bold" : string.Empty)
            + (isItalic ? " Italic" : string.Empty))
        .ToLowerInvariant();

    /// <summary>
    /// Reads the names of the families the machine keeps, and the files
    /// that hold them.
    /// </summary>
    /// <remarks>
    /// The shelves are read once, on the first ask. A shelf that cannot be
    /// read leaves the families it held unknown: the pen answers null for
    /// them, and the writer hears why nothing reached the disk.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    private static IReadOnlyDictionary<string, string> ReadFaces() {
        var faces = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        ReadShelf(Registry.LocalMachine, faces);
        ReadShelf(Registry.CurrentUser, faces);

        return faces;
    }

    /// <summary>
    /// Reads one shelf's registry of names into the list. Names already
    /// known keep their first answer, so the machine-wide shelf speaks
    /// before the private one.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static void ReadShelf(RegistryKey root, IDictionary<string, string> faces) {
        try {
            using var fonts = root.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\Fonts");

            if (fonts is null) {
                return;
            }

            foreach (var title in fonts.GetValueNames()) {
                var cut = title.IndexOf(" (", StringComparison.Ordinal);
                var family = cut < 0 ? title : title[..cut];

                if (family.Length == 0 || fonts.GetValue(title) is not string file || file.Length == 0) {
                    continue;
                }

                var path = file.Contains('\\')
                    ? file
                    : Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                        "Fonts",
                        file);

                faces.TryAdd(family.ToLowerInvariant(), path);
            }
        } catch (Exception flaw) {
            // The shelf stays closed rather than half-read: its families
            // are simply not offered, and the writer's own choice falls
            // back the way a choice the machine cannot carry always has.
            _ = flaw;
        }
    }
}
