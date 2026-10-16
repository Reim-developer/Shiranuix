using System.IO;
using Microsoft.EntityFrameworkCore;
using Shiranuix.Models;

namespace Shiranuix.Persistence;

/// <summary>
/// The store's schema, expressed as entities: one settings row, the files the
/// user has opened, and every typeface/size pairing they have picked.
/// </summary>
/// <remarks>
/// A context is created per operation through
/// <see cref="Microsoft.EntityFrameworkCore.IDbContextFactory{TContext}"/>,
/// never shared: a DbContext carries change tracking and is not thread-safe.
/// </remarks>
public sealed class ShiranuixContext : DbContext {
    /// <summary>
    /// Where the store lives: <c>%APPDATA%\Shiranuix\shiranuix.db</c>. Local,
    /// single-user, no server, no setup -- which is what a writing tool's
    /// store should be.
    /// </summary>
    public static string ConnectionString { get; } = BuildConnectionString();

    /// <summary>
    /// Creates a context from options, the way the dependency injection
    /// container and the EF Core command line tools both build one.
    /// </summary>
    /// <param name="options">The options to build with.</param>
    public ShiranuixContext(DbContextOptions<ShiranuixContext> options) : base(options) {
    }

    /// <summary>The settings: one row, always.</summary>
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();

    /// <summary>The remembered files.</summary>
    public DbSet<RecentFile> RecentFiles => Set<RecentFile>();

    /// <summary>The picked typeface/size pairings.</summary>
    public DbSet<FontUse> FontUses => Set<FontUse>();

    /// <summary>The keyboard shortcuts moved off their defaults.</summary>
    public DbSet<KeyBinding> KeyBindings => Set<KeyBinding>();

    /// <summary>
    /// Declares the schema: keys, the uniqueness each list depends on, and the
    /// one place a value is stored for readability rather than for speed.
    /// </summary>
    /// <param name="modelBuilder">The builder to declare against.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        // A path is remembered once, so re-opening a file is an update of its
        // timestamp rather than a second row for the same file.
        modelBuilder.Entity<RecentFile>().HasIndex(file => file.Path).IsUnique();

        // One row per family/size pair, so re-picking the same typeface at the
        // same size moves it to the front of the history instead of adding a
        // duplicate the picker would have to skip over.
        modelBuilder.Entity<FontUse>()
            .HasIndex(use => new { use.FontFamily, use.FontSize })
            .IsUnique();

        // The theme is stored as its name rather than its number, so the file
        // can be read by hand. A local store should be legible in a text
        // editor; that is the whole format this app edits.
        modelBuilder.Entity<UserSettings>()
            .Property(settings => settings.ThemeMode)
            .HasConversion<string>();

        // A command answers to one gesture, so its id is the row's name as
        // much as its key: a second row for the same command would be a
        // second answer, and there is only ever one.
        modelBuilder.Entity<KeyBinding>().HasKey(binding => binding.Command);
    }

    private static string BuildConnectionString() {
        AppFolder.EnsureExists();

        return $"Data Source={AppFolder.Database}";
    }
}
