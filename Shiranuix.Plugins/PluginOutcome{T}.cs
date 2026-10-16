namespace Shiranuix.Plugins;

/// <summary>
/// How a call that hands something back ended: it did the thing and produced
/// the thing, or it did not.
/// </summary>
/// <typeparam name="T">What a successful call produces.</typeparam>
/// <remarks>
/// The same promise as <see cref="PluginOutcome"/>, with a value riding along
/// when it worked. Asking for the value of a call that failed throws rather
/// than handing back a default and leaving the plugin to wonder why — the
/// failure is a programming mistake, not a condition to branch on.
/// <see cref="TryGetValue"/> is there for the cases where it is a condition.
/// </remarks>
public readonly record struct PluginOutcome<T> {
    private readonly T? _value;

    private PluginOutcome(T? value, string? error) {
        _value = value;
        Error = error;
    }

    /// <summary>Whether the call did what it was asked.</summary>
    public bool IsSuccess => Error is null;

    /// <summary>
    /// Why it did not, in a sentence a person can read; null when it did.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// What the call produced.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The call did not succeed, so there is nothing to hand back.
    /// </exception>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException(
            $"The call did not succeed, so it produced nothing: {Error}");

    /// <summary>
    /// Takes the value when there is one, without throwing when there is not.
    /// </summary>
    /// <param name="value">
    /// What the call produced, or the type's default when it produced nothing.
    /// </param>
    /// <returns>Whether the call succeeded.</returns>
    public bool TryGetValue(out T value) {
        value = _value!;
        return IsSuccess;
    }

    /// <summary>A call that did what it was asked and produced something.</summary>
    /// <param name="value">What it produced.</param>
    /// <returns>The outcome that says so.</returns>
    public static PluginOutcome<T> From(T value) => new(value, null);

    /// <summary>A call that could not be done.</summary>
    /// <param name="error">Why, in a sentence worth showing a writer.</param>
    /// <returns>The outcome that says so.</returns>
    /// <exception cref="ArgumentException">The sentence is empty.</exception>
    public static PluginOutcome<T> Failure(string error) {
        if (string.IsNullOrWhiteSpace(error)) {
            throw new ArgumentException("A refusal has to say something.", nameof(error));
        }

        return new PluginOutcome<T>(default, error);
    }
}
