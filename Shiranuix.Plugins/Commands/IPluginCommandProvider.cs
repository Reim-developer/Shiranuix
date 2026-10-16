namespace Shiranuix.Plugins.Commands;

/// <summary>
/// Implemented by a plugin that has commands. The host asks for this after
/// <see cref="IPlugin.InitializeAsync"/> has run, and shows what it finds in
/// the keyboard settings.
/// </summary>
/// <remarks>
/// Separate from <see cref="IPlugin"/> so that a plugin with nothing to add to
/// the keyboard implements one interface instead of two, and so the host can
/// tell "no commands" from "commands that failed to load" — which it says
/// different things about in the log.
/// </remarks>
public interface IPluginCommandProvider {
    /// <summary>
    /// The commands this plugin contributes.
    /// </summary>
    /// <remarks>
    /// Read once, after the plugin is initialized, and held for as long as the
    /// plugin runs. Returning a fresh list each time is harmless but pointless:
    /// the keyboard settings would show commands appearing and disappearing.
    /// </remarks>
    IReadOnlyList<IPluginCommand> Commands { get; }
}
