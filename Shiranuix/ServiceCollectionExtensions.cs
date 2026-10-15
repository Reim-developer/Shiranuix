using Avalonia.Controls;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shiranuix.Persistence;
using Shiranuix.Services;
using Shiranuix.Services.Dialogs;
using Shiranuix.Services.Fonts;
using Shiranuix.Utilities;
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

        // The two dialog services need a window to show in, and they are
        // handed a way to ask for it rather than the window itself: the
        // container builds the window *from* the view model, the view model
        // from these services, and taking a window here would make the
        // container chase its own tail. The delegate is only called when a
        // dialog is shown, long after the window exists.
        services.AddSingleton<Func<Window>>(sp =>
            new Func<Window>(() => sp.GetRequiredService<MainWindow>()));
        services.AddSingleton<IFileDialogService, WindowFileDialogService>();
        services.AddSingleton<IConfirmDialogService, WindowConfirmService>();

        // The one dialog window every prompt is drawn in. The confirm service
        // above describes what to ask; this is what shows it.
        services.AddSingleton<IDialogService, WindowDialogService>();

        // Settings: the title band's button reaches a window rather than a
        // dialog — modeless and revisited, so the service keeps hold of the
        // one it has open, and each later press makes a fresh one, because a
        // closed window cannot be shown again. Every window is built only at
        // the moment settings are actually opened.
        services.AddSingleton<ISettingsWindowService, SettingsWindowService>();
        services.AddTransient(sp => new SettingsWindow(sp.GetRequiredService<TypographyState>()));

        // How a long composition is kept from freezing the input method:
        // stateless, so one instance answers for every composition the window
        // hears.
        services.AddSingleton<IImeCompositionGuard, ImeCompositionGuard>();

        // One document, one window, one typography behind them: singletons by
        // nature. The two states below are built by the container and handed to
        // the view model, which coordinates them rather than reaching inside
        // them. The history belongs to the document — the text box's own dies
        // whenever the text is replaced from outside — so it is built here too
        // and handed to the session with the rest. The window engine walks the
        // document's file for it: same lifetime, same reason — and the
        // navigator that travels the window over the file shares both, planning
        // against the same window and the same moves the session holds.
        services.AddSingleton<IDocumentHistory, DocumentHistory>();
        services.AddSingleton<DocumentWindow>();
        services.AddSingleton<EditLedger>();
        services.AddSingleton<WindowNavigator>();
        services.AddSingleton<DocumentSession>();
        services.AddSingleton<TypographyState>();
        services.AddSingleton<EditorViewModel>();

        // Persistence: one SQLite file under %APPDATA%, reached through a
        // factory because a DbContext is not thread-safe and so is built per
        // operation rather than shared between them.
        services.AddDbContextFactory<ShiranuixContext>(options =>
            options.UseSqlite(ShiranuixContext.ConnectionString));
        services.AddSingleton<IUserSettings, SqliteUserSettings>();
        services.AddSingleton<IRecentFilesStore, SqliteRecentFilesStore>();
        services.AddSingleton<IFontHistoryStore, SqliteFontHistoryStore>();

        // Built through a factory for two reasons: the window keeps a
        // parameterless constructor for the XAML previewer, which would
        // otherwise be a second candidate the container has to choose
        // between; and going through GetRequiredService here means a missing
        // view model registration fails at startup instead of handing out a
        // window with nothing bound to it.
        services.AddSingleton(sp =>
            new MainWindow(
                sp.GetRequiredService<EditorViewModel>(),
                sp.GetRequiredService<ISettingsWindowService>(),
                sp.GetRequiredService<IImeCompositionGuard>()));

        return services;
    }
}
