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
public sealed record DocumentOutcome(bool Succeeded, string? Detail) {
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
    /// Whether the attempt was called off rather than failed — nothing
    /// went wrong, and nothing took place.
    /// </summary>
    public bool WasCalledOff => !Succeeded && Detail is null;
}
