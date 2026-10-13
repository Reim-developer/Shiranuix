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
}
