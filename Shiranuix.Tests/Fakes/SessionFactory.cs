using Shiranuix.Services;
using Shiranuix.Utilities;
using Shiranuix.ViewModels;

namespace Shiranuix.Tests.Fakes;

/// <summary>
/// Builds document sessions over the fakes: one file service and one
/// recent-files store held shared, and a fresh window, ledger and
/// navigator per session — the trio one document is.
/// </summary>
/// <remarks>
/// The session test classes hold one of these each, so every case starts
/// on a clean disk while the helpers every case leans on live in one
/// place.
/// </remarks>
public sealed class SessionFactory {
    /// <summary>The file the session tests open and save.</summary>
    public const string ExistingPath = "story.txt";

    /// <summary>The text that file holds.</summary>
    public const string ExistingText = "Saved content.";

    /// <summary>The fake disk every session of this factory reads.</summary>
    public FakeFileService Files { get; } = new();

    /// <summary>The fake store behind the recent-files list.</summary>
    public FakeRecentFilesStore RecentStore { get; } = new();

    /// <summary>
    /// Builds a fresh session over the shared fakes.
    /// </summary>
    /// <returns>A session with its own window, ledger and navigator.</returns>
    public DocumentSession CreateSession() {
        var window = new DocumentWindow(Files);
        var ledger = new EditLedger();

        return new DocumentSession(
            Files,
            RecentStore,
            new DocumentHistory(),
            window,
            ledger,
            new WindowNavigator(window, ledger));
    }

    /// <summary>
    /// Builds a session that has already opened its file: clean, on disk,
    /// and remembered.
    /// </summary>
    /// <returns>A session over <see cref="ExistingPath"/>.</returns>
    public DocumentSession CreateSessionWithFile() {
        Files.Files[ExistingPath] = ExistingText;
        var session = CreateSession();
        session.Open(ExistingPath);

        return session;
    }
}
