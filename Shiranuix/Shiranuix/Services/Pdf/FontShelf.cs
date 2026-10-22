namespace Shiranuix.Services.Pdf;

/// <summary>
/// The shelf this machine keeps: picked once, by what the system is, and
/// the only place in the pen that knows which one it picked.
/// </summary>
/// <remarks>
/// The choice is made for the whole process — a machine does not become
/// another kind of machine while it runs — and everything above this line
/// asks the shelf without ever asking what it stands on.
/// </remarks>
internal static class FontShelf {
    private static readonly IFontShelf Chosen = Pick();

    /// <summary>The shelf the pen is handed for this machine.</summary>
    internal static IFontShelf Current => Chosen;

    private static IFontShelf Pick() {
        if (OperatingSystem.IsWindows()) {
            return new RegistryFontShelf();
        }

        if (OperatingSystem.IsMacOS()) {
            return new DirectoryFontShelf(MacFonts(), MacSpares);
        }

        if (OperatingSystem.IsLinux()) {
            return new DirectoryFontShelf(LinuxFonts(), LinuxSpares);
        }

        // A system that is none of the three keeps no shelf the pen knows
        // how to read: folders with nothing in them answer exactly what
        // the Windows-only answer always gave everywhere else — nothing.
        return new DirectoryFontShelf([], []);
    }

    /// <summary>
    /// The folders a Linux machine keeps its faces in: the ones every
    /// distribution shares, and the two beside a user's own home.
    /// </summary>
    private static string[] LinuxFonts() {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return [
            "/usr/share/fonts",
            "/usr/local/share/fonts",
            Path.Combine(home, ".local", "share", "fonts"),
            Path.Combine(home, ".fonts"),
        ];
    }

    /// <summary>
    /// The faces a Linux machine is expected to hold, in the order they are
    /// asked when the page's own cannot carry the writing: the Liberation
    /// pair first — a serif and its sans, the shapes Times New Roman and
    /// Arial would have drawn — and a DejaVu beside them. Each carries the
    /// whole of Vietnamese, marks and all.
    /// </summary>
    private static readonly string[] LinuxSpares = [
        "Liberation Serif",
        "Liberation Sans",
        "DejaVu Sans",
    ];

    /// <summary>
    /// The folders a macOS machine keeps its faces in: the system's own,
    /// the one beside it for everything the system added, and the one
    /// beside a user's home.
    /// </summary>
    private static string[] MacFonts() {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return [
            "/System/Library/Fonts",
            "/Library/Fonts",
            Path.Combine(home, "Library", "Fonts"),
        ];
    }

    /// <summary>
    /// The faces a macOS machine is expected to hold, in the order they are
    /// asked when the page's own cannot carry the writing: Times New Roman
    /// first — like Georgia, a serif, so a page handed over keeps the
    /// shape its writer chose — and an Arial beside it. Each carries the
    /// whole of Vietnamese, marks and all.
    /// </summary>
    private static readonly string[] MacSpares = [
        "Times New Roman",
        "Arial",
    ];
}
