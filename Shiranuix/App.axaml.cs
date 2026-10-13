using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
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
        }

        base.OnFrameworkInitializationCompleted();
    }
}
