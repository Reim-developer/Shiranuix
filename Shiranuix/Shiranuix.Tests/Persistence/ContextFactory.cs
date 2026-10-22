using Microsoft.EntityFrameworkCore;
using Shiranuix.Persistence;

namespace Shiranuix.Tests.Persistence;

/// <summary>
/// A factory that builds contexts over a fixed set of options, which is all
/// the stores ask of one. The container's own factory is an internal EF
/// Core type, so the tests carry a two-method one of their own.
/// </summary>
internal sealed class ContextFactory : IDbContextFactory<ShiranuixContext> {
    private readonly DbContextOptions<ShiranuixContext> _options;

    /// <summary>
    /// Creates the factory over the given options.
    /// </summary>
    /// <param name="options">The options contexts are built with.</param>
    public ContextFactory(DbContextOptions<ShiranuixContext> options) {
        _options = options;
    }

    /// <summary>
    /// Builds a context over the options this factory was given.
    /// </summary>
    /// <returns>The context.</returns>
    public ShiranuixContext CreateDbContext() => new(_options);
}
