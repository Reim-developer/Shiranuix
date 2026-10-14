using System.Windows.Input;
using Shiranuix.Services;

namespace Shiranuix.ViewModels;

/// <summary>
/// What a dialog window binds: the title, the message, and a button per answer.
/// </summary>
/// <remarks>
/// Non-generic on purpose. The window and its template bind this shape, so the
/// markup compiles, while the answer a dialog carries is typed in
/// <see cref="DialogViewModel{TResult}"/> beneath it. The surface stays plain
/// and the payload stays typed — that split is what lets one window serve every
/// dialog.
///
/// A dialog takes one answer and then goes away, so this holds no property
/// that can change and needs no change notification.
/// </remarks>
public abstract class DialogContent {
    /// <summary>The dialog's title.</summary>
    public abstract string Title { get; }

    /// <summary>What the dialog has to say.</summary>
    public abstract string Message { get; }

    /// <summary>The answers it offers, in the order they appear.</summary>
    public abstract IReadOnlyList<DialogButton> Buttons { get; }

    /// <summary>The command the buttons run: answer, then close.</summary>
    public abstract ICommand AnswerCommand { get; }

    /// <summary>
    /// Answers with the button pressed. A button that is not this dialog's — or
    /// none at all — answers nothing.
    /// </summary>
    /// <param name="button">The button the user pressed.</param>
    public abstract void Answer(DialogButton? button);
}
