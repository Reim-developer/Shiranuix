namespace Shiranuix.Plugins.UI;

/// <summary>
/// Implemented by a plugin that has panels. The editor asks for this after the
/// plugin has started, and shows what it finds in the column down the left of
/// the window.
/// </summary>
/// <remarks>
/// Separate from the headless <c>IPlugin</c> on purpose: a plugin with nothing
/// to draw does not reference this package at all, and one that has panels
/// says so rather than returning an empty list that the editor has to wonder
/// about.
/// </remarks>
public interface IPluginPanelProvider {
    /// <summary>
    /// The panels this plugin offers.
    /// </summary>
    /// <remarks>
    /// Read once, while the plugin starts. The controls are built here, on the
    /// editor's thread, because that is the only thread Avalonia lets a
    /// control be built on.
    /// </remarks>
    IReadOnlyList<IPluginPanel> Panels { get; }
}
