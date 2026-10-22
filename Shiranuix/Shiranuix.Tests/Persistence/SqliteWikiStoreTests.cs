using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shiranuix.Graphs.Persistence;
using Shiranuix.Persistence;

namespace Shiranuix.Tests.Persistence;

/// <summary>
/// The wiki against a real database, in a temporary file: the characters
/// themselves, and what happens to the nodes standing for them.
/// </summary>
/// <remarks>
/// The wiki is the cast of every story at once, so its own rules are the
/// ones worth checking here: a character taken out of it takes every node
/// standing for them with them, and a map may not stand for a character
/// nobody keeps.
/// </remarks>
public sealed class SqliteWikiStoreTests : IDisposable {
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "ShiranuixTests", Guid.NewGuid().ToString("N"));

    public SqliteWikiStoreTests() {
        // SQLite will not create the directory the way it creates the file,
        // so a missing one surfaces as "unable to open database file".
        Directory.CreateDirectory(_directory);
    }

    public void Dispose() {
        // The connection pool can hold the file open after the last context
        // has been disposed, which would leave the directory undeletable and
        // the next test's temp path littered with the last one's files.
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_directory)) {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void A_Character_Put_In_The_Wiki_Comes_Back_As_They_Were_Written() {
        var store = NewStore();

        var id = store.CreateCharacter("Mara", "the night of the storm", "a witch", "the writer's note");

        var character = Assert.Single(store.ListCharacters());
        Assert.Equal(id, character.Id);
        Assert.Equal("Mara", character.Name);
        Assert.Equal("the night of the storm", character.BirthDate);
        Assert.Equal("a witch", character.Description);
        Assert.Equal("the writer's note", character.Note);
    }

    [Fact]
    public void The_Wiki_Lists_Its_Characters_By_Name() {
        var store = NewStore();
        store.CreateCharacter("Tomas", "", "", "");
        store.CreateCharacter("Mara", "", "", "");

        Assert.Equal(
            new[] { "Mara", "Tomas" },
            store.ListCharacters().Select(character => character.Name));
    }

    [Fact]
    public void What_The_Wiki_Hears_Of_A_Character_It_Forgets_And_Hears_Again() {
        var store = NewStore();
        var id = store.CreateCharacter("Mara", "", "a witch", "");

        store.UpdateCharacter(id, "Mara of the Marsh", "the first of spring", "a witch of the marsh", "changed");

        var character = Assert.Single(store.ListCharacters());
        Assert.Equal("Mara of the Marsh", character.Name);
        Assert.Equal("the first of spring", character.BirthDate);
        Assert.Equal("a witch of the marsh", character.Description);
        Assert.Equal("changed", character.Note);
    }

    [Fact]
    public void The_Wiki_Says_When_It_Has_Heard_Something_New() {
        var store = NewStore();
        var heard = 0;
        store.Changed += () => heard++;

        var id = store.CreateCharacter("Mara", "", "", "");
        store.UpdateCharacter(id, "Mara", "", "a witch", "");
        store.DeleteCharacter(id);

        Assert.Equal(3, heard);
    }

    [Fact]
    public void A_Character_Taken_Out_Of_The_Wiki_Takes_Every_Node_Standing_For_Them() {
        var factory = NewFactory();
        var wiki = new SqliteWikiStore(factory);
        var graphs = new SqliteGraphStore(factory);

        var graph = graphs.CreateGraph("Witch And Magical");
        var mara = wiki.CreateCharacter("Mara", "", "", "");
        var tomas = wiki.CreateCharacter("Tomas", "", "", "");
        var maraNode = graphs.AddCharacter(graph, mara, 10, 10);
        var tomasNode = graphs.AddCharacter(graph, tomas, 20, 20);
        graphs.AddRelationship(graph, maraNode, tomasNode, "the teacher of");

        wiki.DeleteCharacter(mara);

        // The node standing for the character is gone — and the tie that
        // stood on it with them, for a tie to nobody is no tie at all.
        var snapshot = graphs.LoadGraph(graph);
        Assert.Equal(new[] { "Tomas" }, snapshot.Nodes.Select(node => node.Name));
        Assert.Empty(snapshot.Edges);
    }

    [Fact]
    public void A_Node_May_Not_Stand_For_A_Character_Nobody_Keeps() {
        var factory = NewFactory();
        var graphs = new SqliteGraphStore(factory);
        var graph = graphs.CreateGraph("Witch And Magical");

        // A node pointing at nothing would be a node with no face, and the
        // store refuses it rather than drawing a blank circle.
        Assert.Throws<InvalidOperationException>(() =>
            graphs.AddCharacter(graph, 404, 10, 10));
    }

    [Fact]
    public void The_Wiki_Counts_The_Nodes_Standing_For_A_Character() {
        var factory = NewFactory();
        var wiki = new SqliteWikiStore(factory);
        var graphs = new SqliteGraphStore(factory);

        var first = graphs.CreateGraph("First");
        var second = graphs.CreateGraph("Second");
        var mara = wiki.CreateCharacter("Mara", "", "", "");
        graphs.AddCharacter(first, mara, 10, 10);
        graphs.AddCharacter(second, mara, 20, 20);

        Assert.Equal(2, wiki.CountStandings(mara));
        Assert.Equal(0, wiki.CountStandings(9999));
    }

    /// <summary>
    /// A wiki over a fresh, migrated database in this test's directory.
    /// </summary>
    /// <returns>The wiki.</returns>
    private SqliteWikiStore NewStore() => new(NewFactory());

    /// <summary>
    /// A factory over a fresh, migrated database in this test's directory.
    /// </summary>
    /// <returns>The factory, which the caller disposes.</returns>
    private IDbContextFactory<ShiranuixContext> NewFactory() {
        var factory = new ContextFactory(OptionsFor(NewDatabase()));

        using var context = factory.CreateDbContext();
        context.Database.Migrate();

        return factory;
    }

    /// <summary>
    /// A database of this test's own, in this test's own directory.
    /// </summary>
    /// <returns>The path to it.</returns>
    private string NewDatabase() =>
        Path.Combine(_directory, "shiranuix.db");

    /// <summary>
    /// The options a context is built over, pointed at this test's database.
    /// </summary>
    /// <param name="path">The database to open.</param>
    /// <returns>The options.</returns>
    private static DbContextOptions<ShiranuixContext> OptionsFor(string path) =>
        new DbContextOptionsBuilder<ShiranuixContext>()
            .UseSqlite($"Data Source={path}")
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;
}
