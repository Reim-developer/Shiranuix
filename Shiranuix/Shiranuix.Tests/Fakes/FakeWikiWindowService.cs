using Avalonia.Controls;
using Shiranuix.Graphs;

namespace Shiranuix.Tests.Fakes;

/// <summary>
/// A wiki window that never opens, but counts how many times it was asked
/// for — so a test can say that the map reaches the wiki without a window
/// standing anywhere.
/// </summary>
public sealed class FakeWikiWindowService : IWikiWindowService {
    /// <summary>How many times the wiki was asked for.</summary>
    public int Shown { get; private set; }

    /// <inheritdoc />
    public void Show() => Shown++;

    /// <inheritdoc />
    public Window? Current => null;
}
