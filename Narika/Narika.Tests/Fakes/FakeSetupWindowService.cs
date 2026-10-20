using Avalonia.Controls;
using Narika.Services.Setup;

namespace Narika.Tests.Fakes;

/// <summary>
/// The setup window service's double: it counts the windows it was asked
/// to open and hands the test the closing of one, since no window is ever
/// actually built.
/// </summary>
public sealed class FakeSetupWindowService : ISetupWindowService {
    /// <summary>
    /// How many times a wizard was opened.
    /// </summary>
    public int ShowCount { get; private set; }

    /// <inheritdoc />
    public bool IsOpen { get; private set; }

    /// <inheritdoc />
    public Window? CurrentWindow => null;

    /// <inheritdoc />
    public event EventHandler? Closed;

    /// <inheritdoc />
    public void Show() {
        ShowCount++;
        IsOpen = true;
    }

    /// <summary>
    /// Closes the window that was never shown — the test's hand on what
    /// would otherwise be the title bar's X, or the wizard's own Finish.
    /// </summary>
    public void Close() {
        IsOpen = false;
        Closed?.Invoke(this, EventArgs.Empty);
    }
}
