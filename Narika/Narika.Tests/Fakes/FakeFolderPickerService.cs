using Narika.Services.Install;

namespace Narika.Tests.Fakes;

/// <summary>
/// The folder picker's double: it answers with whatever a test has set it
/// to answer with, and never opens anything.
/// </summary>
public sealed class FakeFolderPickerService : IFolderPickerService {
    /// <summary>
    /// The folder handed back, or null for a person who changed their mind.
    /// </summary>
    public string? Answer { get; set; }

    /// <inheritdoc />
    public Task<string?> PickFolderAsync() => Task.FromResult(Answer);
}
