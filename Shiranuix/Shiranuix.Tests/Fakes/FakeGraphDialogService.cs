using Shiranuix.Graphs.Dialogs;

namespace Shiranuix.Tests.Fakes;

/// <summary>
/// The relationship map's questions answered from a queue, so a test says
/// what the writer answers before it asks — and reads back every question
/// that was asked afterwards.
/// </summary>
/// <remarks>
/// An empty queue answers "the writer walked away", which is the only
/// answer none of the dialogs can be talked out of. The yes/no question
/// has its own switch rather than a queue, because most tests are about
/// what happens after consent and not about the consent itself.
/// </remarks>
public sealed class FakeGraphDialogService : IGraphDialogService {
    /// <summary>The answers in the order the dialogs will be asked for them.</summary>
    public Queue<object?> Answers { get; } = new();

    /// <summary>Every question asked, as one line each — what was answered rides in beside it.</summary>
    public List<string> Asked { get; } = [];

    /// <summary>What the yes/no question answers.</summary>
    public bool Confirm { get; set; } = true;

    /// <inheritdoc />
    public Task<string?> AskGraphName(string? initial, string? error) =>
        Task.FromResult(Next<string>($"name:{initial ?? "<empty>"}:{error ?? "<clean>"}"));

    /// <inheritdoc />
    public Task<WikiCharacterDraft?> AskCharacterInformation(WikiCharacterDraft? initial, string? error) =>
        Task.FromResult(Next<WikiCharacterDraft>(
            $"character:{initial?.Name ?? "<empty>"}:{error ?? "<clean>"}"));

    /// <inheritdoc />
    public Task<RelationshipDraft?> AskRelationship(
        IReadOnlyList<GraphNodeInfo> characters,
        int? fromId,
        int? toId,
        string? label,
        string? error) =>
        Task.FromResult(Next<RelationshipDraft>(
            $"relationship:{fromId}:{toId}:{label ?? "<empty>"}:{error ?? "<clean>"}"));

    /// <inheritdoc />
    public Task<string?> AskRelationshipLabel(string current, string fromName, string toName) =>
        Task.FromResult(Next<string>($"label:{fromName}->{toName}:{current}"));

    /// <inheritdoc />
    public Task<bool> AskConfirm(string message) {
        Asked.Add($"confirm:{message}");
        return Task.FromResult(Confirm);
    }

    private T? Next<T>(string question) where T : class {
        Asked.Add(question);
        return Answers.Count > 0 ? Answers.Dequeue() as T : null;
    }
}
