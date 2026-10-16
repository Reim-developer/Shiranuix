using Microsoft.Extensions.Logging;
using Shiranuix.Plugins;

namespace Shiranuix.PluginHost;

/// <summary>
/// One plugin's log: the editor's own, with the plugin's id in front of every
/// line so a log says who said what.
/// </summary>
/// <remarks>
/// A logger rather than a console, because a plugin has no console. The engine
/// behind it is the one the rest of the editor writes to — the same file, the
/// same day, the same format — so a bug report carrying that file carries the
/// plugin's lines too.
/// </remarks>
internal sealed class PluginLog : IPluginLog {
    private readonly ILogger _logger;

    /// <summary>
    /// Creates the log for one plugin.
    /// </summary>
    /// <param name="loggerFactory">The editor's log factory.</param>
    /// <param name="id">The plugin's id, which names the log it writes to.</param>
    public PluginLog(ILoggerFactory loggerFactory, string id) {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(id);

        _logger = loggerFactory.CreateLogger($"plugin:{id}");
    }

    /// <inheritdoc />
    public void Write(PluginLogLevel level, string message, Exception? error = null) {
        if (string.IsNullOrWhiteSpace(message)) {
            throw new ArgumentException("A log line has to say something.", nameof(message));
        }

        _logger.Log(Map(level), error, "Plugin said: {PluginMessage}", message);
    }

    private static LogLevel Map(PluginLogLevel level) => level switch {
        PluginLogLevel.Information => LogLevel.Information,
        PluginLogLevel.Warning => LogLevel.Warning,
        _ => LogLevel.Error
    };
}
