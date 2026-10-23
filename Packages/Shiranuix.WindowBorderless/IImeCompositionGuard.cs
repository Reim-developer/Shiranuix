namespace Shiranuix.WindowBorderless;

/// <summary>
/// Asks the input method to commit a composition that has grown too long for
/// it to keep taking keys.
/// </summary>
/// <remarks>
/// The Windows Vietnamese input method lets a composition grow to twenty-nine
/// characters and then freezes on it: every later key is swallowed until
/// something outside ends the composition — in any application, on any
/// framework. This service is that outside. The view hears each composition
/// as it is drawn and asks here whether committing it now would save the
/// writer's next keystroke.
/// </remarks>
public interface IImeCompositionGuard {
    /// <summary>
    /// Whether a composing text has grown long enough that the input method
    /// must be asked to commit it now.
    /// </summary>
    /// <param name="composition">The composing text, or nothing.</param>
    /// <returns>True when the composition should be committed.</returns>
    bool ShouldComplete(string? composition);

    /// <summary>
    /// Commits whatever the input method is composing in the given window.
    /// </summary>
    /// <param name="window">The window whose input context to complete.</param>
    void Complete(IntPtr window);
}
