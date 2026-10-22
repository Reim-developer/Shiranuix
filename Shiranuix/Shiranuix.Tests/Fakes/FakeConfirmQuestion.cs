using Avalonia.Controls;
using Shiranuix.Graphs.Dialogs;

namespace Shiranuix.Tests.Fakes;

/// <summary>
/// A question that is never shown: it answers as it is told to, and keeps
/// what it was asked — so a test can say that a question was put before
/// something was struck out, and what it said.
/// </summary>
public sealed class FakeConfirmQuestion : IConfirmQuestion {
    /// <summary>What the question is answered with.</summary>
    public bool Answer { get; set; } = true;

    /// <summary>Every question that was asked, in the order they were asked.</summary>
    public List<string> Asked { get; } = new();

    /// <inheritdoc />
    public Task<bool> Ask(Window owner, string title, string message, string confirmLabel) {
        Asked.Add(message);
        return Task.FromResult(Answer);
    }
}
