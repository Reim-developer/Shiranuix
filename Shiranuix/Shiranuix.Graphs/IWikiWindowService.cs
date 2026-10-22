using Avalonia.Controls;

namespace Shiranuix.Graphs;

/// <summary>
/// Shows the wiki in its own window, beside the writing.
/// </summary>
public interface IWikiWindowService {
    /// <summary>
    /// Opens the wiki, or brings it forward if it is already standing.
    /// </summary>
    void Show();

    /// <summary>
    /// The wiki's window, while it is standing.
    /// </summary>
    Window? Current { get; }
}
