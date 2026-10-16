using Avalonia.Media;

namespace FileTree;

/// <summary>
/// The tree's own drawings, as geometry rather than as a font.
/// </summary>
/// <remarks>
/// <para>
/// Paths rather than glyphs from a system font, for three reasons that all
/// point the same way: they do not depend on a font being installed, they do
/// not depend on the editor's choice of icon library, and they draw in the
/// editor's own colour, so they are right in the dark theme without a second
/// drawing.
/// </para>
/// <para>
/// Every one is drawn on a sixteen-square grid, so a row's icons line up with
/// each other whatever size the tree is drawn at.
/// </para>
/// </remarks>
internal static class TreeIcons {
    /// <summary>Points right: a folder that is closed.</summary>
    public static Geometry ChevronRight { get; } = Parse("M6.5,3.5 L11,8 L6.5,12.5");

    /// <summary>Points down: a folder that is open.</summary>
    public static Geometry ChevronDown { get; } = Parse("M3.5,6.5 L8,11 L12.5,6.5");

    /// <summary>A folder.</summary>
    public static Geometry Folder { get; } = Parse("M1.5,4 H6.5 L8,5.5 H14.5 V12.5 H1.5 Z");

    /// <summary>A file.</summary>
    public static Geometry File { get; } = Parse("M4,1.5 H9.5 L12.5,4.5 V14.5 H4 Z");

    /// <summary>A lens and a handle: what the tree's box filters by.</summary>
    public static Geometry Search { get; } = Parse(
        "M7,2.5 a4.5,4.5 0 1,0 0,9 a4.5,4.5 0 1,0 0,-9 M10.5,10.5 L14,14");

    /// <summary>Reading the folder again.</summary>
    public static Geometry Refresh { get; } = Parse(
        "M13,8 a5,5 0 1,1 -2,-4.5 M13,1 V4 H10");

    /// <summary>A file that does not have its words yet.</summary>
    public static Geometry NewFile { get; } = Parse(
        "M4,1.5 H9.5 L12.5,4.5 V14.5 H4 Z M6.5,8 H10.5 M8.5,6 V10");

    /// <summary>A folder that does not have its things yet.</summary>
    public static Geometry NewFolder { get; } = Parse(
        "M1.5,4 H6.5 L8,5.5 H14.5 V12.5 H1.5 Z M6,8.5 H10 M8,6.5 V10.5");

    /// <summary>Something going away for good.</summary>
    public static Geometry Delete { get; } = Parse(
        "M3.5,4.5 H12.5 M6.5,4.5 V14 H9.5 V4.5 M4.5,4.5 L5,2.5 H11 L11.5,4.5");

    /// <summary>Another name for the same thing.</summary>
    public static Geometry Rename { get; } = Parse("M2.5,13.5 L3.5,10.5 L11,3 L13,5 L5.5,12.5 Z");

    /// <summary>The same path, written down somewhere else.</summary>
    public static Geometry CopyPath { get; } = Parse(
        "M2.5,2.5 H9 V11 H2.5 Z M5.5,13.5 H14 V5.5 H13");

    /// <summary>The folder, opened where the operating system keeps it.</summary>
    public static Geometry Reveal { get; } = Parse(
        "M2.5,4 H6 L7.5,5.5 H13.5 V12.5 H2.5 Z M8.5,7.5 H11.5 M11.5,7.5 L10,6 M11.5,7.5 L10,9");

    /// <summary>Names that start with a dot.</summary>
    public static Geometry Hidden { get; } = Parse(
        "M1.5,8 C4,4.5 6.5,4.5 8,4.5 C9.5,4.5 12,4.5 14.5,8 C12,11.5 9.5,11.5 8,11.5 C6.5,11.5 4,11.5 1.5,8 Z M8,6.5 a1.5,1.5 0 1,0 0,3 a1.5,1.5 0 1,0 0,-3");

    /// <summary>Names in one order rather than another.</summary>
    public static Geometry Sort { get; } = Parse("M4.5,3 V13 M2.5,5 L4.5,3 L6.5,5 M11.5,13 V3 M9.5,11 L11.5,13 L13.5,11");

    /// <summary>A row the writer has marked.</summary>
    public static Geometry Mark { get; } = Parse("M3,8.5 L6.5,12 L13,4.5");

    private static Geometry Parse(string figures) => Geometry.Parse(figures);
}
