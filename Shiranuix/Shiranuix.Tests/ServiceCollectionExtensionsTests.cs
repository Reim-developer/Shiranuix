using Avalonia.Controls;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shiranuix.Graphs;
using Shiranuix.Graphs.Dialogs;
using Shiranuix.Graphs.Persistence;
using Shiranuix.Persistence;
using Shiranuix.Services;
using Shiranuix.Services.Dialogs;
using Shiranuix.Services.Fonts;
using Shiranuix.ViewModels;
using Shiranuix.Views;
using Splat;
using Splat.Microsoft.Extensions.DependencyInjection;

namespace Shiranuix.Tests;

/// <summary>
/// Tests for <see cref="ServiceCollectionExtensions"/>, the list of
/// registrations the whole application is built from.
/// </summary>
/// <remarks>
/// These build a container but never a window: everything below the view model
/// can be constructed, and so checked, with no Avalonia application running.
/// Resolving <see cref="EditorViewModel"/> is the composition root's smoke
/// test — it walks every constructor in the graph and fails if a registration
/// went missing, if an implementation no longer fit its interface, or if a
/// service started reaching for the window before the container was finished
/// building it. It runs on every system the tests ride: nothing in the list
/// asks a machine what it is, and the paper engine's native half is aboard
/// every build a release cuts.
/// </remarks>
public sealed class ServiceCollectionExtensionsTests {
    [Fact]
    public void The_View_Model_Builds_Headlessly() {
        using var container = BuildContainer();

        var viewModel = container.GetRequiredService<EditorViewModel>();

        Assert.NotNull(viewModel);
    }

    [Fact]
    public void Every_Interface_Binds_To_Its_Implementation() {
        using var container = BuildContainer();

        Assert.IsType<FileService>(container.GetRequiredService<IFileService>());
        Assert.IsType<FontCatalog>(container.GetRequiredService<IFontCatalog>());
        Assert.IsType<WindowFileDialogService>(container.GetRequiredService<IFileDialogService>());
        Assert.IsType<WindowConfirmService>(container.GetRequiredService<IConfirmDialogService>());
        Assert.IsType<SettingsWindowService>(container.GetRequiredService<ISettingsWindowService>());
        Assert.IsType<PdfViewerWindowService>(container.GetRequiredService<IPdfViewerService>());
        Assert.IsType<GraphWindowService>(container.GetRequiredService<IGraphWindowService>());
        Assert.IsType<WindowGraphDialogService>(container.GetRequiredService<IGraphDialogService>());
        Assert.IsType<ImeCompositionGuard>(container.GetRequiredService<IImeCompositionGuard>());
        Assert.IsType<DocumentHistory>(container.GetRequiredService<IDocumentHistory>());
    }

    [Fact]
    public void The_Persisted_State_Is_Registered() {
        using var container = BuildContainer();

        Assert.IsType<SqliteUserSettings>(container.GetRequiredService<IUserSettings>());
        Assert.IsType<SqliteRecentFilesStore>(container.GetRequiredService<IRecentFilesStore>());
        Assert.IsType<SqliteFontHistoryStore>(container.GetRequiredService<IFontHistoryStore>());
        Assert.IsType<SqliteKeyBindings>(container.GetRequiredService<IKeyBindingsStore>());
        Assert.IsType<SqliteGraphStore>(container.GetRequiredService<IGraphStore>());

        // The factory the stores build their contexts from: a DbContext is not
        // thread-safe, so it is made per operation rather than shared.
        Assert.NotNull(container.GetRequiredService<IDbContextFactory<ShiranuixContext>>());
    }

    [Fact]
    public void The_Dialogs_Are_Registered_For_Every_Kind() {
        using var container = BuildContainer();

        // One service that shows any dialog, and one that knows what a discard
        // is, both behind their interfaces — the map's strike-out question
        // among them, which is a window prompt of the application's own.
        Assert.IsType<WindowDialogService>(container.GetRequiredService<IDialogService>());
        Assert.IsType<WindowConfirmService>(container.GetRequiredService<IConfirmDialogService>());
        Assert.IsType<WindowConfirmQuestion>(container.GetRequiredService<IConfirmQuestion>());
    }

    [Fact]
    public void The_Dialog_Services_Are_Handed_A_Window_They_Cannot_Reach_Yet() {
        using var container = BuildContainer();

        // What the dialog services are given is a way of asking for the window,
        // not the window itself: the window is built from the view model, the
        // view model from these services, and resolving it here would have the
        // container ask for the very thing it is in the middle of building.
        // Calling the delegate is left to the moment a dialog opens.
        var window = container.GetRequiredService<Func<Window>>();

        Assert.NotNull(window);
    }

    [Fact]
    public void Services_Are_Shared_Rather_Than_Built_Per_Request() {
        using var container = BuildContainer();

        Assert.Same(
            container.GetRequiredService<IFileService>(),
            container.GetRequiredService<IFileService>());
        Assert.Same(
            container.GetRequiredService<IFileDialogService>(),
            container.GetRequiredService<IFileDialogService>());

        // One document, one window, one view model behind it — one settings
        // service and one paper reader, which are what keep a second press of
        // the button bringing the open window forward instead of stacking
        // another.
        Assert.Same(
            container.GetRequiredService<EditorViewModel>(),
            container.GetRequiredService<EditorViewModel>());
        Assert.Same(
            container.GetRequiredService<ISettingsWindowService>(),
            container.GetRequiredService<ISettingsWindowService>());
        Assert.Same(
            container.GetRequiredService<IPdfViewerService>(),
            container.GetRequiredService<IPdfViewerService>());
    }

    [Fact]
    public void The_Window_Is_Registered_With_The_Container() {
        using var container = BuildContainer();

        // Registration only — constructing a Window wants a running Avalonia
        // application, and this suite runs without one.
        var registration = container.GetRequiredService<IServiceProviderIsService>();

        Assert.True(registration.IsService(typeof(MainWindow)));
        Assert.True(registration.IsService(typeof(SettingsWindow)));
    }

    [Fact]
    public void Handing_The_Container_To_Splat_Serves_The_Same_Instances() {
        // Splat is process-wide, so the resolver it starts with is put back
        // afterwards and no other test depends on which one was there.
        var original = AppLocator.GetLocator();

        try {
            using var container = BuildContainer();
            container.UseMicrosoftDependencyResolver();

            Assert.Same(
                container.GetRequiredService<IFileService>(),
                AppLocator.Current.GetService<IFileService>());
        } finally {
            AppLocator.SetLocator(original);
        }
    }

    private static ServiceProvider BuildContainer() {
        var services = new ServiceCollection();
        services.AddShiranuix();

        var container = services.BuildServiceProvider();

        // The application migrates the store before it builds the window, and
        // the tests do the same: the view model reads from it on construction,
        // so the schema has to be there before it is resolved.
        using (var scope = container.CreateScope()) {
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ShiranuixContext>>();

            using var context = factory.CreateDbContext();
            context.Database.Migrate();
        }

        return container;
    }
}
