namespace Shiranuix.Services;

/// <summary>
/// One button a dialog offers, and the answer clicking it gives.
/// </summary>
/// <typeparam name="TResult">
/// What the dialog answers with: a bool for a question, an enum for a choice
/// of several, a file path for a pick.
/// </typeparam>
/// <param name="Label">What the button says.</param>
/// <param name="Result">What it answers.</param>
/// <param name="IsDefault">
/// Whether the Enter key presses it. Keeps the safe answer under the default
/// where there is one.
/// </param>
/// <param name="IsCancel">
/// Whether the Escape key closes the dialog with this answer. A dismissed
/// dialog always has one.
/// </param>
public sealed record DialogButton<TResult>(
    string Label,
    TResult Result,
    bool IsDefault = false,
    bool IsCancel = false)
    : DialogButton(Label);
