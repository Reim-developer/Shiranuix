using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Shiranuix.Services;

/// <summary>
/// Commits a composition before the Windows Vietnamese input method freezes
/// on it.
/// </summary>
/// <remarks>
/// The input method stops extending a composition at twenty-nine characters
/// and swallows every following key until the composition ends — no setting
/// turns that off, and no framework can reach past it. Ending the composition
/// first is the only move: at <see cref="CommitLength"/> characters the guard
/// completes it the same way the input method completes one itself, the text
/// lands at the caret, and the next key starts a fresh composition. Vietnamese
/// words are far shorter than the ceiling, so ordinary typing never sees the
/// guard act at all.
///
/// The completion is a notification, not a write: the input method does the
/// committing and reports it back through the editor's own path, exactly as
/// it does for a press of Alt or a space.
/// </remarks>
public sealed class ImeCompositionGuard : IImeCompositionGuard {
    /// <summary>
    /// The composing length that triggers a commit — two characters under the
    /// input method's own ceiling, so a keystroke racing the commit cannot be
    /// the one that reaches the freeze.
    /// </summary>
    public const int CommitLength = 28;

    /// <inheritdoc/>
    public bool ShouldComplete(string? composition) => composition?.Length >= CommitLength;

    /// <inheritdoc/>
    public void Complete(IntPtr window) {
        // The input method lives behind imm32, which only Windows carries:
        // elsewhere the completion is simply not asked for, and the guard
        // stands down without reaching for a library that is not there.
        if (!OperatingSystem.IsWindows() || window == IntPtr.Zero) {
            return;
        }

        var context = ImmGetContext(window);

        if (context == IntPtr.Zero) {
            return;
        }

        try {
            ImmNotifyIME(context, NiCompositionStr, CpsComplete, 0);
        } finally {
            ImmReleaseContext(window, context);
        }
    }

    // The action and flag that tell an input method to finish the
    // composition it is holding — the pair its own reset path uses.
    private const int NiCompositionStr = 21;
    private const int CpsComplete = 1;

    [SupportedOSPlatform("windows")]
    [DllImport("imm32.dll")]
    private static extern IntPtr ImmGetContext(IntPtr window);

    [SupportedOSPlatform("windows")]
    [DllImport("imm32.dll")]
    private static extern bool ImmReleaseContext(IntPtr window, IntPtr context);

    [SupportedOSPlatform("windows")]
    [DllImport("imm32.dll")]
    private static extern bool ImmNotifyIME(IntPtr context, int action, int index, int value);
}
