namespace Shiranuix.Models;

/// <summary>
/// One keyboard shortcut the user has moved: the command it answers for,
/// and the keys it answers to.
/// </summary>
/// <remarks>
/// Overrides only. The defaults live in code, in <see cref="KeyCommands"/>;
/// a row here exists only when a binding has been moved off its default —
/// so a command added later arrives with its default already working, and
/// "reset to defaults" is the whole set emptied rather than a negotiation
/// with every row in it. The gesture is stored in the shape it is read in
/// — "Ctrl+Shift+S" — so the file stays legible by hand, which is the
/// whole format this application edits.
/// </remarks>
public sealed class KeyBinding {
    /// <summary>
    /// The command this binding answers for: its id from
    /// <see cref="KeyCommands"/>. The row's primary key — a command
    /// answers to one gesture, so a second row for it would be a second
    /// answer, and there is only ever one.
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// The keys it answers to, in canonical form: "Ctrl", "Alt" and
    /// "Shift" in that order, then the key — "Ctrl+Shift+S".
    /// </summary>
    public string Gesture { get; set; } = string.Empty;
}
