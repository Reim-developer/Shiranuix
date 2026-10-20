using Shiranuix.Models;

namespace Shiranuix.Persistence;

/// <summary>
/// The keyboard shortcuts the user has moved off their defaults.
/// </summary>
public interface IKeyBindingsStore {
    /// <summary>
    /// Every stored override, in whatever order they were written.
    /// </summary>
    IReadOnlyList<KeyBinding> LoadAll();

    /// <summary>
    /// Writes the given bindings as the whole set, replacing whatever was
    /// there. The state has already decided what the table should say —
    /// the store's only job is to make it say exactly that.
    /// </summary>
    /// <param name="bindings">The overrides to keep, at most one per command.</param>
    void ReplaceAll(IReadOnlyList<KeyBinding> bindings);
}
