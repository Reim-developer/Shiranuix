using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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

    /// <summary>
    /// A factory that builds contexts over a fixed set of options, which is
    /// all the stores ask of one. The container's own factory is an internal
    /// EF Core type, so the tests carry a two-method one of their own.
    /// </summary>
    private sealed class ContextFactory : IDbContextFactory<ShiranuixContext> {
        private readonly DbContextOptions<ShiranuixContext> _options;

        public ContextFactory(DbContextOptions<ShiranuixContext> options) {
            _options = options;
        }

        public ShiranuixContext CreateDbContext() => new(_options);
    }
}
