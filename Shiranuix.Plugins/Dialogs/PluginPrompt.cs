namespace Shiranuix.Plugins.Dialogs;

/// <summary>
/// A question that wants a line of text back.
/// </summary>
/// <param name="Message">The question, in one sentence.</param>
/// <param name="Title">
/// What to call the question window, or null for the host's own wording.
/// </param>
/// <param name="InitialValue">What the box starts holding.</param>
public readonly record struct PluginPrompt(
    string Message,
    string? Title = null,
    string InitialValue = "");
