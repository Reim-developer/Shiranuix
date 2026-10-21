using Shiranuix.Models;
using Shiranuix.Persistence;

namespace Shiranuix.Tests.Fakes;

/// <summary>
/// An in-memory keybinding store, so tests never touch the database. The
/// moves a state is built over are seeded; every write is kept whole.
/// </summary>
public sealed class FakeKeyBindingsStore : IKeyBindingsStore {
    /// <summary>
    /// What <see cref="LoadAll"/> answers with — the moves a test wants the
    /// writer to have already made.
    /// </summary>
    public List<KeyBinding> Seeded { get; } = new();

    /// <summary>
    /// The last set <see cref="ReplaceAll"/> was handed, as written: the
    /// test reads it to see what the state considers a disagreement with
    /// the defaults.
    /// </summary>
    public IReadOnlyList<KeyBinding>? Written { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<KeyBinding> LoadAll() => Seeded;

    /// <inheritdoc />
    public void ReplaceAll(IReadOnlyList<KeyBinding> bindings) => Written = bindings.ToList();
}
