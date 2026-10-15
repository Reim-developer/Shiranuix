namespace Shiranuix.Models;

/// <summary>
/// The commands a keyboard shortcut may be moved between: the seven the
/// window answers to, with the chords they start on.
/// </summary>
/// <remarks>
/// The defaults live here rather than in the store, so a command added
/// later arrives already working and the table only ever holds the
/// disagreements — the chords the writer has moved. The state that reads
/// them throws if one has drifted out of <see cref="Utilities.KeyGestures"/>'s
/// shape: a bad default is a mistake made by hand in this file, not
/// something to quietly unbind.
///
/// Navigation — the page keys and the whole-file home and end — is not in
/// this list. Those move the caret rather than run a command, and a
/// caret that moved differently for each writer would be a surprise
/// rather than a setting.
/// </remarks>
public static class KeyCommands {
    /// <summary>A fresh document.</summary>
    public static readonly KeyCommand New = new(nameof(New), "New document", "Ctrl+N");

    /// <summary>The open-a-file flow.</summary>
    public static readonly KeyCommand Open = new(nameof(Open), "Open file", "Ctrl+O");

    /// <summary>Writing the document to the file it came from.</summary>
    public static readonly KeyCommand Save = new(nameof(Save), "Save", "Ctrl+S");

    /// <summary>Writing it wherever the writer points.</summary>
    public static readonly KeyCommand SaveAs = new(nameof(SaveAs), "Save as", "Ctrl+Shift+S");

    /// <summary>Taking the last move back.</summary>
    public static readonly KeyCommand Undo = new(nameof(Undo), "Undo", "Ctrl+Z");

    /// <summary>Putting a taken-back move back.</summary>
    public static readonly KeyCommand Redo = new(nameof(Redo), "Redo", "Ctrl+Y");

    /// <summary>Wrapping the lines within the sheet, or letting them run on.</summary>
    public static readonly KeyCommand ToggleWordWrap =
        new(nameof(ToggleWordWrap), "Toggle word wrap", "Alt+Z");

    /// <summary>The seven, in the order the settings list them.</summary>
    public static IReadOnlyList<KeyCommand> All { get; } = new KeyCommand[] {
        New, Open, Save, SaveAs, Undo, Redo, ToggleWordWrap
    };

    /// <summary>
    /// Finds a command by its id — the store's rows and the settings' rows
    /// both reach for it by name.
    /// </summary>
    /// <param name="id">The id to find, or null.</param>
    /// <returns>The command, or null when the id is not one of the seven.</returns>
    public static KeyCommand? Find(string? id) =>
        id is null ? null : All.FirstOrDefault(command => command.Id == id);
}
