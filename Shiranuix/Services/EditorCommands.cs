using Shiranuix.Models;
using Shiranuix.ViewModels;

namespace Shiranuix.Services;

/// <summary>
/// One thing the editor itself can be asked to do: its name, and the work
/// behind it.
/// </summary>
/// <param name="Id">The command's id, in the naming shape the chords use.</param>
/// <param name="Run">The work, told which editor it is being asked of.</param>
/// <remarks>
/// The id is where the routing meets the keyboard: the chords are decided in
/// <see cref="KeyCommands"/> and looked up by id, so the work and the chord
/// cannot disagree about which command a key carries.
/// </remarks>
public sealed record EditorCommand(string Id, Action<EditorViewModel> Run);

/// <summary>
/// The editor's own commands, in the order the keyboard's settings list them.
/// </summary>
/// <remarks>
/// Which chord answers which command is <see cref="Utilities.KeyBindingState"/>'s
/// question; this says what a command does once its id has been named, and
/// nothing about when that might be. A command a key can reach is one command
/// in one table, not a work and a chord written in two places that could
/// drift apart.
/// </remarks>
public static class EditorCommands {
    /// <summary>Every command the editor answers to by name.</summary>
    public static IReadOnlyList<EditorCommand> All { get; } = [
        new(
            KeyCommands.New.Id,
            vm => vm.NewDocumentCommand.Execute(null)),

        new(
            KeyCommands.Open.Id,
            vm => vm.OpenCommand.Execute(null)),

        new(
            KeyCommands.Save.Id,
            vm => vm.SaveCommand.Execute(null)),

        new(
            KeyCommands.SaveAs.Id,
            vm => vm.SaveAsCommand.Execute(null)),

        new(
            KeyCommands.ExportPdf.Id,
            vm => _ = vm.ExportPdfCommand.ExecuteAsync(null)),

        new(
            KeyCommands.Undo.Id,
            vm => vm.Undo()),

        new(
            KeyCommands.Redo.Id,
            vm => vm.Redo()),

        new(
            KeyCommands.Search.Id,
            vm => vm.ToggleFindCommand.Execute(null)),

        new(
            KeyCommands.ToggleWordWrap.Id,
            vm => vm.ToggleWordWrapCommand.Execute(null)),

        new(
            KeyCommands.ToggleSidebar.Id,
            vm => vm.Panel.Toggle())
    ];

    /// <summary>
    /// Runs the command an id names, when the editor has one by that id.
    /// </summary>
    /// <param name="id">The command's id.</param>
    /// <param name="editor">The editor to run it against.</param>
    /// <returns>True when a command answered; false when none is named that.</returns>
    public static bool TryRun(string id, EditorViewModel editor) {
        ArgumentNullException.ThrowIfNull(editor);

        foreach (var command in All) {
            if (string.Equals(command.Id, id, StringComparison.Ordinal)) {
                command.Run(editor);
                return true;
            }
        }

        return false;
    }
}
