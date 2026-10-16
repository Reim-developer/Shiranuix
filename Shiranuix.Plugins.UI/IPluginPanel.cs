using Avalonia.Controls;

namespace Shiranuix.Plugins.UI;

/// <summary>
/// A piece of the editor's own window that a plugin draws: a file tree, an
/// outline, a clock — anything that wants to be beside the page rather than
/// in the status line.
/// </summary>
/// <remarks>
/// <para>
/// The editor mounts the panel; the plugin owns everything inside it. There is
/// no layout negotiation and no docking on a grid this size: the panels sit in
/// one column down the left of the window, one at a time, the way a side bar
/// works in every editor worth copying.
/// </para>
/// <para>
/// A panel is built once, on the editor's own thread, and then left alone: the
/// editor does not resize it, re-lay it out, or reach inside it. A panel that
/// needs to know how big it is reads its own control, and a panel that needs a
/// document asks the editor for one through the headless contract.
/// </para>
/// <para>
/// A plugin that wants no interface needs neither this package nor Avalonia.
/// That separation is the point of the split.
/// </para>
/// </remarks>
public interface IPluginPanel {
    /// <summary>
    /// The panel's own id, namespaced by the plugin that offers it — the same
    /// rule command ids follow, so two plugins can both have a "files" panel
    /// and neither can take the other's place.
    /// </summary>
    string Id { get; }

    /// <summary>What the panel's tab says.</summary>
    string Title { get; }

    /// <summary>
    /// A glyph drawn on the panel's tab, or null for the title alone.
    /// </summary>
    /// <remarks>
    /// One character, not a font: the editor draws it in the same place and the
    /// same size as every other glyph in the window, and a plugin that brought
    /// an icon font would have to bring the font with it for one letter.
    /// </remarks>
    string? Glyph { get; }

    /// <summary>
    /// The control the editor puts in the panel's column.
    /// </summary>
    /// <remarks>
    /// Asked once, when the panel is first shown, and never again — the same
    /// control instance stays put, keeping whatever state it holds. A panel
    /// that wants a fresh one each time is a panel that should be answering
    /// questions instead of holding state.
    /// </remarks>
    Control View { get; }
}
