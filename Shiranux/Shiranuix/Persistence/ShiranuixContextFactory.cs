using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Shiranuix.Persistence;

/// <summary>
/// Builds a context for the EF Core command line tools, which have no
/// container to resolve one from.
/// </summary>
/// <remarks>
/// Same database file as the running application, so a migration is developed
/// against the same shape the app will read.
/// </remarks>
public sealed class ShiranuixContextFactory : IDesignTimeDbContextFactory<ShiranuixContext> {
    /// <summary>
    /// Creates a context pointed at the application's database file.
    /// </summary>
    /// <param name="args">Command line arguments, unused.</param>
    /// <returns>A context over the application's store.</returns>
    public ShiranuixContext CreateDbContext(string[] args) {
        var options = new DbContextOptionsBuilder<ShiranuixContext>()
            .UseSqlite(ShiranuixContext.ConnectionString)
            .Options;

        return new ShiranuixContext(options);
    }
}
