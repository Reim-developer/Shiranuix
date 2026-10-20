using Microsoft.EntityFrameworkCore;
using Shiranuix.Models;

namespace Shiranuix.Persistence;

/// <summary>
/// The typeface and size history, kept one row per family/size pairing.
/// </summary>
/// <remarks>
/// Every operation builds its own context and disposes it, so nothing here
/// holds state between calls.
/// </remarks>
public sealed class SqliteFontHistoryStore : IFontHistoryStore {
    /// <summary>
    /// How many recent families and sizes the pickers are offered. Enough to be
    /// useful, few enough that the rest of the list still reads as the rest of
    /// the list.
    /// </summary>
    private const int HistoryDepth = 8;

    private readonly IDbContextFactory<ShiranuixContext> _factory;

    private DateTime _lastStamp;

    /// <summary>
    /// Creates a store over the given context factory.
    /// </summary>
    /// <param name="factory">The factory contexts are built from.</param>
    public SqliteFontHistoryStore(IDbContextFactory<ShiranuixContext> factory) {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <inheritdoc />
    public IReadOnlyList<string> RecentFamilies() {
        using var context = _factory.CreateDbContext();

        // Ordered in the database and made distinct in memory. The repeats are
        // dropped from rows that are already in the right order, and there are
        // only ever a few dozen of them to read.
        return context.FontUses
            .OrderByDescending(use => use.UsedAtUtc)
            .Select(use => use.FontFamily)
            .AsEnumerable()
            .Distinct()
            .Take(HistoryDepth)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<double> RecentSizes() {
        using var context = _factory.CreateDbContext();

        return context.FontUses
            .OrderByDescending(use => use.UsedAtUtc)
            .Select(use => use.FontSize)
            .AsEnumerable()
            .Distinct()
            .Take(HistoryDepth)
            .ToList();
    }

    /// <inheritdoc />
    public void Remember(string family, double size) {
        ArgumentNullException.ThrowIfNull(family);

        using var context = _factory.CreateDbContext();
        var now = NextUtc();

        // One row per pairing, so the same typeface at the same size moves to
        // the front of the history instead of piling up duplicates.
        // AsTracking because the factory builds contexts with NoTracking: a
        // detached row's edits would never reach SaveChanges, so reusing a
        // font would leave its timestamp where it was.
        var stored = context.FontUses.AsTracking().FirstOrDefault(use =>
            use.FontFamily == family && use.FontSize == size);

        if (stored is null) {
            context.FontUses.Add(new FontUse {
                FontFamily = family,
                FontSize = size,
                UsedAtUtc = now
            });
        } else {
            stored.UsedAtUtc = now;
        }

        context.SaveChanges();
    }

    /// <summary>
    /// A timestamp at least one tick after the last one handed out, for the
    /// same reason as in <see cref="SqliteRecentFilesStore"/>: picks made
    /// inside one clock tick must still come back in the order they happened.
    /// </summary>
    /// <returns>A UTC timestamp strictly later than the previous one.</returns>
    private DateTime NextUtc() {
        var now = DateTime.UtcNow;
        _lastStamp = now > _lastStamp ? now : _lastStamp.AddTicks(1);
        return _lastStamp;
    }
}
