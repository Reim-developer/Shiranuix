namespace Shiranuix.Models;

/// <summary>
/// A window over a file, worked out but not yet on screen: where it begins,
/// the line it begins on, the file's own text across it, and whether the file
/// carries on past it.
/// </summary>
/// <remarks>
/// The window engine plans, the session lays the plan on screen — keeping the
/// two apart means every walk, snap and read is done in one place while the
/// text, the caret and the ledger's window stay in the one that owns them.
/// All coordinates are the file's own, the ones the ledger keeps its moves in.
/// </remarks>
/// <param name="Start">Where the window begins in the file.</param>
/// <param name="FileLine">The file line it begins on, from zero.</param>
/// <param name="Original">The file's own text over the window, before any
/// move is laid over it.</param>
/// <param name="AtEnd">Whether the file carries on past the window.</param>
public readonly record struct WindowPlan(
    LineAnchor Start,
    int FileLine,
    string Original,
    bool AtEnd);
