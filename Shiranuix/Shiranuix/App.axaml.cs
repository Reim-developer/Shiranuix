using System.IO;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Shiranuix.Persistence;
using Shiranuix.Services.FileAssociations;
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

            // Whether this editor is standing in the machine's own menus is
            // brought up to date before the window comes up, because the next
            // double-click on a Shiranuix file should already be answered and
            // the only reason to wait would be a reason to have skipped it.
            if (OperatingSystem.IsWindows()) {
                RegisterWithWindows();
            }

            // The window and everything under it come from the container; the
            // only thing left to do by hand is the command-line argument,
            // which is a call into a window the container just built.
            var window = _services.GetRequiredService<MainWindow>();
            desktop.MainWindow = window;

            // The tree is the editor's own: it follows the document and stands
            // on the folder it was left on. Started once the window is up, and
            // it walks on into the column the moment it has a folder to stand
            // on.
            if (_services.GetRequiredService<EditorViewModel>().Tree is { } tree) {
                _ = tree.StartAsync(CancellationToken.None);
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

            // A build asked to prove itself rather than be used — the
            // runner's question, not a person's — has the window it just
            // built stood up for a moment: one frame drawn, one paper read,
            // one line on the console, and the process leaves. It is asked
            // last on purpose: everything above ran for real first, and the
            // answer is worth exactly that much.
            if (Smoke.IsAsked(desktop.Args)) {
                Smoke.Arrive(desktop, window);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Brings the machine's own menus up to date with this build: whether a
    /// <c>.shiranux</c> file is a Shiranuix Document, and whether a
    /// <c>.txt</c> file offers to be opened with Shiranuix.
    /// </summary>
    /// <remarks>
    /// Nothing is asked of anybody, and nothing may be allowed to stop the
    /// editor from opening. A machine that will not have its registry written
    /// is still a machine that has to run the editor — the writing may be
    /// barred, the disk may be full, the key may belong to somebody else's
    /// policy — so whatever goes wrong is put in the log and the window is
    /// built anyway. The editor opens its own files either way; it simply is
    /// not standing in Explorer's menus, which is one thing less and never
    /// one thing more than what was there before.
    ///
    /// It is written under the current user rather than the whole machine, so
    /// it never needs an administrator and never reaches past the person
    /// running the editor.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    private void RegisterWithWindows() {
        try {
            _services.GetRequiredService<IFileAssociationService>().EnsureRegistered();
        } catch (Exception failure) {
            Log.Warning(failure, "Windows could not be told about Shiranuix.");
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
