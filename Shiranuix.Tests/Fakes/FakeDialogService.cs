using Shiranuix.Services.Dialogs;

namespace Shiranuix.Tests.Fakes;

/// <summary>
/// A dialog service that records the request and answers from a preset, so
/// tests never open a window.
/// </summary>
/// <remarks>
/// The interface is generic, and a question asked of it may be about a bool or
/// about a nullable bool — the editor asks "did they discard" as a bool, while a
/// plugin's question has to be able to say "they closed it without answering",
/// which only a nullable bool can carry. Both are recorded here in the same
/// shape: what was asked, in words, and what was answered.
/// </remarks>
public sealed class FakeDialogService : IDialogService {
    /// <summary>
    /// The answer to give. Null models the window being closed without one.
    /// </summary>
    public bool? Result { get; set; }

    /// <summary>Every request shown, in order.</summary>
    public List<DialogRequest<bool>> Requests { get; } = [];

    /// <summary>
    /// Every question asked, in words, whether it wanted a bool or a nullable
    /// one — which is what a test about labels or titles wants to assert on.
    /// </summary>
    public List<AskedQuestion> Asked { get; } = [];

    /// <summary>The last request shown, or null when none was.</summary>
    public DialogRequest<bool>? LastRequest => Requests.Count > 0 ? Requests[^1] : null;

    /// <summary>The last question asked, or null when none was.</summary>
    public AskedQuestion? LastAsked => Asked.Count > 0 ? Asked[^1] : null;

    /// <summary>
    /// The file name the last prompt was about, read out of its message.
    /// </summary>
    public string? LastFileName {
        get {
            var message = LastRequest?.Message ?? LastAsked?.Message;
            if (message is null) {
                return null;
            }

            // "“story.txt” has unsaved changes." - the name is the quoted part.
            var first = message.IndexOf('"');
            var last = message.LastIndexOf('"');

            return first >= 0 && last > first
                ? message[(first + 1)..last]
                : null;
        }
    }

    /// <inheritdoc />
    public Task<TResult?> ShowAsync<TResult>(DialogRequest<TResult> request) {
        if (request is not { } asked) {
            return Task.FromResult(default(TResult?));
        }

        Asked.Add(new AskedQuestion(
            asked.Title,
            asked.Message,
            asked.Buttons.Select(button => button.Label).ToArray()));

        if (typeof(TResult) == typeof(bool)) {
            Requests.Add((DialogRequest<bool>)(object)request);
        }

        if (typeof(TResult) != typeof(bool?)) {
            // Not a nullable yes-or-no: a plain bool answers what it can and
            // a closed window reads as No, which is what the editor's own
            // questions have always meant.
            return Task.FromResult(
                Result.HasValue ? (TResult?)(object)Result.Value : default(TResult?));
        }

        // A nullable one can honestly say "nobody answered".
        return Task.FromResult<TResult?>(
            Result is { } answer ? (TResult?)(object)answer : default(TResult?));
    }

    /// <summary>
    /// One question, in the words it was asked.
    /// </summary>
    /// <param name="Title">What it was called.</param>
    /// <param name="Message">What it had to say.</param>
    /// <param name="Buttons">What it offered, in order.</param>
    public sealed record AskedQuestion(
        string Title, string Message, IReadOnlyList<string> Buttons);
}
