namespace Shiranuix.ViewModels;

/// <summary>
/// What came of an attempt on a document: either it worked, or it did not and
/// here is why.
/// </summary>
/// <remarks>
/// The session returns these rather than sentences, so the words a user reads
/// are decided where the window's messages are decided. An outcome is a fact;
/// "Could not open: access denied" is a rendering of one.
/// </remarks>
/// <param name="Succeeded">Whether the attempt worked.</param>
/// <param name="Detail">Why it did not, for whoever renders it to a user.</param>
/// <param name="IsUnreadable">
/// Whether the file itself could not be read — damaged, or a shape from
/// further ahead — rather than being absent, refused or out of reach. A
/// rendering has to tell this apart: it is the one failure where nothing the
/// writer did explains it and nothing they retry will mend it.
/// </param>
public sealed record DocumentOutcome(bool Succeeded, string? Detail, bool IsUnreadable = false) {
    /// <summary>The attempt worked.</summary>
    public static DocumentOutcome Done => new(true, null);

    /// <summary>The attempt failed.</summary>
    /// <param name="detail">Why it failed, for the status bar to render.</param>
    /// <returns>The outcome.</returns>
    public static DocumentOutcome Failed(string detail) => new(false, detail);

    /// <summary>
    /// The attempt was called off by whoever was asked about it: nothing
    /// went wrong, and nothing happened either.
    /// </summary>
    /// <remarks>
    /// There is a real difference between a file that could not be read and
    /// one somebody decided not to open — a dialog answered with nothing is
    /// the second — and the words a user reads have to tell them apart.
    /// </remarks>
    public static DocumentOutcome CalledOff => new(false, null);

    /// <summary>
    /// The file was there and could not be read: the bytes behind it are not
    /// the shape this app knows how to open.
    /// </summary>
    /// <param name="detail">Why it could not be read, for whoever renders it.</param>
    /// <returns>The outcome.</returns>
    /// <remarks>
    /// Kept apart from an ordinary failure because it is not the writer's
    /// doing and no retry will mend it: the file is damaged, or it is a
    /// shape from further ahead than this build. That deserves more than a
    /// line at the bottom of the window, and it deserves being told apart
    /// from a file that simply is not there.
    /// </remarks>
    public static DocumentOutcome Unreadable(string detail) => new(false, detail, true);

    /// <summary>
    /// Whether the attempt was called off rather than failed — nothing
    /// went wrong, and nothing took place.
    /// </summary>
    public bool WasCalledOff => !Succeeded && Detail is null;
}
