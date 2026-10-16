namespace Shiranuix.Plugins.Dialogs;

/// <summary>
/// A question that wants a folder, the way a file tree needs somewhere to
/// stand.
/// </summary>
/// <param name="Message">What to say while the picker is open.</param>
/// <param name="Title">
/// What to call the picker window, or null for the host's own wording.
/// </param>
public readonly record struct PluginFolderPrompt(
    string Message,
    string? Title = null);
