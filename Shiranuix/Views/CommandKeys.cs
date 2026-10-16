using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Shiranuix.Models;
using Shiranuix.ViewModels;

namespace Shiranuix.Views;

/// <summary>
/// Runs the commands the window's chords answer: the flows behind the
/// toolbar's buttons, the status bar's word-wrap toggle, and the
/// document's history moves.
/// </summary>
/// <remarks>
/// Which chord answers which command is <see cref="KeyBindingState"/>'s
/// question, so this class holds no keyboard opinions of its own — beyond
/// how each command's name maps onto the view model, which is here
/// because the view is where the window's controls live.
///
/// The routes answer a press whether or not the editor box took it first
/// — the same reason <see cref="EditorKeys"/> listens with
/// <c>handledEventsToo</c> — and mark the press handled once a route has
/// answered it, so nothing the route stood for gets it twice. A chord no
/// route answers, or a press with no chord behind it, goes on exactly as
/// though this had not listened.
/// </remarks>
public static class CommandKeys {
    /// <summary>
    /// The command each route answers, by the names
    /// <see cref="KeyCommands"/> gives them.
    /// </summary>
    private static readonly Dictionary<string, Action<EditorViewModel>> Routes = new() {
        [KeyCommands.New.Id] = vm => vm.NewDocumentCommand.Execute(null),
        [KeyCommands.Open.Id] = vm => vm.OpenCommand.Execute(null),
        [KeyCommands.Save.Id] = vm => vm.SaveCommand.Execute(null),
        [KeyCommands.SaveAs.Id] = vm => vm.SaveAsCommand.Execute(null),
        [KeyCommands.Undo.Id] = vm => vm.Undo(),
        [KeyCommands.Redo.Id] = vm => vm.Redo(),
        [KeyCommands.Search.Id] = vm => vm.ToggleFindCommand.Execute(null),
        [KeyCommands.ToggleWordWrap.Id] = vm => vm.ToggleWordWrapCommand.Execute(null),
        [KeyCommands.ToggleSidebar.Id] = vm => vm.Panel.Toggle()
    };

    /// <summary>
    /// Answers the window's chord-presses with the commands they are
    /// bound to.
    /// </summary>
    /// <param name="window">The window whose presses are answered.</param>
    /// <param name="viewModel">Reads the view model the routes answer, or
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

            if (Routes.TryGetValue(command, out var route)) {
                route(vm);
                e.Handled = true;
            }
        }
    }
}
