using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using Narika.Services.Download;
using Narika.Services.Install;
using Narika.Services.Setup;
using Narika.Services.Theme;
using Narika.ViewModels;
using Narika.ViewModels.Pages;
using Splat.Microsoft.Extensions.DependencyInjection;

namespace Narika;

[SupportedOSPlatform("windows")]
public partial class App : Application {
    /// <summary>
    /// The service container. It is also Splat's resolver — see the
    /// constructor — so this field is only here to give the composition
    /// root a typed, fail-fast way to ask for the launcher's window.
    /// </summary>
    private readonly ServiceProvider _services;

    /// <summary>
    /// Builds the container and hands it to Splat, wired the editor's way:
    /// one call registers the whole launcher, and from there Splat *is*
    /// this container.
    /// </summary>
    public App() {
        var services = new ServiceCollection();
        services.AddNarika();

        _services = services.BuildServiceProvider();
        _services.UseMicrosoftDependencyResolver();
    }

    public override void Initialize() {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted() {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            // The launcher is the window the machine opened: closing it
            // ends the session even with a setup wizard still standing,
            // while closing the wizard alone leaves the launcher exactly
            // where it was.
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.MainWindow = _services.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }
}

/// <summary>
/// Where the launcher is put together: its state, its services, the
/// wizard chain it opens when a card is pressed, the two windows, and
/// the theme they are all drawn in — one registration each, so nothing
/// anywhere reaches for anything by hand.
/// </summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Registers the launcher: the install state both windows read, the
    /// fetch behind every install, the wizard's fresh walk per opening,
    /// the windows over them, and the theme service.
    /// </summary>
    /// <param name="services">The container being built.</param>
    /// <returns>The same container, for the next registration.</returns>
    [SupportedOSPlatform("windows")]
    public static IServiceCollection AddNarika(this IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);

        // The theme: remembered across runs in the app's own settings
        // folder, applied straight to the running application, and built
        // at window-resolution time — by which point the application the
        // apply call reaches for exists.
        services.AddSingleton(provider => new ThemeService(
            ApplyTheme,
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Narika",
                "theme.txt")));

        // What is installed, said by both windows: the wizard writes it
        // when a walk finishes, Repair writes it again after its own walk,
        // and the launcher reads it when its commands are next asked. It
        // is written down beside this application's own settings, because
        // a program still on the machine after the launcher closes is
        // still installed tomorrow — and it is believed only while the
        // file it names is still there, which is the whole of IInstallState.
        services.AddSingleton<IInstallState>(_ => new JsonInstallState(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Narika",
                "install.json")));

        // Where the bytes come from and how they are put down. One client
        // reaches out, with no timeout of its own: the archive is tens of
        // megabytes and a slow line is not a failure, so the Cancel a
        // person can press is what stops a fetch instead. The machine —
        // its runtime identifier, its archive shape, the name its program
        // goes by — is read once here and handed to everyone who would
        // otherwise have to guess a platform for themselves: the catalog
        // picks this machine's asset out of a release's several, and the
        // install unpacks exactly that shape. The catalog asks the release
        // pages and falls back to the pinned release when they cannot be
        // reached; the download lands beside this application's state
        // under a name that says it is unfinished until it is; the install
        // unpacks that copy, leaves the marks the options page offered,
        // and takes the archive back; and the marks themselves are the
        // shell's own link files, written where each surface keeps them.
        services.AddSingleton(_ => {
            var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Narika (the Shiranuix launcher)");
            return http;
        });
        services.AddSingleton(_ => Runtime.ForMachine(InstallerViewModel.AppId));
        services.AddSingleton<IReleaseCatalog>(provider => new GitHubReleaseCatalog(
            provider.GetRequiredService<HttpClient>(),
            new KnownReleaseCatalog(provider.GetRequiredService<Runtime>()),
            provider.GetRequiredService<Runtime>()));
        services.AddSingleton<IDownloadService>(provider => new HttpDownloadService(
            provider.GetRequiredService<IReleaseCatalog>(),
            InstallerViewModel.AppId,
            provider.GetRequiredService<HttpClient>()));
        services.AddSingleton<IShortcutService, ShortcutService>();
        services.AddSingleton<IInstallService, ReleaseInstallService>();
        services.AddSingleton<IAppLauncher, ProcessAppLauncher>();

        // The setup wizard: a fresh window, view model and set of pages
        // every time it is opened, so no walk remembers the one before
        // it. The install request is the one piece that outlives a walk —
        // it has to, or the three pages of one walk would each hold a
        // different copy of the same request, and it remembers the folder
        // a person chose last time as where the box starts this time.
        services.AddSingleton<ISetupWindowService, SetupWindowService>();
        services.AddSingleton<InstallOptions>();
        services.AddTransient<DestinationPageViewModel>();
        services.AddTransient<OptionsPageViewModel>();
        services.AddTransient<ProgressPageViewModel>();
        services.AddTransient<FinishPageViewModel>();
        services.AddTransient<InstallerViewModel>();
        services.AddTransient<SetupWindow>();

        // The folder picker inside the wizard needs the window the wizard
        // is standing in, and that window is only ever open when a folder
        // is actually being picked: the delegate asks the setup service at
        // that moment rather than the container at construction time,
        // which is what breaks the circle.
        services.AddSingleton(provider => new Func<Window>(() =>
            provider.GetRequiredService<ISetupWindowService>().CurrentWindow
            ?? throw new InvalidOperationException(
                "A folder can only be picked while the setup window is open.")));
        services.AddSingleton<IFolderPickerService, WindowFolderPickerService>();

        // The launcher's window and the view model it is drawn from.
        services.AddSingleton<LauncherViewModel>();
        services.AddSingleton<MainWindow>();

        return services;
    }

    /// <summary>
    /// Puts one mode onto the running application: the machine's own
    /// answer, or the light or dark face chosen in the header.
    /// </summary>
    /// <param name="mode">The mode to show.</param>
    private static void ApplyTheme(ThemeMode mode) {
        if (Application.Current is { } application) {
            application.RequestedThemeVariant = mode switch {
                ThemeMode.Light => ThemeVariant.Light,
                ThemeMode.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default
            };
        }
    }
}
