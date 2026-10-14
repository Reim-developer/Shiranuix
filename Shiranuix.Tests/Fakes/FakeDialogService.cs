using Shiranuix.Services.Dialogs;

namespace Shiranuix.Tests.Fakes;

/// <summary>
/// A dialog service that records the request and answers from a preset, so
/// tests never open a window.
/// </summary>
/// <remarks>
/// The interface is generic; the recording here is for the bool questions this
/// application asks, so the generic call is recognised by type and cast down.
/// </remarks>
public sealed class FakeDialogService : IDialogService {
    /// <summary>
    /// The answer to give. Null models the window being closed without one.
    /// </summary>
    public bool? Result { get; set; }

    /// <summary>Every request shown, in order.</summary>
    public List<DialogRequest<bool>> Requests { get; } = new();

    /// <summary>The last request shown, or null when none was.</summary>
    public DialogRequest<bool>? LastRequest => Requests.Count > 0 ? Requests[^1] : null;

    /// <summary>
    /// The file name the last prompt was about, read out of its message.
    /// </summary>
    public string? LastFileName {
        get {
            var message = LastRequest?.Message;
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
        if (typeof(TResult) != typeof(bool)) {
            return Task.FromResult<TResult?>(default);
        }

        Requests.Add((DialogRequest<bool>)(object)request);

        // The answer is a bool for a bool question, and null for a dismissed
        // dialog.
        var answer = Result.HasValue ? (TResult?)(object)Result.Value : default(TResult?);

        return Task.FromResult(answer);
    }
}
