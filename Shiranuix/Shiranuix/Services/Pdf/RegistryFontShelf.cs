using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Shiranuix.Services.Pdf;

/// <summary>
/// The shelf Windows keeps: the two registry shelves the machine writes
/// its typeface titles into, read the way the pen has always read them.
/// </summary>
/// <remarks>
/// <para>
/// Windows says which file holds each family in titles like "Georgia Bold
/// (georgiabd.ttf)" — the family and its style before the parenthesis,
/// the file after it, a bare file name standing for one in the Windows
/// fonts folder. The machine-wide shelf is read before the private one,
/// and a name already known keeps its first answer.
/// </para>
/// <para>
/// The shelves are read once, on the first ask of a face. A shelf that
/// cannot be read stays closed rather than half-read: its families are
/// simply not offered, and a choice the machine cannot carry falls back
/// the way it always has.
/// </para>
/// </remarks>
internal sealed class RegistryFontShelf : IFontShelf {
    /// <summary>
    /// The faces a Windows machine is expected to hold, in the order they
    /// are asked when the page's own cannot carry the writing. Times New
    /// Roman first: like Georgia, a serif, so a page handed over keeps the
    /// shape its writer chose rather than turning into something else
    /// entirely. Each carries the whole of Vietnamese, marks and all.
    /// </summary>
    public static readonly string[] WindowsSpares = [
        "Times New Roman",
        "Arial",
        "Segoe UI",
    ];

    /// <inheritdoc />
    public string[] Spares => WindowsSpares;

    /// <inheritdoc />
    [SupportedOSPlatform("windows")]
    public IReadOnlyDictionary<string, string> ReadFaces() {
        var faces = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        ReadShelf(Registry.LocalMachine, faces);
        ReadShelf(Registry.CurrentUser, faces);

        return faces;
    }

    /// <summary>
    /// The family a registry title carries: "Georgia Bold" out of "Georgia
    /// Bold (georgiai.ttf)", and the whole title when no file sits after
    /// one.
    /// </summary>
    /// <param name="title">One name as the registry keeps it.</param>
    internal static string FamilyOf(string title) {
        var cut = title.IndexOf(" (", StringComparison.Ordinal);

        return cut < 0 ? title : title[..cut];
    }

    /// <summary>
    /// Where a registry value points: a path stands as it is, and a bare
    /// file name is one in the folder given.
    /// </summary>
    /// <param name="value">One value as the registry keeps it.</param>
    /// <param name="fontsFolder">The folder a bare file name stands in.</param>
    internal static string PathOn(string value, string fontsFolder) =>
        value.Contains('\\') ? value : Path.Combine(fontsFolder, value);

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
                var family = FamilyOf(title);

                if (family.Length == 0 || fonts.GetValue(title) is not string file || file.Length == 0) {
                    continue;
                }

                var path = PathOn(
                    file,
                    Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                        "Fonts"));

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
