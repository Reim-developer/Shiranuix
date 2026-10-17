using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shiranuix.Persistence;
using Shiranuix.ViewModels;
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

            // The tree is the editor's own: it follows the document and stands
            // on the folder it was left on. Started once the window is up, so
            // the column has something to bind to, and it asks into the column
            // the moment it has a folder to stand on.
            var editor = _services.GetRequiredService<EditorViewModel>();
            if (editor.Tree is { } tree) {
                _ = tree.StartAsync(CancellationToken.None).ContinueWith(_ =>
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                        if (tree.Panel is { } panel) {
                            editor.Panel.Show(panel);
                        }
                    }));
            }

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

        }

        base.OnFrameworkInitializationCompleted();
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
