using Shiranuix.Plugins;

namespace Shiranuix.Plugins.UI;

/// <summary>
/// The editor's side of the window a plugin draws in: the column down the left,
/// and the tab strip across its top.
/// </summary>
/// <remarks>
/// A capability, like the document: a plugin asks for it and a host that has
/// no window to give hands back null. That is what keeps a plugin loadable in
/// a host that runs it headlessly — a test harness, a build check, a future
/// command-line mode — where there is nothing to mount a panel on.
/// </remarks>
public interface IPluginPanelHost {
    /// <summary>
    /// Puts a panel in the window's column.
    /// </summary>
    /// <param name="panel">The panel, with its control already built.</param>
    /// <returns>
    /// A refusal when this editor already has a panel under that id, or when
    /// the panel has not said who it is. First one wins here too, exactly as
    /// on the register: a plugin cannot take another's place by asking nicely
    /// or by arriving second.
    /// </returns>
    /// <exception cref="ArgumentNullException">The panel is null.</exception>
    PluginOutcome Show(IPluginPanel panel);

    /// <summary>
    /// Takes a panel back out of the window.
    /// </summary>
    /// <param name="id">The panel's id.</param>
    /// <returns>Whether there was one to take out.</returns>
    PluginOutcome Hide(string id);
}
