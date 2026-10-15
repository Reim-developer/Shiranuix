using Microsoft.EntityFrameworkCore;
using Shiranuix.Models;

namespace Shiranuix.Persistence;

/// <summary>
/// Keybinding overrides kept in the store.
/// </summary>
/// <remarks>
/// Every operation builds its own context and disposes it, like every
/// other store here. The set is replaced rather than reconciled: it is
/// seven rows at most, and a reconciliation that kept a row the state had
/// already dropped would be a binding the user removed, back from the
/// dead on the next launch.
/// </remarks>
public sealed class SqliteKeyBindings : IKeyBindingsStore {
    private readonly IDbContextFactory<ShiranuixContext> _factory;

    /// <summary>
    /// Creates a store over the given context factory.
    /// </summary>
    /// <param name="factory">The factory contexts are built from.</param>
    public SqliteKeyBindings(IDbContextFactory<ShiranuixContext> factory) {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <inheritdoc />
    public IReadOnlyList<KeyBinding> LoadAll() {
        using var context = _factory.CreateDbContext();
        return context.KeyBindings.AsNoTracking().ToList();
    }

    /// <inheritdoc />
    public void ReplaceAll(IReadOnlyList<KeyBinding> bindings) {
        ArgumentNullException.ThrowIfNull(bindings);

        using var context = _factory.CreateDbContext();

        // One statement rather than a tracked removal per row: the rows
        // are going, not being examined, and the table is small enough
        // that either way is a rounding error.
        context.KeyBindings.ExecuteDelete();
        context.KeyBindings.AddRange(bindings);
        context.SaveChanges();
    }
}
