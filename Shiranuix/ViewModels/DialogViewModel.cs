using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Shiranuix.Services;

namespace Shiranuix.ViewModels;

/// <summary>
/// One dialog's contents, carrying a typed answer.
/// </summary>
/// <typeparam name="TResult">What this dialog answers with.</typeparam>
/// <remarks>
/// The generic layer exists to carry the answer type. Everything the view binds
/// is on <see cref="DialogContent"/> above it, which is why one window shows
/// every dialog: the markup never has to name the answer type.
/// </remarks>
public sealed class DialogViewModel<TResult> : DialogContent {
    private readonly Action<TResult> _answer;

    /// <summary>
    /// Creates a view model over a request.
    /// </summary>
    /// <param name="request">What the dialog is to show.</param>
    /// <param name="answer">
    /// Called with the answer when a button is pressed; the service that built
    /// this uses it to close the window with the answer.
    /// </param>
    public DialogViewModel(DialogRequest<TResult> request, Action<TResult> answer) {
        _answer = answer ?? throw new ArgumentNullException(nameof(answer));
        Request = request ?? throw new ArgumentNullException(nameof(request));
        AnswerCommand = new RelayCommand<DialogButton>(Answer);
    }

    private DialogRequest<TResult> Request { get; }

    /// <inheritdoc />
    public override string Title => Request.Title;

    /// <inheritdoc />
    public override string Message => Request.Message;

    /// <inheritdoc />
    public override IReadOnlyList<DialogButton> Buttons => Request.Buttons;

    /// <inheritdoc />
    public override ICommand AnswerCommand { get; }

    /// <inheritdoc />
    public override void Answer(DialogButton? button) {
        // A button another dialog offered is not this dialog's to answer.
        if (button is DialogButton<TResult> pressed) {
            _answer(pressed.Result);
        }
    }
}
