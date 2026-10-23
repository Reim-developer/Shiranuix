using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shiranuix.Graphs.Persistence;
using Shiranuix.Logging;
using Shiranuix.Models;
using Shiranuix.Persistence;

namespace Shiranuix.Tests.Persistence;

/// <summary>
/// The SQLite stores against a real database, in a temporary file.
/// </summary>
/// <remarks>
/// The migration is what is worth checking here: a missing table or a
/// misnamed column is invisible to every other test, because they all run
/// over fakes. Each test gets its own file, so nothing is shared between them.
/// </remarks>
public sealed class SqliteStoreTests : IDisposable {
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "ShiranuixTests", Guid.NewGuid().ToString("N"));

    public SqliteStoreTests() {
        // SQLite will not create the directory the way it creates the file, so
        // a missing one surfaces as "unable to open database file".
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
    public void The_First_Migration_Creates_All_Three_Tables() {
        var factory = NewStore();

        var tables = factory.CreateDbContext()
            .Database
            .SqlQuery<string>($"SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name")
            .ToList();

        Assert.Contains("FontUses", tables);
        Assert.Contains("RecentFiles", tables);
        Assert.Contains("UserSettings", tables);
        // The history table is what keeps the next launch from applying the
        // same migration twice.
        Assert.Contains("__EFMigrationsHistory", tables);
    }

    [Fact]
    public void The_Store_Lives_In_The_Editor_s_Own_Folder() {
        // One answer for the log and the store, because two answers computed
        // separately are two places for a path to go wrong.
        Assert.Equal(AppFolder.Database, ShiranuixContext.ConnectionString["Data Source=".Length..]);
        Assert.StartsWith(AppFolder.Root + Path.DirectorySeparatorChar, AppFolder.Database);
    }

    [Fact]
    public void The_Log_Sits_Beside_The_Store() {
        Assert.StartsWith(AppFolder.Root + Path.DirectorySeparatorChar, Logs.CurrentFile);
    }

    [Fact]
    public void Settings_Save_Then_Load_Round_Trips() {
        var factory = NewStore();
        var store = new SqliteUserSettings(factory);

        store.Save(new UserSettings {
            FontFamily = "Verdana",
            FontSize = 24,
            ThemeMode = ThemeMode.Dark
        });

        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.Equal("Verdana", loaded.FontFamily);
        Assert.Equal(24, loaded.FontSize);
        Assert.Equal(ThemeMode.Dark, loaded.ThemeMode);
    }

    [Fact]
    public void Settings_Load_On_An_Empty_Store_Returns_Nothing() {
        var factory = NewStore();
        var store = new SqliteUserSettings(factory);

        Assert.Null(store.Load());
    }

    [Fact]
    public void Saving_Twice_Updates_The_Same_Row() {
        var factory = NewStore();
        var store = new SqliteUserSettings(factory);

        store.Save(new UserSettings { FontFamily = "Georgia", FontSize = 17, ThemeMode = ThemeMode.System });
        store.Save(new UserSettings { FontFamily = "Verdana", FontSize = 24, ThemeMode = ThemeMode.Dark });

        using var context = factory.CreateDbContext();

        var stored = context.UserSettings.Single();
        Assert.Equal("Verdana", stored.FontFamily);
        Assert.Equal(24, stored.FontSize);
        Assert.Equal(ThemeMode.Dark, stored.ThemeMode);
    }

    [Fact]
    public void Turning_Wrap_Off_Survives_A_Later_Save() {
        // The wrap choice rides the same row as the font and the theme —
        // and a row that forgets it on every save after the first is a
        // setting that cannot survive a restart. The second save is an
        // update of an existing row, which is the path that used to skip
        // the wrap column and leave it at whatever it had been.
        var factory = NewStore();
        var store = new SqliteUserSettings(factory);

        store.Save(new UserSettings { FontFamily = "Georgia", FontSize = 17, WordWrap = true });
        store.Save(new UserSettings { FontFamily = "Verdana", FontSize = 24, WordWrap = false });

        Assert.False(store.Load()?.WordWrap);
    }

    [Fact]
    public void The_Alignment_Rides_The_Row_And_Survives_A_Later_Save() {
        // The alignment is one more field of the same single row, kept by
        // its name in the store; a later save is an update, so the choice
        // must move with it rather than fall back to the column's own
        // default.
        var factory = NewStore();
        var store = new SqliteUserSettings(factory);

        store.Save(new UserSettings { FontFamily = "Georgia", FontSize = 17, TextAlign = TextAlign.Center });
        store.Save(new UserSettings { FontFamily = "Verdana", FontSize = 24, TextAlign = TextAlign.Right });

        Assert.Equal(TextAlign.Right, store.Load()?.TextAlign);
    }

    [Fact]
    public void The_Quotes_Keep_The_Turn_A_Later_Save_Says() {
        // The typing turns ride the same single row as the font and the
        // theme; a later save is an update of that row, and an update that
        // skips the quote column would bring the row back to whatever it
        // had been on the first launch.
        var factory = NewStore();
        var store = new SqliteUserSettings(factory);

        store.Save(new UserSettings { FontFamily = "Georgia", SmartQuotes = true });
        store.Save(new UserSettings { FontFamily = "Verdana", SmartQuotes = false });

        Assert.False(store.Load()?.SmartQuotes);
    }

    [Fact]
    public void The_Dash_Keeps_The_Turn_A_Later_Save_Says() {
        var factory = NewStore();
        var store = new SqliteUserSettings(factory);

        store.Save(new UserSettings { FontFamily = "Georgia", DashMode = DashMode.Em });
        store.Save(new UserSettings { FontFamily = "Verdana", DashMode = DashMode.Off });

        Assert.Equal(DashMode.Off, store.Load()?.DashMode);
    }

    [Fact]
    public void The_Welcome_Keeps_The_Setting_A_Later_Save_Says() {
        // The invitation rides the same single row as the rest of the
        // settings; a later save is an update of that row, and an update
        // that skipped the column would bring the invitation back on a
        // writer who has already sent it away.
        var factory = NewStore();
        var store = new SqliteUserSettings(factory);

        store.Save(new UserSettings { FontFamily = "Georgia", ShowWelcomeMessage = true });
        store.Save(new UserSettings { FontFamily = "Verdana", ShowWelcomeMessage = false });

        Assert.False(store.Load()?.ShowWelcomeMessage);
    }

    [Fact]
    public void Key_Binding_Overrides_Round_Trip_And_Replace_The_Set() {
        var factory = NewStore();
        var store = new SqliteKeyBindings(factory);

        store.ReplaceAll(new[] {
            new KeyBinding { Command = "Save", Gesture = "Ctrl+Shift+S" },
            new KeyBinding { Command = "Open", Gesture = "Alt+O" }
        });

        // A second write is the whole set again, not an addition: what the
        // state no longer says must not be waiting in the table for the
        // next launch to find.
        store.ReplaceAll(new[] { new KeyBinding { Command = "Save", Gesture = "Ctrl+K" } });

        var loaded = store.LoadAll();
        var binding = Assert.Single(loaded);
        Assert.Equal("Save", binding.Command);
        Assert.Equal("Ctrl+K", binding.Gesture);
    }

    [Fact]
    public void Recent_Files_Are_Returned_Newest_First() {
        var factory = NewStore();
        var time = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Seeded with explicit timestamps: three calls in a row can land inside
        // one clock tick, and the order they came back in would then be
        // whatever the database felt like.
        using (var context = factory.CreateDbContext()) {
            context.RecentFiles.AddRange(
                new RecentFile { Path = "a.txt", LastOpenedUtc = time },
                new RecentFile { Path = "b.txt", LastOpenedUtc = time.AddMinutes(1) },
                new RecentFile { Path = "c.txt", LastOpenedUtc = time.AddMinutes(2) });
            context.SaveChanges();
        }

        var store = new SqliteRecentFilesStore(factory);

        Assert.Equal(new[] { "c.txt", "b.txt", "a.txt" }, store.Load());
    }

    [Fact]
    public void Remembering_A_Path_Again_Moves_It_To_The_Front() {
        var factory = NewStore();
        var store = new SqliteRecentFilesStore(factory);

        store.Remember("a.txt");
        store.Remember("b.txt");
        store.Remember("a.txt");

        Assert.Equal(new[] { "a.txt", "b.txt" }, store.Load());
    }

    [Fact]
    public void The_Recent_List_Is_Capped_Ten() {
        var factory = NewStore();
        var store = new SqliteRecentFilesStore(factory);

        for (var i = 0; i < 15; i++) {
            store.Remember($"file{i}.txt");
        }

        var loaded = store.Load();

        Assert.Equal(10, loaded.Count);
        // Newest first, oldest evicted.
        Assert.Equal("file14.txt", loaded[0]);
        Assert.Equal("file5.txt", loaded[^1]);
    }

    [Fact]
    public void Font_History_Lists_Families_Newest_First_Without_Repeats() {
        var factory = NewStore();
        var store = new SqliteFontHistoryStore(factory);

        store.Remember("Georgia", 17);
        store.Remember("Verdana", 24);
        store.Remember("Georgia", 32);

        // Georgia was picked last, so it leads; the earlier Georgia pick does
        // not put it there twice.
        Assert.Equal(new[] { "Georgia", "Verdana" }, store.RecentFamilies());
    }

    [Fact]
    public void Font_History_Lists_Sizes_Newest_First_Without_Repeats() {
        var factory = NewStore();
        var store = new SqliteFontHistoryStore(factory);

        store.Remember("Georgia", 17);
        store.Remember("Verdana", 24);
        store.Remember("Georgia", 32);

        Assert.Equal(new[] { 32d, 24d, 17d }, store.RecentSizes());
    }

    [Fact]
    public void Remembering_The_Same_Pairing_Twice_Keeps_One_Row() {
        var factory = NewStore();
        var store = new SqliteFontHistoryStore(factory);

        store.Remember("Georgia", 17);
        store.Remember("Georgia", 17);

        using var context = factory.CreateDbContext();

        var use = context.FontUses.Single();
        Assert.Equal("Georgia", use.FontFamily);
        Assert.Equal(17, use.FontSize);
    }

    [Fact]
    public void The_Graph_Migration_Stands_Its_Three_Tables() {
        var factory = NewStore();

        var tables = factory.CreateDbContext()
            .Database
            .SqlQuery<string>($"SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name")
            .ToList();

        Assert.Contains("Graphs", tables);
        Assert.Contains("GraphCharacters", tables);
        Assert.Contains("GraphRelationships", tables);
    }

    [Fact]
    public void A_Graph_May_Not_Be_Made_Or_Renamed_Under_A_Standing_Name() {
        var factory = NewStore();
        var store = new SqliteGraphStore(factory);
        store.CreateGraph("Witch And Magical");

        Assert.Throws<InvalidOperationException>(() => store.CreateGraph("Witch And Magical"));

        var other = store.CreateGraph("The Other Story");
        Assert.Throws<InvalidOperationException>(() => store.RenameGraph(other, "Witch And Magical"));

        // The graph's own name, on the other hand, is its own to keep.
        store.RenameGraph(other, "The Other Story");
        Assert.Equal(
            new[] { "The Other Story", "Witch And Magical" },
            store.ListGraphs().Select(graph => graph.Name));
    }

    [Fact]
    public void The_Picker_States_What_Stands_In_Each_Graph() {
        var factory = NewStore();
        var store = new SqliteGraphStore(factory);
        var graph = store.CreateGraph("Witch And Magical");
        var mara = AddCharacter(factory, store, graph, "Mara", 10, 10);
        var tomas = AddCharacter(factory, store, graph, "Tomas", 20, 20);
        store.AddRelationship(graph, mara, tomas, "the teacher of");

        var summary = Assert.Single(store.ListGraphs());
        Assert.Equal("Witch And Magical", summary.Name);
        Assert.Equal(2, summary.CharacterCount);
        Assert.Equal(1, summary.RelationshipCount);
    }

    [Fact]
    public void Deleting_A_Graph_Takes_Its_Characters_And_Ties_With_It() {
        var factory = NewStore();
        var store = new SqliteGraphStore(factory);
        var graph = store.CreateGraph("Witch And Magical");
        var mara = AddCharacter(factory, store, graph, "Mara", 10, 10);
        var tomas = AddCharacter(factory, store, graph, "Tomas", 20, 20);
        store.AddRelationship(graph, mara, tomas, "the teacher of");

        store.DeleteGraph(graph);

        Assert.Empty(store.ListGraphs());

        using var context = factory.CreateDbContext();
        Assert.Equal(0, context.GraphCharacters.Count());
        Assert.Equal(0, context.GraphRelationships.Count());
    }

    [Fact]
    public void Deleting_A_Character_Takes_Every_Tie_Standing_On_Them() {
        // Both directions: a tie leaving from them and a tie arriving at
        // them are both ties nothing could draw any more.
        var factory = NewStore();
        var store = new SqliteGraphStore(factory);
        var graph = store.CreateGraph("Witch And Magical");
        var mara = AddCharacter(factory, store, graph, "Mara", 10, 10);
        var tomas = AddCharacter(factory, store, graph, "Tomas", 20, 20);
        var ivo = AddCharacter(factory, store, graph, "Ivo", 30, 30);
        store.AddRelationship(graph, mara, tomas, "the teacher of");
        store.AddRelationship(graph, ivo, mara, "owes");

        store.DeleteCharacter(mara);

        var snapshot = store.LoadGraph(graph);
        Assert.Equal(
            new[] { "Tomas", "Ivo" },
            snapshot.Nodes.Select(character => character.Name));
        Assert.Empty(snapshot.Edges);
    }

    [Fact]
    public void A_Tie_May_Not_Join_Characters_From_Another_Graph() {
        var factory = NewStore();
        var store = new SqliteGraphStore(factory);
        var first = store.CreateGraph("First");
        var second = store.CreateGraph("Second");
        var mara = AddCharacter(factory, store, first, "Mara", 10, 10);
        var tomas = AddCharacter(factory, store, second, "Tomas", 20, 20);

        // SQLite does not stand over the keys on its own, so the store
        // refuses the tie itself: no arrow may be drawn from one story's
        // cast into another's, whichever graph claims to draw it.
        Assert.Throws<InvalidOperationException>(() =>
            store.AddRelationship(first, mara, tomas, "the teacher of"));
        Assert.Throws<InvalidOperationException>(() =>
            store.AddRelationship(second, mara, tomas, "the teacher of"));
    }

    [Fact]
    public void Bending_A_Tie_Keeps_The_Curve_Where_It_Was_Let_Go_Of() {
        var factory = NewStore();
        var store = new SqliteGraphStore(factory);
        var graph = store.CreateGraph("Witch And Magical");
        var mara = AddCharacter(factory, store, graph, "Mara", 10, 10);
        var tomas = AddCharacter(factory, store, graph, "Tomas", 20, 20);
        var tie = store.AddRelationship(graph, mara, tomas, "the teacher of");

        store.BendRelationship(tie, -48.5);

        var stored = Assert.Single(store.LoadGraph(graph).Edges);
        Assert.Equal(-48.5, stored.Bend);

        // A tie just drawn stands straight until a hand says otherwise.
        var fresh = store.AddRelationship(graph, tomas, mara, "owes a debt");
        Assert.Equal(0, store.LoadGraph(graph).Edges.Single(edge => edge.Id == fresh).Bend);
    }

    [Fact]
    public void Placing_The_Whole_Arrangement_Lands_Every_Node_Where_It_Was_Told() {
        var factory = NewStore();
        var store = new SqliteGraphStore(factory);
        var graph = store.CreateGraph("Witch And Magical");
        var mara = AddCharacter(factory, store, graph, "Mara", 0, 0);
        var tomas = AddCharacter(factory, store, graph, "Tomas", 0, 0);

        store.PlaceAll(graph, [(mara, 111, 222), (tomas, 333, 444)]);

        var snapshot = store.LoadGraph(graph);
        var placedMara = snapshot.Nodes.Single(character => character.Id == mara);
        var placedTomas = snapshot.Nodes.Single(character => character.Id == tomas);
        Assert.Equal((111d, 222d), (placedMara.X, placedMara.Y));
        Assert.Equal((333d, 444d), (placedTomas.X, placedTomas.Y));
    }

    /// <summary>
    /// Puts a character in the wiki and stands them in a graph — the two
    /// steps a character takes, said together, the way the state says them.
    /// </summary>
    /// <param name="factory">The factory the stores are built over.</param>
    /// <param name="store">The graph store they stand in.</param>
    /// <param name="graphId">The graph they stand in.</param>
    /// <param name="name">What they are called.</param>
    /// <param name="x">Where they stand, across.</param>
    /// <param name="y">Where they stand, down.</param>
    /// <returns>The node's id.</returns>
    private static int AddCharacter(
        IDbContextFactory<ShiranuixContext> factory,
        SqliteGraphStore store,
        int graphId,
        string name,
        double x,
        double y) {
        var wiki = new SqliteWikiStore(factory);
        var wikiId = wiki.CreateCharacter(name, string.Empty, string.Empty, string.Empty);
        return store.AddCharacter(graphId, wikiId, x, y);
    }

    [Fact]
    public void The_Characters_Written_Down_Before_The_Wiki_Existed_Are_Carried_Into_It() {
        // A store that has been in use already: a story, a cast and a tie,
        // written down the way it was written down before the wiki was
        // there. The names and the notes must not be lost with the columns
        // that held them.
        var factory = new ContextFactory(OptionsFor(NewDatabase()));

        using (var context = factory.CreateDbContext()) {
            context.Database.Migrate("20261022044114_AddRelationshipBend");

            var graph = new CharacterGraph {
                Name = "Witch And Magical",
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };
            context.Graphs.Add(graph);
            context.SaveChanges();

            // Two characters with one name between them, because a story
            // may have two of anything — and each must keep its own note.
            var mara = InsertCharacter(context, graph.Id, "Mara", "the witch");
            var tomas = InsertCharacter(context, graph.Id, "Mara", "the other Mara");

            context.GraphRelationships.Add(new GraphRelationship {
                GraphId = graph.Id,
                FromCharacterId = mara,
                ToCharacterId = tomas,
                Label = "the enemy of"
            });
            context.SaveChanges();

            // And now the wiki arrives.
            context.Database.Migrate();
        }

        using var after = factory.CreateDbContext();

        var entries = after.WikiCharacters.OrderBy(character => character.Id).ToList();
        Assert.Equal(2, entries.Count);
        Assert.Equal("Mara", entries[0].Name);
        Assert.Equal("the witch", entries[0].Note);
        Assert.Equal("Mara", entries[1].Name);
        Assert.Equal("the other Mara", entries[1].Note);

        // Every node stands for an entry of its own, and not for the other
        // character's entry: a character is a character, and two of the
        // same name are two characters.
        var nodes = after.GraphCharacters.OrderBy(character => character.Id).ToList();
        Assert.Equal(entries[0].Id, nodes[0].WikiCharacterId);
        Assert.Equal(entries[1].Id, nodes[1].WikiCharacterId);

        // And the tie standing on them is still standing.
        Assert.Single(after.GraphRelationships);
    }

    /// <summary>
    /// Puts a character in a store that has not heard of the wiki yet: the
    /// columns are the old ones, and the row is written the old way.
    /// </summary>
    /// <param name="context">The context to write through.</param>
    /// <param name="graphId">The graph the character stands in.</param>
    /// <param name="name">What the character is called.</param>
    /// <param name="note">What the writer remembers of them.</param>
    /// <returns>The character's row.</returns>
    private static int InsertCharacter(
        ShiranuixContext context,
        int graphId,
        string name,
        string note) {
        context.Database.ExecuteSqlRaw(
            """
            INSERT INTO "GraphCharacters" ("GraphId", "Name", "Note", "X", "Y")
            VALUES ({0}, {1}, {2}, 10, 10);
            """,
            graphId,
            name,
            note);

        // The row's id asked of the database, not of the connection:
        // last_insert_rowid() belongs to whichever connection answered,
        // and a cleared pool — another test's teardown empties every pool,
        // not only its own — can leave the next question heard by a fresh
        // one that has inserted nothing. Every test writes to a file of its
        // own, so the newest row is always this one.
        return context.Database
            .SqlQueryRaw<int>("SELECT \"Id\" AS \"value\" FROM \"GraphCharacters\" ORDER BY \"Id\" DESC LIMIT 1")
            .Single();
    }

    /// <summary>
    /// A factory over a fresh, migrated database in this test's directory.
    /// </summary>
    /// <returns>The factory, which the caller disposes.</returns>
    private IDbContextFactory<ShiranuixContext> NewStore() {
        var factory = new ContextFactory(OptionsFor(NewDatabase()));

        using var context = factory.CreateDbContext();
        context.Database.Migrate();

        return factory;
    }

    private string NewDatabase() => Path.Combine(_directory, $"{Guid.NewGuid():N}.db");

    private static DbContextOptions<ShiranuixContext> OptionsFor(string path) =>
        new DbContextOptionsBuilder<ShiranuixContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
}
