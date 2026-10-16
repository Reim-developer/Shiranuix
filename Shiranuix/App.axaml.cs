using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shiranuix.Persistence;
using Shiranuix.PluginHost;
using Shiranuix.Views;
using Splat.Microsoft.Extensions.DependencyInjection;

namespace Shiranuix;

public partial class App : Application {
    /// <summary>
    /// The service container. It is also Splat's resolver — see the
    /// constructor — so this field is only here to give the composition root
    /// a typed, fail-fast way to ask for the window.
    /// </summary>
    private readonly ServiceProvider _services;

    /// <summary>
    /// Builds the container and hands it to Splat.
    /// </summary>
    /// <remarks>
    /// The container is built here rather than in
    /// <see cref="OnFrameworkInitializationCompleted"/>, the usual home for
    /// this, because the XAML previewer creates the application but never
    /// finishes framework initialization — and the window it constructs still
    /// asks the container for a view model to preview against. Everything the
    /// registrations touch is plain .NET, so nothing is too early.
    /// </remarks>
    public App() {
        var services = new ServiceCollection();
        services.AddShiranuix();

        _services = services.BuildServiceProvider();

        // From here on Splat *is* this container: anything that asks
        // AppLocator.Current for a service — this window today, ReactiveUI if
        // it ever arrives — gets the instances registered above.
        _services.UseMicrosoftDependencyResolver();
    }

    public override void Initialize() {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted() {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            // The store is brought up to the current schema before anything
            // reads it: the window below restores the user's settings, recent
            // files and font history from it on construction.
            MigrateStore();

            // The window and everything under it come from the container; the
            // only thing left to do by hand is the command-line argument,
            // which is a call into a window the container just built.
            var window = _services.GetRequiredService<MainWindow>();
            desktop.MainWindow = window;

            // `Shiranuix path\to\file.txt` opens that file straight away, which
            // is what double-clicking a document in Explorer does. The first
            // argument that names a real file wins; anything else is ignored.
            if (desktop.Args is { } args) {
                foreach (var arg in args) {
                    if (File.Exists(arg)) {
                        window.OpenStartupFile(arg);
                        break;
                    }
                }
            }

            // Plugins are read once the window exists, so the document they
            // plug into is already there and a slow one delays nothing a
            // writer is doing.
            StartPlugins(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Reads the plugin folder, and gives every plugin a short while to let go
    /// of what it holds when the editor is closing.
    /// </summary>
    /// <param name="desktop">The lifetime whose exit ends the plugins.</param>
    private void StartPlugins(IClassicDesktopStyleApplicationLifetime desktop) {
        var catalog = _services.GetRequiredService<PluginCatalog>();
        var log = _services.GetRequiredService<ILogger<PluginCatalog>>();

        desktop.Exit += (_, _) => {
            // Off the window's thread, and on a timer: a plugin that will not
            // let go is a plugin the editor stops waiting for. Blocking here
            // would also block the very thread a plugin's own shutdown would
            // have been handed back to.
            using var closing = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            Task.Run(() => catalog.ShutdownAsync(closing.Token)).GetAwaiter().GetResult();
        };

        _ = LoadPluginsAsync(catalog, log);
    }

    /// <summary>
    /// Reads the plugin folder, writing down anything that will not load.
    /// </summary>
    /// <param name="catalog">What is installed.</param>
    /// <param name="log">Where the refusals go.</param>
    /// <returns>A task that finishes when the folder has been read.</returns>
    private static async Task LoadPluginsAsync(PluginCatalog catalog, ILogger log) {
        try {
            await catalog.LoadAsync(PluginFolder.Root, CancellationToken.None);
        } catch (Exception error) {
            // The catalog keeps a bad plugin to itself; this is the last
            // thing that could take the editor down with it.
            log.LogError(error, "The plugin folder could not be read.");
        }
    }

    /// <summary>
    /// Applies any pending migrations to the store, creating it on first run.
    /// </summary>
    /// <remarks>
    /// Done here rather than in the constructor so the XAML previewer, which
    /// creates the application but never finishes framework initialization,
    /// does not go looking for a database it has no use for.
    /// </remarks>
    private void MigrateStore() {
        using var scope = _services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ShiranuixContext>>();

        using var context = factory.CreateDbContext();
        context.Database.Migrate();
    }
}
