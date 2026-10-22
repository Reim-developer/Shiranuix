using Avalonia.Controls;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Shiranuix.Graphs;
using Shiranuix.Graphs.Dialogs;
using Shiranuix.Graphs.Persistence;
using Shiranuix.Persistence;
using Shiranuix.Services;
using Shiranuix.Services.Dialogs;
using Shiranuix.Services.FileAssociations;
using Shiranuix.Services.Fonts;
using Shiranuix.Services.Pdf;
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
        // Logging is registered first, so every registration below can be
        // reached from its very first moment. Serilog is the engine: the
        // root logger is here for whatever needs it directly, and the
        // bridge gives services the framework's own ILogger<T> — same
        // engine, their type as the source, so every line in the file
        // says who wrote it.
        services.AddSingleton(Log.Logger);
        services.AddLogging(builder => builder.AddSerilog(Log.Logger, dispose: false));

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

        // The relationship map's strike-out question crosses into this
        // application as a question alone — its words and the window it
        // stands in front of — because the dialog that draws it is the one
        // every other warning is drawn in, and where it stands is the map's
        // to say.
        services.AddSingleton<IConfirmQuestion, WindowConfirmQuestion>();

        // The export's own question — the typeface and the size the paper is
        // drawn in — is a chooser of its own, not one of the generic prompts:
        // it carries two pickers and answers with options, the way the file
        // dialog answers with a path. Made new each ask, for the same reason
        // the dialogs are: a window answered with something is spent.
        services.AddSingleton<IExportPdfDialogService, WindowExportPdfDialogService>();

        // The relationship map's own questions — a graph's name, a
        // character whole, a tie's two ends — are a conversation of their
        // own, so they get one service rather than one each; every dialog
        // inside it is made new per ask, the way all the window dialogs
        // are: a window answered with something is spent.
        services.AddSingleton<IGraphDialogService, WindowGraphDialogService>();

        // A file may name a typeface this machine has never carried — the
        // name travelled with the words, and this is where that is settled
        // before any of them land. A chooser of its own rather than one of
        // the generic prompts: it carries a face list and a sample of the
        // file's own writing, and answers with a face.
        services.AddSingleton<IMissingFontDialogService, WindowMissingFontDialogService>();

        // Settings: the title band's button reaches a window rather than a
        // dialog — modeless and revisited, so the service keeps hold of the
        // one it has open, and each later press makes a fresh one, because a
        // closed window cannot be shown again. Every window is built only at
        // the moment settings are actually opened.
        services.AddSingleton<ISettingsWindowService, SettingsWindowService>();
        services.AddTransient(sp => new SettingsWindow(
            sp.GetRequiredService<TypographyState>(),
            sp.GetRequiredService<KeyBindingState>()));

        // How a long composition is kept from freezing the input method:
        // stateless, so one instance answers for every composition the window
        // hears.
        services.AddSingleton<IImeCompositionGuard, ImeCompositionGuard>();

        // Whether the editor stands in the machine's own menus: a
        // .shiranuix file named a Shiranuix Document, and a .txt file
        // offering to be opened with one. Written once and left standing —
        // the plan it writes from is in code and can be read off the
        // screen, so nothing here is asked of anybody. Registered for
        // every system and constructed on none but Windows's: the
        // container is happy to hold the registration anywhere; only the
        // ask reaches for the service, and that ask stands behind an OS
        // check of its own.
#pragma warning disable CA1416 // A registration is not a construction; the Windows half stands behind its own guard.
        services.AddSingleton<IFileAssociationService, WindowsFileAssociationService>();
#pragma warning restore CA1416

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
        services.AddSingleton<DocumentFile>();
        services.AddSingleton<DocumentWindow>();
        services.AddSingleton<EditLedger>();
        services.AddSingleton<WindowNavigator>();
        services.AddSingleton<DocumentSession>();
        services.AddSingleton<TypographyState>();
        // The same state under the name the writing asks for: family, size
        // and the word that either moved, which is the whole of typography a
        // document can carry into its file. One registration through the
        // other, so both names are the one object — the picker, the page and
        // the saved file must never be looking at different type.
        services.AddSingleton<IDocumentTypography>(sp =>
            sp.GetRequiredService<TypographyState>());
        // The shape the last export to PDF was drawn in, held between
        // sessions beside the typography it falls back to.
        services.AddSingleton<PdfExportState>();

        // A paper read beside the writing: a PDF is not text the writer
        // revises, so it is handed to the reader's window instead of the
        // document. The reading is built over its file only when it is asked
        // for — a bad file is an outcome, not a startup failure — and the
        // window is made new when the old one is gone.
        services.AddSingleton<Func<string, PdfViewerState>>(
            sp => path => new PdfViewerState(path, new PdfPages(path)));
        services.AddSingleton<IPdfViewerService, PdfViewerWindowService>();

        // The relationship map's own window: modeless like settings and the
        // reader, one standing window brought forward while it stands, and a
        // fresh state opened on whatever the store holds each time it is
        // made — because a closed window cannot be shown again, and the
        // graph should come back as it was left rather than as a stale
        // window once saw it.
        services.AddSingleton<IGraphWindowService, GraphWindowService>();
        services.AddTransient<GraphState>();

        // The wiki's own window: the cast of every story, kept once however
        // many maps they stand in — modeless like the map beside it, one
        // standing window brought forward while it stands, and a fresh state
        // opened on whatever the wiki holds each time it is made.
        services.AddSingleton<IWikiWindowService, WikiWindowService>();
        services.AddTransient<WikiState>();

        // The picker a character's face is chosen with: the app knows how a
        // file is picked on the machine it runs on, and the map only asks
        // for a picture.
        services.AddSingleton<Graphs.Dialogs.IImagePicker, WindowImagePicker>();
        // What each command answers to on the keyboard: the defaults in
        // code, the writer's moves over them from the store. Built once,
        // like the typography it sits beside.
        services.AddSingleton<KeyBindingState>();
        services.AddSingleton<EditorViewModel>();

        // The column down the left of the page, where the editor's own file
        // tree panel is drawn. A view model state like the typography beside
        // it; the tree offers its panel once it has a folder to stand on.
        services.AddSingleton<PanelRegistry>();

        // Persistence: one SQLite file under %APPDATA%, reached through a
        // factory because a DbContext is not thread-safe and so is built per
        // operation rather than shared between them.
        services.AddDbContextFactory<ShiranuixContext>(options =>
            options.UseSqlite(ShiranuixContext.ConnectionString)
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
        services.AddSingleton<IUserSettings, SqliteUserSettings>();
        services.AddSingleton<IRecentFilesStore, SqliteRecentFilesStore>();
        services.AddSingleton<IFontHistoryStore, SqliteFontHistoryStore>();
        services.AddSingleton<IKeyBindingsStore, SqliteKeyBindings>();
        services.AddSingleton<IGraphStore, SqliteGraphStore>();

        // The wiki: the cast of every story, kept once however many maps
        // they stand in. Beside the graphs rather than inside them, because
        // a character is a character whatever map they are drawn on.
        services.AddSingleton<IWikiStore, SqliteWikiStore>();

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
                sp.GetRequiredService<IGraphWindowService>(),
                sp.GetRequiredService<IWikiWindowService>(),
                sp.GetRequiredService<IImeCompositionGuard>(),
                sp.GetRequiredService<KeyBindingState>()));

        return services;
    }
}
