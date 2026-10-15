namespace Shiranuix.Models;

/// <summary>
/// One command the writer may move a keyboard shortcut to: the id that
/// storage and routing agree on, what the settings call it, and the chord
/// it starts on.
/// </summary>
/// <remarks>
/// A record because it never changes once named: the catalogue in
/// <see cref="KeyCommands"/> owns the seven of them, and both the
/// settings' rows and the store's rows speak in <c>Id</c>.
/// </remarks>
/// <param name="Id">The command's name, in storage and in routing.</param>
/// <param name="Label">What a person reads in the settings window.</param>
/// <param name="DefaultGesture">
/// The chord it starts on, written the way <see cref="Utilities.KeyGestures"/>
/// reads it — or null when it starts unassigned.
/// </param>
public sealed record KeyCommand(string Id, string Label, string? DefaultGesture);
