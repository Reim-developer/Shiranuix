namespace Shiranuix.CoreEditor;

/// <summary>
/// A value tracked against the last point it was saved at, so "has this changed
/// since?" is answerable without keeping a second copy of the value.
/// </summary>
/// <remarks>
/// The view model holds the document text this way. The text itself is one
/// thing; whether it differs from the last thing read or written is another;
/// and keeping the two in step by hand is how a document ends up claiming it
/// is saved when it is not.
///
/// The tracker knows nothing about how the value is stored or displayed — it
/// only raises <see cref="Changed"/> when the value moves, and leaves the
/// broadcasting of each property to whoever owns it.
/// </remarks>
public sealed class DirtyTracker<T>
    where T : IEquatable<T> {
    private T _value;
    private T _saved;

    /// <summary>
    /// Creates a tracker over <paramref name="value"/>, which starts clean.
    /// </summary>
    /// <param name="value">The initial value.</param>
    public DirtyTracker(T value) {
        _value = value;
        _saved = value;
    }

    /// <summary>
    /// The current value.
    /// </summary>
    /// <remarks>
    /// Setting it raises <see cref="Changed"/>; <see cref="IsDirty"/> is
    /// recomputed from it, so the two can never drift apart.
    /// </remarks>
    public T Value {
        get => _value;
        set {
            _value = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// True when the value differs from the last point it was saved at.
    /// </summary>
    public bool IsDirty => !_value.Equals(_saved);

    /// <summary>
    /// Marks the current value as the saved one, clearing
    /// <see cref="IsDirty"/>.
    /// </summary>
    public void MarkSaved() {
        _saved = Value;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Sets the value and the saved point together, so the two agree.
    /// </summary>
    /// <param name="value">The new value.</param>
    /// <remarks>
    /// Used where a change is not an edit — a new document, say — and so
    /// should not leave the tracker dirty.
    /// </remarks>
    public void Reset(T value) {
        _value = value;
        _saved = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Raised whenever the value or the saved point moves.
    /// </summary>
    public event EventHandler? Changed;
}
