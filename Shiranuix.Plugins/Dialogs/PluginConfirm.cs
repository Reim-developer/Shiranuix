namespace Shiranuix.Plugins.Dialogs;

/// <summary>
/// A question that wants a yes or a no.
/// </summary>
/// <param name="Message">The question, in one sentence.</param>
/// <param name="Title">
/// What to call the question window, or null for the host's own wording.
/// </param>
/// <param name="ConfirmLabel">
/// What the yes button says, or null for the host's own wording — which is
/// "Yes" in the editor's language.
/// </param>
/// <param name="CancelLabel">
/// What the no button says, or null for the host's own wording.
/// </param>
public readonly record struct PluginConfirm(
    string Message,
    string? Title = null,
    string? ConfirmLabel = null,
    string? CancelLabel = null);
