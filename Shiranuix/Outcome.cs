namespace Shiranuix;

/// <summary>
/// How a call to the editor ended: it did the thing, or it did not and can say
/// why.
/// </summary>
/// <remarks>
/// A value, because a refusal is a fact rather than an exception: a caller
/// asking to replace a range past the end of the document has asked something
/// impossible, and the answer is "that range is not in this document" rather
/// than a stack trace. Exceptions stay for the genuinely unexpected.
/// </remarks>
public readonly record struct Outcome {
    private Outcome(string? error) => Error = error;

    /// <summary>Whether the call did what it was asked.</summary>
    public bool IsSuccess => Error is null;

    /// <summary>
    /// Why it did not, in a sentence a person can read; null when it did.
    /// </summary>
    public string? Error { get; }

    /// <summary>A call that did what it was asked.</summary>
    /// <returns>The outcome that says so.</returns>
    public static Outcome Ok() => new(null);

    /// <summary>A call that could not be done.</summary>
    /// <param name="error">Why, in a sentence worth showing a writer.</param>
    /// <returns>The outcome that says so.</returns>
    /// <exception cref="ArgumentException">The sentence is empty.</exception>
    public static Outcome Failure(string error) {
        if (string.IsNullOrWhiteSpace(error)) {
            throw new ArgumentException("A refusal has to say something.", nameof(error));
        }

        return new Outcome(error);
    }
}
