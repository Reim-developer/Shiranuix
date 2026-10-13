using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Shiranuix.Services;
using Shiranuix.ViewModels;
using Shiranuix.Views;

namespace Shiranuix;

/// <summary>
/// The application's service registrations: the one list that says which
/// implementation backs each interface, and how long it lives.
/// </summary>
/// <remarks>
/// This is the composition root in list form. Nothing here reaches into
/// another type to build it — the container works out every constructor
/// argument from the registrations below — so swapping an implementation is
/// one line instead of a hunt through the code.
///
/// The container is built by <see cref="App"/>, which also hands it to Splat:
/// Splat is this application's resolver, and Microsoft.Extensions.DependencyInjection
/// is the container doing the construction behind it.
/// </remarks>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Registers everything the editor needs to run.
    /// </summary>
    /// <param name="services">The collection to register into.</param>
    /// <returns>The same collection, so calls can be chained.</returns>
    public static IServiceCollection AddShiranuix(this IServiceCollection services) {
        // Stateless: one instance serves the whole session.
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IFontCatalog, FontCatalog>();

        // The two dialog services are the only ones that need a window, and
        // they are handed a way to ask for it rather than the window itself:
        // the container builds the window *from* the view model, the view
        // model from these services, and taking a window here would make the
        // container chase its own tail. The delegate is only called when a
        // dialog is shown, long after the window exists.
        services.AddSingleton<Func<Window>>(sp =>
            new Func<Window>(() => sp.GetRequiredService<MainWindow>()));
        services.AddSingleton<IFileDialogService, WindowFileDialogService>();
        services.AddSingleton<IConfirmDialogService, WindowConfirmService>();

        // One document, one window: singletons by nature.
        services.AddSingleton<EditorViewModel>();

        // Built through a factory for two reasons: the window keeps a
        // parameterless constructor for the XAML previewer, which would
        // otherwise be a second candidate the container has to choose
        // between; and going through GetRequiredService here means a missing
        // view model registration fails at startup instead of handing out a
        // window with nothing bound to it.
        services.AddSingleton(sp => new MainWindow(sp.GetRequiredService<EditorViewModel>()));

        return services;
    }
}
