using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Narika.Services.Install;
using Narika.ViewModels;
using Narika.ViewModels.Pages;
using Narika.Views;
using Splat.Microsoft.Extensions.DependencyInjection;

namespace Narika;

public partial class App : Application {
    /// <summary>
    /// The service container. It is also Splat's resolver — see the
    /// constructor — so this field is only here to give the composition
    /// root a typed, fail-fast way to ask for the window.
    /// </summary>
    private readonly ServiceProvider _services;

    /// <summary>
    /// Builds the container and hands it to Splat, wired the editor's way:
    /// one call registers the whole wizard, and from there Splat *is* this
    /// container.
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
            desktop.MainWindow = _services.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }
}

/// <summary>
/// Where the wizard is put together: every page, the walk over them, the
/// window that shows it, and the services they are built against — one
/// registration each, so nothing anywhere reaches for anything by hand.
/// </summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Registers the installer: its one shared request, its pages, the
    /// wizard over them, the window, and the services behind them.
    /// </summary>
    /// <param name="services">The container being built.</param>
    /// <returns>The same container, for the next registration.</returns>
    public static IServiceCollection AddNarika(this IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);

        // The one install request every page writes into and the service
        // is finally handed: built once, here, and shared by reference.
        services.AddSingleton(new InstallOptions());

        // Where the bytes come from and how they are put down. Both are
        // named rather than reached for: the day the download becomes
        // real, this line is what changes.
        services.AddSingleton<IReleaseCatalog, KnownReleaseCatalog>();
        services.AddSingleton<IInstallService, StubInstallService>();
        services.AddSingleton<IAppLauncher, ProcessAppLauncher>();

        // The folder picker needs the window, and the window needs the
        // wizard, and the wizard needs the picker: the delegate breaks the
        // circle by not asking for the window until a folder is actually
        // being picked, by which point the window exists.
        services.AddSingleton(provider =>
            new Func<Window>(() => provider.GetRequiredService<MainWindow>()));
        services.AddSingleton<IFolderPickerService, WindowFolderPickerService>();

        // The pages, the walk over them, and the window that shows it.
        services.AddSingleton<WelcomePageViewModel>();
        services.AddSingleton<DestinationPageViewModel>();
        services.AddSingleton<OptionsPageViewModel>();
        services.AddSingleton<ProgressPageViewModel>();
        services.AddSingleton<FinishPageViewModel>();
        services.AddSingleton<InstallerViewModel>();
        services.AddSingleton<MainWindow>();

        return services;
    }
}
