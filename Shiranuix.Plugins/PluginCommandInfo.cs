namespace Shiranuix.Plugins;

/// <summary>
/// What one of a plugin's commands is called, and what key it starts on.
/// </summary>
/// <remarks>
/// A command's id is filed beside the editor's own — the same table the writer
/// sees in the keyboard settings — so it follows the same naming rule and is
/// namespaced by the plugin's own id: <c>word-count.count</c>, never a bare
/// <c>count</c> that another plugin might also want.
/// </remarks>
/// <param name="Id">The command's id, in the editor's own naming shape.</param>
/// <param name="Label">What the writer reads in the keyboard settings.</param>
/// <param name="DefaultGesture">
/// The chord the command starts on, or null when it starts on nothing. The
/// host checks this before offering it, and a chord it cannot read is treated
/// as no chord at all rather than as a broken command.
/// </param>
/// <param name="Description">
/// What the command does, in a line, or null when the label says it well
/// enough.
/// </param>
public readonly record struct PluginCommandInfo(
    string Id,
    string Label,
    string? DefaultGesture = null,
    string? Description = null);
