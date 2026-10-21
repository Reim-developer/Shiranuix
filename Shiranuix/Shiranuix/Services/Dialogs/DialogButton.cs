namespace Shiranuix.Services.Dialogs;

/// <summary>
/// One button a dialog offers.
/// </summary>
/// <remarks>
/// Non-generic on purpose: the dialog's markup binds this shape, so it can be
/// compiled. The answer a button carries is typed, and lives in
/// <see cref="DialogButton{TResult}"/>.
/// </remarks>
/// <param name="Label">What the button says.</param>
public abstract record DialogButton(string Label);
