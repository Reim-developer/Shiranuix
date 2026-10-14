using Microsoft.EntityFrameworkCore;
using Shiranuix.Models;

namespace Shiranuix.Persistence;

/// <summary>
/// The recent-files list, kept newest first.
/// </summary>
/// <remarks>
/// Every operation builds its own context and disposes it, so nothing here
/// holds state between calls.
/// </remarks>
public sealed class SqliteRecentFilesStore : IRecentFilesStore {
    /// <summary>
    /// How many paths are kept. Matches what the start screen has room for,
    /// so the table cannot outgrow the list it feeds.
    /// </summary>
    private const int Limit = 10;

    private readonly IDbContextFactory<ShiranuixContext> _factory;

    private DateTime _lastStamp;

    /// <summary>
    /// Creates a store over the given context factory.
    /// </summary>
    /// <param name="factory">The factory contexts are built from.</param>
    public SqliteRecentFilesStore(IDbContextFactory<ShiranuixContext> factory) {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Load() {
        using var context = _factory.CreateDbContext();

        return context.RecentFiles
            .OrderByDescending(file => file.LastOpenedUtc)
            .Select(file => file.Path)
            .ToList();
    }

    /// <inheritdoc />
    public void Remember(string path) {
        ArgumentNullException.ThrowIfNull(path);

        using var context = _factory.CreateDbContext();
        var now = NextUtc();

        // One row per path: a file opened twice has its timestamp moved
        // forward rather than a second entry competing for the same slot.
        var stored = context.RecentFiles.FirstOrDefault(file => file.Path == path);
        if (stored is null) {
            context.RecentFiles.Add(new RecentFile {
                Path = path,
                LastOpenedUtc = now
            });
        } else {
            stored.LastOpenedUtc = now;
        }

        context.SaveChanges();

        // Drop the oldest opens past the limit. Done here rather than in the
        // caller so the cap is the store's own invariant and survives however
        // many times the app is launched.
        var excess = context.RecentFiles.Count() - Limit;
        if (excess > 0) {
            var stale = context.RecentFiles
                .OrderBy(file => file.LastOpenedUtc)
                .Take(excess)
                .ToList();

            context.RecentFiles.RemoveRange(stale);
            context.SaveChanges();
        }
    }

    /// <summary>
    /// A timestamp at least one tick after the last one handed out. Two opens
    /// inside a single clock tick would otherwise share a timestamp, and the
    /// list would then come back out of the database in whatever order the
    /// database felt like rather than the order the files were opened in.
    /// </summary>
    /// <returns>A UTC timestamp strictly later than the previous one.</returns>
    private DateTime NextUtc() {
        var now = DateTime.UtcNow;
        _lastStamp = now > _lastStamp ? now : _lastStamp.AddTicks(1);
        return _lastStamp;
    }
}
