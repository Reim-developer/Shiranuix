using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Shiranuix.Models;
using Shiranuix.Services;
using Shiranuix.ViewModels;

namespace Shiranuix.Views;

/// <summary>
/// Runs the commands the window's chords answer: the flows behind the
/// toolbar's buttons, the status bar's word-wrap toggle, and the
/// document's history moves.
/// </summary>
/// <remarks>
/// Which chord answers which command is <see cref="KeyBindingState"/>'s
/// question, so this class holds no keyboard opinions of its own — and no
/// table of its own either: the commands live in
/// <see cref="EditorCommands"/>, so the routing and the work are one table
/// rather than two lists that could drift apart.
/// </remarks>
public static class CommandKeys {

    /// <summary>
    /// Answers the window's chord-presses with the commands they are
    /// bound to.
    /// </summary>
    /// <remarks>
    /// A press is answered whether or not the editor box took it first — the
    /// same reason <see cref="EditorKeys"/> listens with
    /// <c>handledEventsToo</c> — and is marked handled once a command has
    /// answered it, so nothing the command stood for gets it twice. A chord
    /// nothing answers, or a press with no chord behind it, goes on exactly
    /// as though this had not listened.
    /// </remarks>
    /// <param name="window">The window whose presses are answered.</param>
    /// <param name="viewModel">Reads the view model the commands answer, or
    /// null while the window holds none.</param>
    /// <param name="bindings">What each command answers to.</param>
    public static void Watch(Window window, Func<EditorViewModel?> viewModel, KeyBindingState bindings) {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(bindings);

        window.AddHandler(
            InputElement.KeyDownEvent,
            OnKeyDown,
            RoutingStrategies.Bubble,
            handledEventsToo: true);

        void OnKeyDown(object? sender, KeyEventArgs e) {
            if (viewModel() is not { } vm) {
                return;
            }

            if (bindings.CommandFor(e.Key, e.KeyModifiers) is not { } command) {
                return;
            }

            if (EditorCommands.TryRun(command, vm)) {
                e.Handled = true;
            }
        }
    }
}
