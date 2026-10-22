using Avalonia.Controls;

namespace Shiranuix.Graphs;

/// <summary>
/// How the title band's button reaches the relationship graph's own window.
/// </summary>
/// <remarks>
/// Modeless, the way settings and the reader are: the graph stands beside
/// the writing rather than in its place, and one window answers every press
/// — an open graph is brought forward rather than shadowed by a second.
/// A closed window cannot be shown again, so the next press makes a fresh
/// one over whatever the store holds by then. The standing window itself
/// is handed out too, because the map's dialogs are shown over it: a
/// question raised from the map must answer in front of the map, or the
/// focus walks back to the writing behind it.
/// </remarks>
public interface IGraphWindowService {
    /// <summary>Brings the graph's window up, or opens it where none stands.</summary>
    void Show();

    /// <summary>
    /// The map's window as it stands now, or nothing while none stands —
    /// the window every dialog this map asks is shown over.
    /// </summary>
    Window? Current { get; }
}
