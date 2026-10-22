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

    /// <summary>The relationship maps: one row per story's graph.</summary>
    public DbSet<CharacterGraph> Graphs => Set<CharacterGraph>();

    /// <summary>The characters standing in the graphs.</summary>
    public DbSet<GraphCharacter> GraphCharacters => Set<GraphCharacter>();

    /// <summary>
    /// The wiki: one row per character, kept whatever graphs they stand in.
    /// </summary>
    public DbSet<WikiCharacter> WikiCharacters => Set<WikiCharacter>();

    /// <summary>The ties drawn between them.</summary>
    public DbSet<GraphRelationship> GraphRelationships => Set<GraphRelationship>();

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

        // The alignment rides the same row, kept as its name for the same
        // reason: this store should read the way the files it serves read.
        modelBuilder.Entity<UserSettings>()
            .Property(settings => settings.TextAlign)
            .HasConversion<string>();

        // A command answers to one gesture, so its id is the row's name as
        // much as its key: a second row for the same command would be a
        // second answer, and there is only ever one.
        modelBuilder.Entity<KeyBinding>().HasKey(binding => binding.Command);

        // A graph's name is the picker's whole vocabulary, so it stands
        // unique: two graphs answering to the same word would be two rows
        // the writer cannot tell apart.
        modelBuilder.Entity<CharacterGraph>().HasIndex(graph => graph.Name).IsUnique();

        // The characters stand in one graph each, and the graph takes them
        // with it: deleting a story leaves no orphan nodes standing in
        // tables nothing will ever draw.
        modelBuilder.Entity<GraphCharacter>()
            .HasOne(character => character.Graph)
            .WithMany(graph => graph.Characters)
            .HasForeignKey(character => character.GraphId)
            .OnDelete(DeleteBehavior.Cascade);

        // A character in a graph is a wiki entry standing in it: the entry
        // is the character, and the graph only says where they stand. The
        // wiki takes the node with it — a character deleted from the wiki
        // leaves no node standing in tables nothing will ever draw.
        modelBuilder.Entity<GraphCharacter>()
            .HasOne(character => character.WikiCharacter)
            .WithMany()
            .HasForeignKey(character => character.WikiCharacterId)
            .OnDelete(DeleteBehavior.Cascade);

        // A tie belongs to the graph it is drawn in — and to the two
        // characters it joins, whichever way it points. Losing one of them
        // ends the tie the same way deleting a character ends every tie
        // standing on them, so no arrow is ever left pointing at nobody.
        modelBuilder.Entity<GraphRelationship>()
            .HasOne(relationship => relationship.Graph)
            .WithMany(graph => graph.Relationships)
            .HasForeignKey(relationship => relationship.GraphId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GraphRelationship>()
            .HasOne(relationship => relationship.FromCharacter)
            .WithMany()
            .HasForeignKey(relationship => relationship.FromCharacterId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GraphRelationship>()
            .HasOne(relationship => relationship.ToCharacter)
            .WithMany()
            .HasForeignKey(relationship => relationship.ToCharacterId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static string BuildConnectionString() {
        AppFolder.EnsureExists();

        return $"Data Source={AppFolder.Database}";
    }
}
