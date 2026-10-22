using Avalonia.Controls;

namespace Shiranuix.Graphs.Dialogs;

/// <summary>
/// One yes/no question, shown over the window it is handed.
/// </summary>
/// <remarks>
/// <para>
/// The relationship map strikes things out — a graph, a character, a tie —
/// and asks before every strike, the way the rest of the application does.
/// What the map cannot own is the drawing of that prompt: the dialog that
/// draws it belongs to the application, which keeps it beside every other
/// warning it shows. So the question crosses as a question — its words and
/// the answer back — together with the window it must stand in front of,
/// and where it stands is the whole point: in front of the map, not in
/// front of the writing behind it.
/// </para>
/// </remarks>
public interface IConfirmQuestion {
    /// <summary>
    /// Shows the question over the given window and waits for the answer.
    /// </summary>
    /// <param name="owner">The window the question is shown in front of.</param>
    /// <param name="title">What the question is called by.</param>
    /// <param name="message">What is about to go, in the writer's own terms.</param>
    /// <param name="confirmLabel">What the button that strikes out says.</param>
    /// <returns>True when the writer said yes.</returns>
    Task<bool> Ask(Window owner, string title, string message, string confirmLabel);
}
