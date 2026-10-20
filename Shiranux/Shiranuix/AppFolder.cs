using System;
using System.IO;

namespace Shiranuix;

/// <summary>
/// Where the editor keeps the things it has to keep: its log and its store.
/// </summary>
/// <remarks>
/// <para>
/// One answer for both, asked once, because two answers computed
/// separately are two places for a path to go wrong - and because a log in
/// one folder beside a database in another is a bug report nobody can follow.
/// </para>
/// <para>
/// By default that is <c>%APPDATA%\Shiranuix</c>, which is where Windows
/// keeps what an application owns. Setting <c>SHIRANUIX_HOME</c> to another
/// folder moves both at once, which is how the editor runs off a memory
/// stick, and how the measuring tool asks what a first run costs - a question
/// that cannot be asked against somebody's real settings, because Windows
/// decides where those live and ignores what the environment says.
/// </para>
/// </remarks>
public static class AppFolder {
    /// <summary>
    /// The variable that moves everything, when it is set at all.
    /// </summary>
    public const string HomeVariable = "SHIRANUIX_HOME";

    /// <summary>The editor's own folder, created only when something is written to it.</summary>
    public static string Root { get; } = Resolve();

    /// <summary>Where the log files are.</summary>
    public static string Logs { get; } = Path.Combine(Root, "logs");

    /// <summary>Where the store lives.</summary>
    public static string Database { get; } = Path.Combine(Root, "shiranuix.db");

    /// <summary>
    /// Makes the folder, for the callers that are about to write into it.
    /// Reading something never creates anything.
    /// </summary>
    public static void EnsureExists() => Directory.CreateDirectory(Root);

    /// <summary>
    /// Works out the folder: the one that was asked for, or the one Windows
    /// keeps for applications.
    /// </summary>
    /// <returns>The folder to keep everything in.</returns>
    private static string Resolve() {
        var asked = Environment.GetEnvironmentVariable(HomeVariable);

        return string.IsNullOrWhiteSpace(asked)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Shiranuix")
            : Path.GetFullPath(asked.Trim());
    }
}
