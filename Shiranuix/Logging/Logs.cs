using System;
using System.IO;
using System.Runtime.InteropServices;
using Serilog;
using Serilog.Events;

namespace Shiranuix.Logging;

/// <summary>
/// Where the application writes its log, and the one call that wakes it up.
/// </summary>
/// <remarks>
/// <para>
/// Serilog is the engine and this is where it is wound up: one file a day
/// under <c>%APPDATA%\Shiranuix\logs</c>, the last fourteen days kept, and
/// a build's own measure of detail. A debug build writes everything it
/// hears — every service's Verbose line — to the file and to the
/// debugger's output window both, which is what makes the detail worth
/// having while the thing is being built. A release build writes
/// Information and up: enough for a person with a bug report to follow
/// what happened, and no more than that.
/// </para>
/// <para>
/// The current day's file is readable for anything that has to tell the
/// user where the story went — the crash dialog names the very file it
/// is writing into. Messages are structured everywhere: who wrote a line
/// is part of the line, and an exception arrives whole beneath the
/// message it belongs to, stack and all.
/// </para>
/// </remarks>
public static class Logs {
    /// <summary>
    /// The file the lines land in today — one file a day, named for the
    /// day, so the one a bug report points at is the one to read.
    /// </summary>
    public static string CurrentFile { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Shiranuix",
        "logs",
        $"shiranuix-{DateTime.Now:yyyyMMdd}.log");

    /// <summary>
    /// Wakes the engine: the file a day, the retention, and as much detail
    /// as this build was made to give. Called first thing in Main, before
    /// anything else has a reason to say a word.
    /// </summary>
    public static void Configure() {
        // The sink makes the file but not the road to it; the folder is
        // the application's own to keep.
        var directory = Path.GetDirectoryName(CurrentFile)!;
        Directory.CreateDirectory(directory);

        // The pattern, not today's file, is what the sink wants: it rolls
        // by inserting the day itself, and a name that already carries
        // one comes back with two.
        var pattern = Path.Combine(directory, "shiranuix-.log");

#if DEBUG
        // Everything, to the debugger's output window as well: the walk a
        // window took, the spans a move swallowed, the counts a keystroke
        // moved — beside the code that produced them, live. Both the gate
        // and the sink's own floor open to Verbose, since a floor above
        // the gate would quietly drop the lines before they arrive.
        var configuration = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.File(
                pattern,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: OutputTemplate,
                restrictedToMinimumLevel: LogEventLevel.Verbose)
            .WriteTo.Debug(
                outputTemplate: OutputTemplate,
                restrictedToMinimumLevel: LogEventLevel.Verbose);
#else
        // Enough for a person with a bug report to follow what happened,
        // and no more than that.
        var configuration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                pattern,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: OutputTemplate,
                restrictedToMinimumLevel: LogEventLevel.Information);
#endif

        Log.Logger = configuration.CreateLogger();
    }

    /// <summary>
    /// Says what is running before anything else happens: the version, the
    /// machine, and how the application was started — the first lines a
    /// log file opens with, so the last ones can be read against them.
    /// </summary>
    /// <param name="args">The command line, as the operating system
    /// handed it over.</param>
    public static void NoteStartup(string[] args) =>
        Log.Information(
            "Shiranuix {Version} starting — {OperatingSystem}, {Architecture}; working directory {Directory}; arguments [{Arguments}]",
            AppVersion.Text,
            RuntimeInformation.OSDescription,
            RuntimeInformation.ProcessArchitecture,
            Environment.CurrentDirectory,
            string.Join(' ', args));

    /// <summary>
    /// Hands the engine's lines over — called on the way out, so the last
    /// lines a crash cares about are on disk before the process is.
    /// </summary>
    public static void CloseAndFlush() => Log.CloseAndFlush();

    // Timestamped to the millisecond, leveled, and attributed to whoever
    // wrote it — with the whole exception beneath the message, newlines
    // and all, because a stack collapsed to one line is a stack nobody
    // can read.
    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext:l}: {Message:lj}{NewLine}{Exception}";
}
