using Shiranuix.Models;
using Shiranuix.ViewModels;

namespace Shiranuix.Services;

/// <summary>
/// One thing the editor itself can be asked to do: its name, its words, and
/// the work behind it.
/// </summary>
/// <param name="Id">The command's id, in the naming shape the chords use.</param>
/// <param name="Label">What a writer reads in the palette.</param>
/// <param name="Description">What it does, in a line.</param>
/// <param name="Run">The work, told which editor it is being asked of.</param>
/// <remarks>
/// This is the table the palette reads, so a writer types one thing and is
/// told what it answers rather than having to know where a command sits. A
/// command's chord, when it has one, is not written here: the chords
/// live in <see cref="KeyCommands"/> and are looked up by id, so the same
/// table cannot disagree with the keyboard about what is bound.
/// </remarks>
public sealed record EditorCommand(
    string Id,
    string Label,
    string Description,
    Action<EditorViewModel> Run) {
    /// <summary>The chord this command starts on, or null when it starts on
    /// nothing.</summary>
    public string? Gesture => KeyCommands.Find(Id)?.DefaultGesture;

    /// <summary>Whether this command answers to a chord at all.</summary>
    public bool HasGesture => !string.IsNullOrWhiteSpace(Gesture);
}

/// <summary>
/// The editor's own commands, in the order the palette lists them.
/// </summary>
/// <remarks>
/// A list rather than a dictionary, because a palette that cannot show what
/// it holds is a palette a writer has to guess at: the order here is the order
/// they read down the box, the way a menu reads. The keyboard reaches the same
/// commands by id, so this list and the routing in
/// <see cref="Views.CommandKeys"/> are two readings of one table - the
/// keyboard looks a command up, the palette lists it.
///
/// The folder is no different: the tree of files is the editor's own, so its
/// commands are here like every other command's - one table, and the palette
/// lists it.
/// </remarks>
public static class EditorCommands {
    /// <summary>Every command the editor answers to by name.</summary>
    public static IReadOnlyList<EditorCommand> All { get; } = [
        new(
            KeyCommands.New.Id, "New document",
            "Starts an empty page, asking about unsaved work first.",
            vm => vm.NewDocumentCommand.Execute(null)),

        new(
            KeyCommands.Open.Id, "Open file",
            "Opens a document from disk.",
            vm => vm.OpenCommand.Execute(null)),

        new(
            KeyCommands.Save.Id, "Save",
            "Writes the page to the file it came from.",
            vm => vm.SaveCommand.Execute(null)),

        new(
            KeyCommands.SaveAs.Id, "Save as",
            "Writes the page wherever the writer points.",
            vm => vm.SaveAsCommand.Execute(null)),

        new(
            KeyCommands.Undo.Id, "Undo",
            "Takes the last move back.",
            vm => vm.Undo()),

        new(
            KeyCommands.Redo.Id, "Redo",
            "Puts a taken-back move back.",
            vm => vm.Redo()),

        new(
            KeyCommands.Search.Id, "Find",
            "Finds a piece of text through the page.",
            vm => vm.ToggleFindCommand.Execute(null)),

        new(
            KeyCommands.ToggleWordWrap.Id, "Toggle word wrap",
            "Wraps the lines within the sheet, or lets them run on.",
            vm => vm.ToggleWordWrapCommand.Execute(null)),

        new(
            "file-tree.open-folder", "Open folder",
            "Asks for a folder and stands the tree on it.",
            vm => _ = vm.Tree!.ChooseFolderAsync(CancellationToken.None)),

        new(
            "file-tree.refresh", "Refresh tree",
            "Reads the folder again, keeping what is open.",
            vm => _ = vm.Tree!.Tree.RefreshAsync(CancellationToken.None)),

        new(
            KeyCommands.ToggleSidebar.Id, "Toggle sidebar",
            "Opens and closes the column down the left of the page.",
            vm => vm.Panel.Toggle()),

        // The box itself is a command, and the only one whose chord answers
        // for every other command: one chord, and every command is two keys
        // away from being run.
        new(
            KeyCommands.Palette.Id, "Command palette",
            "Types a command's name and runs it.",
            vm => vm.Palette.Toggle())
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
