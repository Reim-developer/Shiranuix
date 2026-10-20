using Avalonia.Controls;

namespace Narika.Services.Setup;

/// <summary>
/// The setup wizard as the launcher opens it: a window, shown when there
/// is something to set up and answered when it is done.
/// </summary>
/// <remarks>
/// The wizard is its own window rather than a page of the launcher because
/// it is a sequence with its own chrome — fixed size, three buttons, a
/// walk that starts again from the top next time. That starting again is
/// why this service hands out a <em>fresh</em> window each time: the walk
/// remembers nothing between openings, and the launcher behind it keeps
/// everything it needs in <see cref="Install.IInstallState"/>.
/// </remarks>
public interface ISetupWindowService {
    /// <summary>
    /// Whether a setup window is showing right now — which is the one
    /// thing that holds Download still while one is open.
    /// </summary>
    bool IsOpen { get; }

    /// <summary>
    /// The window a dialog should belong to while setup is open, or null
    /// when it is not — which is the answer the folder picker is given
    /// through the same broken circle the editor's own pickers use.
    /// </summary>
    Window? CurrentWindow { get; }

    /// <summary>
    /// Raised whenever a setup window closes, whether it finished the
    /// walk or the person cancelled out of it — the launcher uses it to
    /// put the status line back in words that are true again.
    /// </summary>
    event EventHandler? Closed;

    /// <summary>
    /// Opens a fresh setup wizard over the installed state: it arrives
    /// empty, walks itself, and when its window closes the state it
    /// recorded is what the launcher reads.
    /// </summary>
    void Show();
}
