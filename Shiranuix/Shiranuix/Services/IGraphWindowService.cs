namespace Shiranuix.Services;

/// <summary>
/// How the title band's button reaches the relationship graph's own window.
/// </summary>
/// <remarks>
/// Modeless, the way settings and the reader are: the graph stands beside
/// the writing rather than in its place, and one window answers every press
/// — an open graph is brought forward rather than shadowed by a second.
/// A closed window cannot be shown again, so the next press makes a fresh
/// one over whatever the store holds by then.
/// </remarks>
public interface IGraphWindowService {
    /// <summary>Brings the graph's window up, or opens it where none stands.</summary>
    void Show();
}
