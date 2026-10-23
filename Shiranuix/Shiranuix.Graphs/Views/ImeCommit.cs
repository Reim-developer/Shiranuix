using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shiranuix.WindowBorderless;

namespace Shiranuix.Graphs.Views;

/// <summary>
/// The words an input method is holding on the writer's behalf, and the
/// promise that they survive a change of attention.
/// </summary>
/// <remarks>
/// A Vietnamese IME — and every CJK one — keeps the syllable being typed in
/// a composition of its own: drawn in the box, underlined, but not yet the
/// box's text and nowhere near the view model. The moment the focus walks
/// away, Avalonia asks the IME to finish that composition and then, quite
/// deliberately, swallows the result (Imm32InputMethod.Reset announces the
/// completion and sets the flag that ignores it); whatever the writer typed
/// stands in neither box nor view model, which is why names typed with an
/// IME on vanished the moment Tab was pressed. So the words are harvested
/// first: read straight out of the IMM context while the box is still the
/// focused one, pressed into its text where the caret was keeping their
/// place, and the composition is cancelled so the coming discard finds
/// nothing left to throw away. Without an IME — or with one that has
/// nothing composing — every step of this is a no-op, and the dialogs
/// behave exactly as they always did.
/// </remarks>
internal static class ImeCommit {
    /// <summary>
    /// Wires a dialog so that whatever its input method is still composing is
    /// pressed into the box that was receiving it the moment the writer's
    /// attention moves on — by tab, by enter, or by pointing elsewhere.
    /// </summary>
    /// <param name="window">The dialog whose boxes might be mid-composition.</param>
    /// <param name="freeze">The guard that decides when a composition has
    /// outgrown what the IME can hold, or nothing when the dialog has none.</param>
    public static void Guard(Window window, IImeCompositionGuard? freeze = null) {
        // Tunneling, so the words are safe before any focus change this
        // same gesture goes on to cause.
        window.AddHandler(InputElement.KeyDownEvent, (_, e) => OnLeaving(window, e), RoutingStrategies.Tunnel);
        window.AddHandler(InputElement.PointerPressedEvent, (_, e) => OnLeaving(window, e), RoutingStrategies.Tunnel);

        if (freeze is null) {
            return;
        }

        // A Vietnamese IME stops taking a composition once it outgrows what
        // it can hold, and every key after that point is swallowed until the
        // composition ends — the text freezes where it stands. The very
        // moment the IME says it can hold no more, the composition is
        // committed, exactly as the editor's own composition guard does.
        window.Opened += (_, _) => {
            foreach (var box in window.GetVisualDescendants().OfType<TextBox>()) {
                WatchFreeze(window, box, freeze);
            }
        };
    }

    /// <summary>
    /// Watches one box's preedit — the only place a composition shows itself
    /// before it is committed — and commits it once it is too big to grow.
    /// </summary>
    private static void WatchFreeze(Window window, TextBox box, IImeCompositionGuard freeze) {
        TextPresenter? watched = null;
        EventHandler<AvaloniaPropertyChangedEventArgs>? watching = null;

        box.TemplateApplied += (_, e) => {
            var presenter = e.NameScope.Find<TextPresenter>("PART_TextPresenter");

            if (ReferenceEquals(presenter, watched)) {
                return;
            }

            if (watched is not null && watching is not null) {
                watched.PropertyChanged -= watching;
            }

            watched = presenter;
            watching = null;

            if (presenter is null) {
                return;
            }

            watching = (_, args) => {
                if (args.Property != TextPresenter.PreeditTextProperty) {
                    return;
                }

                if (!freeze.ShouldComplete(presenter.PreeditText)) {
                    return;
                }

                if (window.TryGetPlatformHandle()?.Handle is { } handle && handle != 0) {
                    freeze.Complete(handle);
                }
            };

            presenter.PropertyChanged += watching;
        };
    }

    /// <summary>
    /// Presses the input method's unfinished composition into the box that
    /// was receiving it, if there is one to press.
    /// </summary>
    /// <param name="window">The dialog whose focused box might be composing.</param>
    public static void HarvestPending(Window window) {
        if (!OperatingSystem.IsWindows()) {
            return;
        }

        if (window.TryGetPlatformHandle()?.Handle is not { } rawHandle || rawHandle == 0) {
            return;
        }

        if (window.FocusManager?.GetFocusedElement() is not TextBox box) {
            return;
        }

        var handle = (nint)rawHandle;
        var composed = ReadComposition(handle);

        if (string.IsNullOrEmpty(composed)) {
            return;
        }

        // Where the words stand now. Should the IME deliver them itself on
        // the way out — Avalonia does read a cancelled composition's
        // remains while the box is still focused — the text will have
        // already changed, and there is nothing left to press by hand.
        var before = box.Text ?? string.Empty;

        CancelComposition(handle);

        if ((box.Text ?? string.Empty) != before) {
            return;
        }

        // A posted hand presses them in behind whatever this gesture is
        // still doing, rather than in the middle of it.
        Dispatcher.UIThread.Post(() => {
            if ((box.Text ?? string.Empty) != before) {
                return;
            }

            var caret = Math.Clamp(box.CaretIndex, 0, before.Length);
            box.Text = before.Insert(caret, composed);
            box.CaretIndex = caret + composed.Length;
        });
    }

    /// <summary>Only tab and enter walk the focus on to somewhere else.</summary>
    private static void OnLeaving(Window window, RoutedEventArgs e) {
        if (e is KeyEventArgs key && key.Key is not (Key.Tab or Key.Enter)) {
            return;
        }

        HarvestPending(window);
    }

    /// <summary>Reads the composition string the IME is holding, if any.</summary>
    private static string? ReadComposition(nint window) {
        var context = ImmGetContext(window);

        if (context == 0) {
            return null;
        }

        try {
            var bytes = ImmGetCompositionString(context, CompositionStringFlag, 0, 0);

            if (bytes <= 0) {
                return null;
            }

            var buffer = Marshal.AllocHGlobal(bytes);

            try {
                var read = ImmGetCompositionString(context, CompositionStringFlag, buffer, (uint)bytes);

                return read <= 0 ? null : Marshal.PtrToStringUni(buffer, read / 2);
            } finally {
                Marshal.FreeHGlobal(buffer);
            }
        } finally {
            ImmReleaseContext(window, context);
        }
    }

    /// <summary>
    /// Asks the IME to throw the composition away without producing a
    /// result string — the one ending that never lands in the wrong box.
    /// </summary>
    private static void CancelComposition(nint window) {
        var context = ImmGetContext(window);

        if (context == 0) {
            return;
        }

        try {
            ImmNotifyIME(context, NotifyCompositionString, CancelCompositionFlag, 0);
        } finally {
            ImmReleaseContext(window, context);
        }
    }

    // The IMM32 vocabulary, in the shapes Avalonia's own interop spells it.
    private const uint CompositionStringFlag = 0x0008;
    private const int NotifyCompositionString = 21;
    private const int CancelCompositionFlag = 4;

    [DllImport("imm32.dll")]
    private static extern nint ImmGetContext(nint window);

    [DllImport("imm32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ImmReleaseContext(nint window, nint context);

    [DllImport("imm32.dll", EntryPoint = "ImmGetCompositionStringW", ExactSpelling = true)]
    private static extern int ImmGetCompositionString(nint context, uint index, nint buffer, uint length);

    [DllImport("imm32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ImmNotifyIME(nint context, int action, int index, int value);
}
