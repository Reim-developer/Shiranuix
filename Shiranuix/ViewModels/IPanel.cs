namespace Shiranuix.ViewModels;

/// <summary>
/// A piece of the editor's own window that stands beside the page rather than
/// in the status line: the file tree is the first, and the only one there is
/// room for a writer to ask for today.
/// </summary>
/// <remarks>
/// <para>
/// The editor mounts the panel; whatever is inside it is the panel's own. There
/// is no layout negotiation and no docking on a grid this size: the panels sit
/// in one column down the left of the window, one at a time, the way a side bar
/// works in every editor worth copying.
/// </para>
/// <para>
/// A panel is built once, on the editor's own thread, and then left alone: the
/// editor does not resize it, re-lay it out, or reach inside it.
/// </para>
/// </remarks>
public interface IPanel {
    /// <summary>
    /// The panel's own id, namespaced by what offers it — the same
    /// rule command ids follow, so nothing can quietly take a panel's place.
    /// </summary>
    string Id { get; }

    /// <summary>What the panel's tab says.</summary>
    string Title { get; }

    /// <summary>
    /// A glyph drawn on the panel's tab, or null for the title alone.
    /// </summary>
    /// <remarks>
    /// One character, not a font: the editor draws it in the same place and the
    /// same size as every other glyph in the window. Prefer <see cref="Icon"/>
    /// where a panel can name a drawing — a glyph is a character from
    /// whatever font the reader has, and two readers with two fonts see two
    /// different marks.
    /// </remarks>
    string? Glyph { get; }

    /// <summary>
    /// The icon drawn on the panel's tab, named rather than drawn - or null
    /// to leave the tab to <see cref="Glyph"/>.
    /// </summary>
    /// <remarks>
    /// One name out of the Lucide set (<c>lucide.dev/icons</c>), which is the
    /// set every icon in the editor is drawn from: the toolbar, the file tree,
    /// and every panel's tab, so the window is one hand rather than a drawer
    /// of several. Spelling does not matter — "FolderTree", "folder-tree" and
    /// "folder tree" all find the same icon, because nobody should lose a
    /// panel's icon to a hyphen.
    /// </remarks>
    string? Icon { get; }

    /// <summary>
    /// The control the editor puts in the panel's column.
    /// </summary>
    /// <remarks>
    /// Asked once, when the panel is first shown, and never again — the same
    /// control instance stays put, keeping whatever state it holds.
    /// </remarks>
    Avalonia.Controls.Control View { get; }
}
